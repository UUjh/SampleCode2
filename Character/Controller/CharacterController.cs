using UnityEngine;
using Character.Data;
using Character.States;
using Core.Managers;
using Core.Base;
using Spine.Unity;
using Objects;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Character.Controller
{
    /// <summary>
    /// 캐릭터 컨트롤러
    /// - 상태 머신 구동(Enter/Update/Exit), 클릭/드래그 입력 처리
    /// - 오브젝트 등록/이벤트 구독, 애니메이션/사운드 트리거
    /// - 화면 크기/바닥 높이 변화에 따른 위치 보정
    /// </summary>
    public class CharacterController : MonoBehaviour, IClickable, IPointerEnterHandler, IPointerExitHandler
    {
        // CharacterManager에서 할당하는 슬롯 인덱스 (0~2)
        public int CharacterSlotIndex { get; set; }
        // 캐릭터의 기본 데이터
        public CharacterData characterData;

        // 현재 상태 관리
        private StateBase _curState;
        public StateBase CurState => _curState;
        
        // 애니메이션 컴포넌트
        private SkeletonAnimation _skeletonAnimation;

        [SerializeField]
        private GameObject coinObj;
        
        // 배부름 시스템
        [SerializeField]
        private float _curFullness;
        [SerializeField]
        private float _fullnessRate;
        [SerializeField] private float _hungerThreshold = 40f;
        [SerializeField] private float _starvingThreshold = 0f;
        private bool _isDragging = false;
        private FoodBowl _foodBowl;
        public FoodBowl FoodBowl => _foodBowl;
        // 동적 최적 캣타워 선택 (현재 사용 중인 것이 있으면 우선, 없으면 최적 선택)
        public Tower Tower => _curTower ?? ObjectManager.Instance.FindBestTower(this);
        
        // 현재 사용 중인 캣타워 (슬롯을 점유하고 있는 경우)
        private Tower _curTower;
        private PlacedToy _placedToy;
        public PlacedToy PlacedToy => _placedToy;
        private MovingToy _movingToy;
        public MovingToy MovingToy => _movingToy;
        // 동적 최적 소파 선택 (현재 사용 중인 것이 있으면 그것 우선, 없으면 최적 선택)
        public Sofa Sofa => _curSofa ?? ObjectManager.Instance.FindBestSofa(this);
        
        // Sofa Doze/Sleep 시작 시점 DrawOrder 고정용 캐시
        private System.Collections.Generic.List<Spine.Slot> _sofaDozeDrawOrder;
        private System.Collections.Generic.List<Spine.Slot> _sofaSleepDrawOrder;
        
        // 현재 사용 중인 소파 (슬롯을 점유하고 있는 경우)
        private Sofa _curSofa;

        /// <summary>
        /// 가구 사용 시작 시 현재 사용 중인 가구로 설정
        /// </summary>
        public void SetCurTower(Tower tower)
        {
            _curTower = tower;
        }

        public void SetCurSofa(Sofa sofa)
        {
            _curSofa = sofa;
        }

        /// <summary>
        /// 가구 사용 종료 시 현재 가구 해제
        /// </summary>
        public void ClearCurTower()
        {
            _curTower = null;
        }

        public void ClearCurSofa()
        {
            _curSofa = null;
        }
        
        private Coroutine _statusCo;

        // 클릭 보상 시간 추적
        private float _lastClickRewardTime;
        private const float CLICK_MIN_INTERVAL = 30f;   // 최소 대기 시간
        private const float CLICK_MAX_INTERVAL = 120f;  // 최대 보상 시간
        private const float COIN_DISPLAY_DURATION = 5f; // 코인 표시 지속 시간
        private bool _isCoinDisplayed = false;
        private bool _isCoinShown = true;
        private float _coinDisplayStartTime = 0f;

        // 캐릭터가 점유한 캣타워 슬롯 인덱스 (없으면 null)
        public int? CurTowerSlot { get; set; }
        public int? CurSofaSlot { get; set; }

        void Awake()
        { 
            _skeletonAnimation = GetComponent<SkeletonAnimation>();
        }


        private void Start()
        {
            if (coinObj != null)
                coinObj.SetActive(false);

            _curFullness = 100f;
            _fullnessRate = 100f / characterData.hungerThreshold;
            _isDragging = false;
            
            // ObjectManager에 자기 자신을 등록
            ObjectManager.Instance.RegisterCharacter(this);
            
            // ObjectManager에서 다른 오브젝트들을 가져오거나 등록 이벤트 구독
            _foodBowl = ObjectManager.Instance.FoodBowl;
            if (_foodBowl == null)
            {
                // FoodBowl이 아직 등록되지 않았으면 등록 이벤트 구독
                ObjectManager.Instance.OnFoodBowlRegistered += OnFoodBowlRegistered;
                Debug.LogWarning("[CharacterController] FoodBowl이 아직 등록되지 않았습니다. 등록 대기 중...");
            }
            
            // 스마트 캣타워 선택: 동적으로 최적 캣타워를 선택함
            if (ObjectManager.Instance.Towers.Count == 0)
            {
                // Tower가 아직 등록되지 않았으면 등록 이벤트 구독
                ObjectManager.Instance.OnTowerRegistered += OnTowerRegistered;
                Debug.LogWarning("[CharacterController] Tower가 아직 등록되지 않았습니다. 등록 대기 중...");
            }
            
            _placedToy = ObjectManager.Instance.PlacedToy;
            if (_placedToy == null)
            {
                ObjectManager.Instance.OnPlacedToyRegistered += OnPlacedToyRegistered;
                Debug.LogWarning("[CharacterController] PlacedToy가 아직 등록되지 않았습니다. 등록 대기 중...");
            }
            
            _movingToy = ObjectManager.Instance.MovingToy;
            if (_movingToy == null)
            {
                ObjectManager.Instance.OnMovingToyRegistered += OnMovingToyRegistered;
                Debug.LogWarning("[CharacterController] MovingToy가 아직 등록되지 않았습니다. 등록 대기 중...");
            }

            // 스마트 소파 선택: 동적으로 최적 소파를 선택함
            if (ObjectManager.Instance.Sofas.Count == 0)
            {
                ObjectManager.Instance.OnSofaRegistered += OnSofaRegistered;
                Debug.LogWarning("[CharacterController] Sofa가 아직 등록되지 않았습니다. 등록 대기 중...");
            }

            Vector3 curPos = transform.position;
            curPos.y = WindowManager.Instance.GetCanvasBottomY();
            transform.position = curPos;
            
            DataManager.Instance.onWindowSettingsChanged += OnWindowSettingsChanged;
            
            // 배고픔 시스템 코루틴 시작
            _statusCo = StartCoroutine(StatusCo());
            
            ChangeState(new DecisionState(this, false));

            // 첫 클릭 시 최대 보상을 주기 위해 타이머를 최대 대기 시간만큼 과거로 설정
            _lastClickRewardTime = Time.time - CLICK_MAX_INTERVAL;
        }

        void Update()
        {
            if (_curState != null)
            {
                _curState.Update();
            }
        }

        void OnDestroy()
        {
            // 배고픔 코루틴 정리
            if (_statusCo != null)
            {
                StopCoroutine(_statusCo);
                _statusCo = null;
            }
            
            // ObjectManager에서 자기 자신을 등록 해제
            if (ObjectManager.Instance != null)
            {
                ObjectManager.Instance.UnregisterCharacter(this);
                ObjectManager.Instance.OnFoodBowlRegistered -= OnFoodBowlRegistered;
                ObjectManager.Instance.OnTowerRegistered -= OnTowerRegistered;
                ObjectManager.Instance.OnPlacedToyRegistered -= OnPlacedToyRegistered;
                ObjectManager.Instance.OnMovingToyRegistered -= OnMovingToyRegistered;
                ObjectManager.Instance.OnSofaRegistered -= OnSofaRegistered;
            }
            
            // 이벤트 구독 해제
            if (DataManager.Instance != null)
                DataManager.Instance.onWindowSettingsChanged -= OnWindowSettingsChanged;
        }

        // 윈도우 설정 변경 시 호출되는 메서드 (모니터 전환 감지)
        private void OnWindowSettingsChanged(Core.Data.WindowSettings settings)
        {
            // 캐릭터 위치 조정
            Vector3 curPos = transform.position;
            bool needAdjust = false;
            
            // 화면 경계 확인
            if (!Utils.IsWithinScreenBounds(curPos))
            {
                curPos = Utils.ClampToScreenBounds(curPos);
                needAdjust = true;
            }
            
            // 바닥 스냅은 '정말 바닥 상태'에만 적용 (오브젝트 사용/이동/낙하 중이면 건드리지 않음)
            bool isUsingObject = CurTowerSlot.HasValue || CurSofaSlot.HasValue;
            if (!isUsingObject)
            {
                float newY = WindowManager.Instance.GetCanvasBottomY();
                if (Mathf.Abs(curPos.y - newY) > 0.01f)
                {
                    curPos.y = newY;
                    needAdjust = true;
                }
            }
            
            // 위치가 변경된 경우에만 적용
            if (needAdjust)
                transform.position = curPos;
                
            // 현재 상태가 MoveState인 경우 목표물 위치 변경 확인
            if (_curState is MoveState moveState)
            {
                // 캣타워로 가는 중이었다면
                if (moveState.TargetTower != null)
                {
                    var tower = Tower; // 스마트 선택된 최적 캣타워
                    if (tower != null && tower.gameObject.activeSelf)
                    {
                        tower.StartUsing(this);  // 캣타워 사용 상태 유지
                        ChangeState(new MoveState(this, tower, new ClimbState(this, tower)));
                    }
                }
                // 소파로 가는 중이었다면
                else if (moveState.TargetSofa != null)
                {
                    var sofa = Sofa; // 스마트 선택된 최적 소파
                    if (sofa != null && sofa.gameObject.activeSelf)
                    {
                        sofa.StartUsing(this);  // 소파 사용 상태 유지
                        ChangeState(new MoveState(this, sofa, new ClimbState(this, sofa)));
                    }
                }
                // 밥그릇으로 가는 중이었다면
                else if (moveState.TargetBowl != null)
                {
                    if (_foodBowl != null)
                    {
                        // 밥그릇으로 가는 중, EatState/MoveState/WaitForFoodState 분기
                        if (_foodBowl.HasFood()) {
                            int? slot = _foodBowl.CheckUseSlotOnArrival(this);
                            if (slot.HasValue) {
                                float dx = transform.position.x - _foodBowl.GetEatingPos(slot.Value).x;
                                float scaledXOffset = _foodBowl.bowlData.xOffset * DataManager.Instance.GetCurScaleFactor();
                                if ((slot.Value == 0 && dx > -scaledXOffset) || (slot.Value == 1 && dx < scaledXOffset)) 
                                {
                                    ChangeState(new EatState(this, _foodBowl, slot));
                                } 
                                else 
                                {
                                    ChangeState(new MoveState(this, _foodBowl, slot.Value, new EatState(this, _foodBowl, slot)));
                                }
                                return;
                            }
                        }
                        // 슬롯이 없으면 WaitForFoodState
                        ChangeState(new MoveState(this, _foodBowl, new WaitForFoodState(this, _foodBowl, IsStarving())));
                    }
                }
                else
                {
                    // 배회 중이었다면 의사결정 상태로 전환
                    ChangeState(new DecisionState(this));
                }
            }
            
        }

        // 배고픔 시스템 코루틴 - 1초마다 배고픔 감소 체크
        private IEnumerator StatusCo()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f); // 1초마다 체크
                
                // 배부름 감소
                if (_curFullness > 0)
                {
                    _curFullness -= _fullnessRate; // 1초당 감소량
                    if (_curFullness <= 0) _curFullness = 0;
                }
                
                // === 코인 표시 관리 ===
                float elapsed = Time.time - _lastClickRewardTime;
                if (!_isCoinShown)
                {
                    if (elapsed >= CLICK_MAX_INTERVAL && !_isCoinDisplayed)
                    {
                        ShowCoin(true);
                        _coinDisplayStartTime = Time.time;
                    }
                    
                    // 코인 표시 중일 때 5초 타이머 체크
                    if (_isCoinDisplayed && Time.time - _coinDisplayStartTime >= COIN_DISPLAY_DURATION)
                    {
                        ShowCoin(false);
                        _isCoinShown = true;
                    }
                }
            }
        }

        // ObjectManager에서 FoodBowl이 등록되었을 때 호출되는 메서드
        private void OnFoodBowlRegistered(FoodBowl bowl)
        {
            _foodBowl = bowl;
            ObjectManager.Instance.OnFoodBowlRegistered -= OnFoodBowlRegistered;
            Debug.Log("[CharacterController] FoodBowl이 등록되었습니다.");
        }

        // ObjectManager에서 Tower가 등록되었을 때 호출되는 메서드
        private void OnTowerRegistered(Tower tower)
        {
            // 스마트 캣타워 선택: 이제 동적으로 최적 캣타워를 선택함
            ObjectManager.Instance.OnTowerRegistered -= OnTowerRegistered;
            Debug.Log("[CharacterController] Tower가 등록되었습니다. 스마트 선택 활성화됨.");
        }

        private void OnPlacedToyRegistered(PlacedToy toy)
        {
            _placedToy = toy;
            ObjectManager.Instance.OnPlacedToyRegistered -= OnPlacedToyRegistered;
            Debug.Log("[CharacterController] PlacedToy가 등록되었습니다.");
        }

        private void OnMovingToyRegistered(MovingToy toy)
        {
            _movingToy = toy;
            ObjectManager.Instance.OnMovingToyRegistered -= OnMovingToyRegistered;
            Debug.Log("[CharacterController] MovingToy가 등록되었습니다.");
        }

        private void OnSofaRegistered(Sofa sofa)
        {
            // 스마트 소파 선택: 이제 동적으로 최적 소파를 선택함
            ObjectManager.Instance.OnSofaRegistered -= OnSofaRegistered;
            Debug.Log("[CharacterController] Sofa가 등록되었습니다. 스마트 선택 활성화됨.");
        }

        public void ChangeState(StateBase newState)
        {
            Debug.Log($"[CharacterController] {characterData.characterName}: 상태 변경 - {_curState?.GetType().Name} → {newState.GetType().Name}");
            _curState?.Exit();
            _curState = newState;
            _curState?.Enter();
        }

        #region Animation
        private const float DEFAULT_MIX_DURATION = 0.2f;
        private const float DEFAULT_TIME_SCALE = 1f;
        
        private void PlayAnimation(SkeletonDataAsset asset, string animName, string skinName, bool loop = true, float timeScale = DEFAULT_TIME_SCALE, float mixDuration = DEFAULT_MIX_DURATION)
        {
            if (_skeletonAnimation == null || asset == null || string.IsNullOrEmpty(animName))
                return;

            // 에셋이 바뀐 경우에만 초기화 + 한 번만 잡을 옵션
            if (!_skeletonAnimation.valid || _skeletonAnimation.skeletonDataAsset != asset)
            {
                _skeletonAnimation.skeletonDataAsset = asset;
                _skeletonAnimation.Initialize(true);

                _skeletonAnimation.fixDrawOrder = true;

                if (_skeletonAnimation.maskInteraction != SpriteMaskInteraction.VisibleOutsideMask)
                    _skeletonAnimation.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            }

            // 스킨: 존재하고, 실제로 달라질 때만 변경
            if (!string.IsNullOrEmpty(skinName))
            {
                var data   = _skeletonAnimation.Skeleton.Data;
                var newSkin = data.FindSkin(skinName);
                if (newSkin != null && _skeletonAnimation.Skeleton.Skin != newSkin)
                    _skeletonAnimation.Skeleton.SetSkin(newSkin);
            }

            _skeletonAnimation.Skeleton.SetSlotsToSetupPose();

            var state   = _skeletonAnimation.AnimationState;
            var current = state.GetCurrent(0);
            if (current != null && current.Animation != null &&
                current.Animation.Name == animName && current.Loop == loop)
            {
                current.TimeScale = timeScale;
                return;
            }
            var track = _skeletonAnimation.AnimationState.SetAnimation(0, animName, loop);
            track.TimeScale = timeScale;
            track.MixDuration = mixDuration;
        }
        
        // 방향 설정 (좌, 우)
        public void SetDirection(float? direction = null)   // 오른쪽 1, 왼쪽 -1, null이면 랜덤
        {
            float directionValue = direction ?? (Random.value < 0.5f ? -1f : 1f);
            _skeletonAnimation.Skeleton.ScaleX = directionValue;
        }

        #endregion


        #region Sorting Order
        // 슬롯 기반 sorting order 업데이트

        #endregion

        #region Move
        public void MoveTowards(Vector3 target, float speed)
        {
            Vector3 direction = (target - transform.position).normalized;
            Vector3 newPosition = transform.position + direction * speed * Time.deltaTime;
            
            // Canvas 바닥 높이로 Y 좌표 고정
            newPosition.y = WindowManager.Instance.GetCanvasBottomY();
            
            transform.position = newPosition;
        }
        #endregion
        
        public void Feed()
        {
            _curFullness = 100f;
        }
        
        // 배부름 관련 메서드들
        public bool IsHungry()
        {
            return _curFullness <= _hungerThreshold;
        }
        
        public bool IsStarving()
        {
            return _curFullness <= _starvingThreshold;
        }

        #region Click
        private void HandleClick()
        {
            if (_curState is ToolState)
                return;
            
            _isDragging = false;
            
            // AlarmState일 때는 알람 해제 처리
            if (_curState is AlarmState alarmState)
            {
                alarmState.OnAlarmClick();
                return;
            }
            
            // SofaState일 때는 WakeUp 처리
            if (_curState is SofaState sofaState)
            {
                sofaState.WakeUpClick();
                return;
            }
            
            // InteractionState에서 드래그 중이거나 클릭 중이거나 낙하 중이면 무시
            if (_curState is InteractionState interactionState)
            {
                if (interactionState.IsDragging || interactionState.IsClick || interactionState.IsFalling)
                {
                    return;
                }
            }
            

            // 클릭 시 골드 지급 + 시각 효과
            if (GameManager.Instance != null)
            {
                int reward = ClickReward(characterData.clickCoin);
                if (reward > 0)
                {
                    GameManager.Instance.SpawnCoin(transform.position + Vector3.up, reward);
                    _lastClickRewardTime = Time.time;
                    ShowCoin(false); // 클릭 시 즉시 코인 지시자 숨김
                    _isCoinShown = false;
                    ChangeState(new InteractionState(this, false, true));
                }
                else
                {
                    ChangeState(new InteractionState(this, false, true));
                }
            }

            SoundManager.Instance.PlayCharacterMeow(characterData);
        }

        // 클릭 보상 계산


        private int ClickReward(int maxClickCoin)
        {
            float elapsed = Time.time - _lastClickRewardTime; 

            // 30초 전에는 보상 X
            if (elapsed < CLICK_MIN_INTERVAL)
                return 0;

            // 120초 이상이면 최대 보상 지급
            if (elapsed >= CLICK_MAX_INTERVAL)
                return maxClickCoin;

            // 30~120초 구간에서 선형으로 분배, 100단위 내림
            float t = (elapsed - CLICK_MIN_INTERVAL) / (CLICK_MAX_INTERVAL - CLICK_MIN_INTERVAL);
            int raw = Mathf.FloorToInt(maxClickCoin * t);

            if (raw <= 0) return 0;

            return (raw / 100) * 100;
        }

        private void ShowCoin(bool show)
        {
            if (coinObj != null)
            {
                coinObj.SetActive(show);
                _isCoinDisplayed = show;
            }
            if (show)
            {
                SoundManager.Instance.PlayCharacterMeow(characterData);
            }
        }


        #endregion

        #region IClickable
        public void OnClick()
        {
            HandleClick();
        }

        public void OnDragStart()
        {
            // InteractionState에서 드래그, 클릭 상태면 무시
            if (_curState is InteractionState interactionState )
            {
                if (interactionState.IsDragging || interactionState.IsClick)
                {
                    return;
                }
            }
            if (_curState is ToolState)
                return;

            // 드래그 시작하면 모든 슬롯 해제
            if (_curTower != null && CurTowerSlot.HasValue)
            {
                _curTower.ReleaseSlot(this);
            }
            if (_curSofa != null && CurSofaSlot.HasValue)
            {
                _curSofa.ReleaseSlot(this);
            }
            if (_foodBowl != null)
            {
                _foodBowl.ReleaseSlot(this);
            }

            _isDragging = true;
            
            // 이미 InteractionState면 상태만 변경, 아니면 새로 생성
            if (_curState is InteractionState currentInteraction)
            {
                currentInteraction.SetDragState(true);
            }
            else
            {
                ChangeState(new InteractionState(this, true));
            }
        }

        public void OnDragEnd()
        {
            if (!_isDragging) return;
            _isDragging = false;

            if (_curState is InteractionState interactionState)
            {
                interactionState.SetDragState(false);
            }
            else
            {
                ChangeState(new InteractionState(this, false));
            }
        }
        
        #endregion

        #region IPointerEnterHandler, IPointerExitHandler
        /// <summary>
        /// 마우스가 캐릭터 위로 진입할 때
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            // 캐릭터 위에 마우스가 있을 때만 파일 드롭 허용
            var windowController = Kirurobo.UniWindowController.current;
            if (windowController != null)
            {
                windowController.allowDropFiles = true;
            }
        }

        /// <summary>
        /// 마우스가 캐릭터에서 이탈할 때
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            // 캐릭터에서 마우스가 벗어나면 파일 드롭 비활성화
            var windowController = Kirurobo.UniWindowController.current;
            if (windowController != null)
            {
                windowController.allowDropFiles = false;
            }
        }
        #endregion

    }
}