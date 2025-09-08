using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Core.Managers;

namespace UI.Panels
{
    /// <summary>
    /// 모든 UI 패널의 베이스 클래스
    /// MainPanel: 마우스 호버링으로만 제어 (ESC 무시)
    /// 다른 Panel: PopupManager 통해 관리 (ESC로 닫기 가능)
    /// </summary>
    public abstract class BasePanel : MonoBehaviour
    {
        [Header("=== 공통 버튼 ===")]
        [SerializeField] 
        protected Button backButton;
        
        [SerializeField] 
        protected BasePanel mainPanel;
        
        // 패널이 현재 열려있는지 여부
        public bool IsOpen { get; private set; }
        
        protected RectTransform panelRectTransform;
        
        protected virtual void Awake()
        {
            panelRectTransform = GetComponent<RectTransform>();
            
            if (backButton != null)
            {
                backButton.onClick.AddListener(ReturnMainPanel);
            }
            
            if (mainPanel == null)
            {
                FindMainPanel();
            }
        
            // 시작 시 패널을 비활성화
            IsOpen = false;
        }
        
        protected virtual void Start()
        {
            // MainPanel이 아닌 경우에만 InputManager에 등록
            if (this != mainPanel && InputManager.Instance != null)
            {
                InputManager.Instance.AddPanel(this);
            }
        }
        
        protected virtual void OnDestroy()
        {
            // MainPanel이 아닌 경우에만 InputManager에서 제거
            if (this != mainPanel && InputManager.Instance != null)
            {
                InputManager.Instance.RemovePanel(this);
            }
        }
        
        #region PopupManager 연동 메서드 (MainPanel 제외)
        
        /// <summary>
        /// PopupManager에서 호출하는 표시 메서드
        /// MainPanel은 직접 호출됨
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            IsOpen = true;
            OnPanelOpened();
        }
        
        /// <summary>
        /// PopupManager에서 호출하는 숨김 메서드  
        /// MainPanel은 직접 호출됨
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            IsOpen = false;
            OnPanelClosed();
        }
        
        /// <summary>
        /// 패널 열기
        /// MainPanel: 직접 Show() 호출
        /// 다른 Panel: PopupManager에 위임
        /// </summary>
        public virtual void Open()
        {
            if (IsOpen) return;
            
            if (this == mainPanel)
            {
                // MainPanel은 직접 표시
                Show();
            }
            else
            {
                // 다른 Panel은 PopupManager에 위임
                if (PopupManager.Instance != null)
                {
                    PopupManager.Instance.ShowPopup(this);
                }
            }
        }
        
        /// <summary>
        /// 패널 닫기
        /// MainPanel: 직접 Hide() 호출  
        /// 다른 Panel: PopupManager에 위임
        /// </summary>
        public virtual void Close()
        {
            if (!IsOpen) return;
            
            if (this == mainPanel)
            {
                // MainPanel은 직접 숨김
                Hide();
            }
            else
            {
                // 다른 Panel은 PopupManager에 위임
                if (PopupManager.Instance != null)
                {
                    PopupManager.Instance.ClosePopup(this.GetType());
                }
            }
        }
        
        /// <summary>
        /// 패널 토글 (열려있으면 닫기, 닫혀있으면 열기)
        /// </summary>
        public virtual void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }
        
        #endregion
        
        #region 가상 메서드들 (하위 클래스에서 구현)
        
        protected virtual void OnPanelOpened()
        {
            // 하위 클래스에서 필요에 따라 구현
        }
        
        protected virtual void OnPanelClosed()
        {
            // 하위 클래스에서 필요에 따라 구현
        }
        
        protected virtual void ReturnMainPanel()
        {
            // 현재 패널 닫기
            Close();
            
            // 메인패널도 닫기 (MainPanel은 Close에서 Paw 애니메이션 실행됨)
            if (mainPanel != null && mainPanel.IsOpen)
            {
                mainPanel.Close();
            }
        }
        
        #endregion
        
        #region Private 메서드들
        
        private void FindMainPanel()
        {
            // MainPanel 타입의 오브젝트를 찾기 (UI.Panels 네임스페이스)
            var foundMainPanels = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            
            foreach (var component in foundMainPanels)
            {
                if (component.GetType().Name == "MainPanel")
                {
                    mainPanel = component as BasePanel;
                    if (mainPanel != null)
                    {
                        return;
                    }
                }
            }
        }
        
        #endregion
    }
} 