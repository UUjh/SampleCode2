using UnityEngine;
using Core.Base;
using Core.Data;
using System.Collections;
using System.IO;
using UnityEngine.InputSystem;
using System.Runtime.InteropServices;
using System;
using System.Linq;

namespace Core.Managers
{
    /// <summary>
    /// 윈도우/모니터/캔버스 관리 매니저
    /// - 모니터 전환, 항상 위 설정, 캔버스 크기/바닥 Y 계산
    /// - 파일 드롭 이벤트 처리 및 휴지통 이동(Win32 API)
    /// </summary>
    public class WindowManager : Singleton<WindowManager>
    {
        protected override void Awake()
        {
            base.Awake();
            
            // 모니터 설정 변경 이벤트 구독
            if (windowController != null)
            {
                windowController.OnStateChanged += HandleWindowStateChanged;
                windowController.OnDropFiles += OnFilesDropped;  // 파일 드롭 이벤트 구독
                windowController.allowDropFiles = false;  // 기본적으로 드롭 비활성화
                _lastMonitorSize = new Vector2(
                    DataManager.Instance.WindowSettings.monitorWidth,
                    DataManager.Instance.WindowSettings.monitorHeight
                );
            }
        }

        protected override void Start()
        {
            base.Start();

            ApplyWindowSettings(DataManager.Instance.WindowSettings);
        }
        
        protected override void OnDestroy()
        {
            if (windowController != null)
            {
                windowController.OnStateChanged -= HandleWindowStateChanged;
                windowController.OnDropFiles -= OnFilesDropped;  // 파일 드롭 이벤트 구독 해제
            }
            
            base.OnDestroy();
        }

        private void ApplyWindowSettings(WindowSettings settings)
        {
            if (windowController != null)
            {
                windowController.shouldFitMonitor = true;
                windowController.monitorToFit = settings.activeMonitor;
                windowController.isTopmost = settings.alwaysOnTop;
            }
            
            _curRootHeightOffset = settings.rootHeight;

            UpdateCanvasSize();
            UpdateRootPosition();
        }

        public float GetTopWorldY() => _rootTransform != null ? _rootTransform.position.y : 0f;
        
        public float GetCanvasBottomY()
        {
            if (_mainCanvas == null) return GetTopWorldY();
            
            RectTransform canvasRect = _mainCanvas.GetComponent<RectTransform>();
            float canvasWorldY = canvasRect.position.y;
            float canvasHeight = canvasRect.sizeDelta.y * canvasRect.localScale.y;
            return canvasWorldY - (canvasHeight / 2f);
        }
        
        public void SetRootHeight(float heightOffset)
        {
            _curRootHeightOffset = Mathf.Max(heightOffset, _minRootHeight);
            
            UpdateRootPosition();
            
            var settings = DataManager.Instance.WindowSettings;
            settings.rootHeight = _curRootHeightOffset;
            DataManager.Instance.UpdateWindowSettings(settings);
        }

        public void AdjustRootHeight(float deltaHeight)
        {
            SetRootHeight(_curRootHeightOffset + deltaHeight);
        }
        
        public void SwitchMonitor(int monitorIndex)
        {
            if (windowController != null)
            {
                bool wasTopmost = windowController.isTopmost;
                
                // 최상단 상태 일시 해제 (모니터 전환 중 포커스 문제 방지)
                if (wasTopmost)
                {
                    windowController.isTopmost = false;
                }

                // 목표 모니터로 전환
                windowController.monitorToFit = monitorIndex;

                // 변경 완료 후 값들 확인 및 진단 로그
                var monitorRect = windowController.GetMonitorRect(monitorIndex);
                var cam = Camera.main;

                // 카메라 aspect를 새 모니터 해상도에 맞게 업데이트
                if (cam != null)
                {
                    float monitorAspect = monitorRect.width / monitorRect.height;
                    cam.aspect = monitorAspect;
                }
                
                // 모니터 전환 완료 후 설정 업데이트
                var settings = DataManager.Instance.WindowSettings;
                settings.activeMonitor = monitorIndex;
                settings.rootHeight = _curRootHeightOffset;
                settings.monitorWidth = (int)monitorRect.width;
                settings.monitorHeight = (int)monitorRect.height;
                
                // 설정 저장 및 적용
                DataManager.Instance.UpdateWindowSettings(settings);
                ApplyWindowSettings(settings);

                // 최상단 상태 복원
                if (wasTopmost)
                {
                    StartCoroutine(RestoreTopmost());
                }
            }
        }

        private IEnumerator RestoreTopmost()
        {
            yield return null;  // 다음 프레임까지만 대기
            
            if (windowController != null)
            {
                windowController.isTopmost = true;
            }
        }
        
        private void UpdateCanvasSize()
        {
            int monitorIndex = DataManager.Instance.WindowSettings.activeMonitor;
            Rect monitorRect = GetMonitorRect(monitorIndex);
            float monitorWidth = monitorRect.width;
            float monitorHeight = monitorRect.height;

            if (monitorWidth <= 0 || monitorHeight <= 0)
            {
                monitorWidth = 1920f;
                monitorHeight = 1080f;
            }

            Camera cam = Camera.main;
            if (_mainCanvas != null && cam != null && cam.orthographic)
            {
                RectTransform canvasRect = _mainCanvas.GetComponent<RectTransform>();
                Vector2 oldSize = canvasRect.sizeDelta;
                
                float targetAspect = monitorWidth / monitorHeight;
                cam.aspect = targetAspect;
                cam.orthographicSize = _cameraOrthographicSize;

                float worldHeight = _cameraOrthographicSize * 2f;
                float worldWidth = worldHeight * targetAspect;

                float scale = _mainCanvas.transform.localScale.x;

                float sizeDeltaX = worldWidth / scale;
                float sizeDeltaY = worldHeight / scale;
                Vector2 newSize = new Vector2(sizeDeltaX, sizeDeltaY);
                
                canvasRect.sizeDelta = newSize;
            }
        }
        
        private void UpdateRootPosition()
        {
            if (_rootTransform == null || Camera.main == null) return;
            
            Camera cam = Camera.main;
            Vector3 position = _rootTransform.position;
            float newY = position.y;
            
            if (DataManager.Instance != null)
            {
                int monitorIndex = DataManager.Instance.WindowSettings.activeMonitor;
                var monitorRect = Kirurobo.UniWindowController.GetMonitorRect(monitorIndex);
                
                if (monitorRect.height > 0)
                {
                    float worldHeight = cam.orthographicSize * 2f;
                    
                    // 픽셀당 월드 단위 계산
                    float pixelToWorldY = worldHeight / monitorRect.height;
                    
                    float taskbarOffset = _taskbarHeightPx * pixelToWorldY;
                    float heightOffset = _curRootHeightOffset * pixelToWorldY;
                    newY = taskbarOffset + heightOffset;
                }
                else
                {
                    Debug.LogError($"[UpdateRootPosition] 모니터 해상도가 유효하지 않음 (height: {monitorRect.height})");
                }
            }
            else
            {
                Debug.LogError("[UpdateRootPosition] DataManager.Instance가 null임");
            }
            
            // 실제로 위치가 변경될 때만 적용
            if (Mathf.Abs(position.y - newY) > 0.001f)
            {
                position.y = newY;
                _rootTransform.position = position;
            }
        }

        private void HandleWindowStateChanged(WindowStateEventType type)
        {
            if (type == WindowStateEventType.Resized)
            {
                if (windowController != null)
                {
                    int currentMonitor = windowController.monitorToFit;
                    var monitorRect = GetMonitorRect(currentMonitor);
                    Vector2 newSize = new Vector2(monitorRect.width, monitorRect.height);
                    
                    // 실제로 해상도가 변경되었을 때만 처리
                    if (newSize != _lastMonitorSize)
                    {
                        // 현재 상태 저장
                        bool wasTopmost = windowController.isTopmost;
                        
                        // 최상단 상태 일시 해제
                        if (wasTopmost)
                        {
                            windowController.isTopmost = false;
                        }
                        
                        // 현재 설정과 비교
                        var settings = DataManager.Instance.WindowSettings;
                        if (settings.monitorWidth != (int)monitorRect.width || 
                            settings.monitorHeight != (int)monitorRect.height)
                        {
                            // 설정 업데이트
                            settings.monitorWidth = (int)monitorRect.width;
                            settings.monitorHeight = (int)monitorRect.height;
                            DataManager.Instance.UpdateWindowSettings(settings);
                            ApplyWindowSettings(settings);
                        }
                        
                        _lastMonitorSize = newSize;

                        // 최상단 상태 복원
                        if (wasTopmost)
                        {
                            StartCoroutine(RestoreTopmost());
                        }
                    }
                }
            }
        }

        private void MoveFilesBin(string[] paths)
        {
            // Win32 휴지통 이동 기능
        }

        private void OnFilesDropped(string[] paths)
        {
            if (paths == null || paths.Length == 0)
            {
                Debug.LogError("드롭된 항목이 없습니다.");
                return;
            }

            // UI가 열려있는지 확인
            if (InputManager.Instance.IsOverOpenPanel())
            {
                Debug.LogError("UI 패널이 열려있어 드롭을 무시합니다.");
                return;
            }

            // 파일 드롭 → 휴지통 이동 동작
        }

    }
} 