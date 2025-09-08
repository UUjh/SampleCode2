using UnityEngine;
using UnityEngine.UI;
using System;

namespace UI
{
    /// <summary>
    /// 공통 UI 베이스
    /// - 패널/팝업 타입을 통합 처리하고 공통 버튼/콜백을 제공
    /// - Show/Hide/Toggle/위치 지정 등 표준 인터페이스 제공
    /// </summary>
    public enum UIType
    {
        Panel,      // 일반 패널 (토글 가능)
        Popup,      // 팝업 (OK/YesNo 버튼)
    }
    
    public enum PopupButtonType
    {
        OK,         // OK 버튼만
        YesNo,      // Yes/No 버튼
    }
    
    public class BaseUI : MonoBehaviour
    {
        [Header("=== UI 설정 ===")]
        [SerializeField] protected UIType uiType = UIType.Panel;
        [SerializeField] protected PopupButtonType popupButtonType = PopupButtonType.OK;
        
        [Header("=== 공통 버튼 ===")]
        [SerializeField] protected Button closeBtn; // 공통 닫기 버튼
        [SerializeField] protected Button okBtn;    // OK 버튼
        [SerializeField] protected Button yesBtn;   // Yes 버튼  
        [SerializeField] protected Button noBtn;    // No 버튼
        
        // 현재 상태
        public bool IsOpen => gameObject.activeInHierarchy;
        public UIType Type => uiType;
        public PopupButtonType ButtonType => popupButtonType;
        
        // 이벤트
        public event Action OnShown;
        public event Action OnHidden;
        
        // 팝업 콜백들 (기존 BasePopup 기능 유지!)
        public Action OnOkClicked;
        public Action OnYesClicked; 
        public Action OnNoClicked;
        
        protected virtual void Awake()
        {
            // 공통 닫기 버튼
            if (closeBtn != null)
                closeBtn.onClick.AddListener(Hide);
                
            // 팝업 타입일 때 버튼 설정
            if (uiType == UIType.Popup)
            {
                SetupPopupButtons();
            }
        }
        
        #region 핵심 메서드
        
        public virtual void Show()
        {
            if (IsOpen) return;
            
            gameObject.SetActive(true);
            OnShown?.Invoke();
            OnUIShown();
        }
        
        public virtual void Hide()
        {
            if (!IsOpen) return;
            
            gameObject.SetActive(false);
            OnHidden?.Invoke();
            OnUIHidden();
            
            // PopupManager의 스택에서도 제거
            if (uiType == UIType.Popup)
            {
                var popupManager = Core.Managers.PopupManager.Instance;
                if (popupManager != null)
                {
                    popupManager.ClosePopup(this.GetType());
                }
            }
        }
        
        public virtual void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }
        
        public virtual void ShowAtPosition(Vector3 position)
        {
            var rectTransform = transform as RectTransform;
            if (rectTransform != null)
                rectTransform.anchoredPosition = position;
            Show();
        }
        
        // 기존 BasePopup 호환 메서드들
        public void Open() => Show();
        public void Close() => Hide();
        
        public void OpenAtPosition(Vector3 position) => ShowAtPosition(position);
        
        public void OpenWithOkCallback(Action onOkCallback)
        {
            OnOkClicked = onOkCallback;
            Show();
        }
        
        public void OpenWithYesNoCallback(Action onYesCallback, Action onNoCallback = null)
        {
            OnYesClicked = onYesCallback;
            OnNoClicked = onNoCallback;
            Show();
        }
        
        #endregion
        
        #region Private 메서드
        
        private void SetupPopupButtons()
        {
            switch (popupButtonType)
            {
                case PopupButtonType.OK:
                    if (okBtn != null)
                    {
                        okBtn.onClick.AddListener(() => {
                            OnOkClicked?.Invoke();
                            Hide();
                        });
                    }
                    break;
                    
                case PopupButtonType.YesNo:
                    if (yesBtn != null)
                    {
                        yesBtn.onClick.AddListener(() => {
                            OnYesClicked?.Invoke();
                            Hide();
                        });
                    }
                    
                    if (noBtn != null)
                    {
                        noBtn.onClick.AddListener(() => {
                            OnNoClicked?.Invoke();
                            Hide();
                        });
                    }
                    break;
            }
        }
        
        #endregion
        
        #region 가상 메서드
        
        protected virtual void OnUIShown() { }
        protected virtual void OnUIHidden() { }
        
        #endregion
    }
} 