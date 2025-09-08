using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using Core.Managers;
using Kirurobo;
using UnityEngine.EventSystems;
using Character.States;
using Character.Controller;
using UI;

/// <summary>
/// 도구 패널
/// - 자주 쓰는 프로그램 실행(메모장/그림판/계산기/즐겨찾기)
/// - 즐겨찾기 EXE 선택/저장/실행, 우클릭으로 즐겨찾기 리셋
/// - 패널 열림/닫힘에 따라 캐릭터 상태 전환 연동
/// </summary>
public class ToolPanel : BaseUI
{
    [Header("UI Components")]
    [SerializeField] private Button _notepadBtn;
    [SerializeField] private Button _paintBtn;
    [SerializeField] private Button _calculatorBtn;
    [SerializeField] private Button _favoriteBtn;

    [Header("Favorite Button Sprites")]
    [SerializeField] private Sprite _selectFavoriteSprite;
    [SerializeField] private Sprite _favoriteSprite;

    [SerializeField] private Canvas _canvas;
    
    private CharacterController _character;
    
    protected override void Awake()
    {
        // UI 타입을 Panel로 설정
        uiType = UIType.Panel;
        base.Awake();
    }
    
    void Start()
    {
        if (_canvas == null)
        {
            _canvas = GetComponentInParent<Canvas>();
        }
        
        // 버튼 이벤트 연결
        _notepadBtn.onClick.AddListener(() => OpenProgram("notepad.exe"));
        _paintBtn.onClick.AddListener(() => OpenProgram("mspaint.exe"));
        _calculatorBtn.onClick.AddListener(() => OpenProgram("calc.exe"));
        _favoriteBtn.onClick.AddListener(OpenFavorite);
        
        // 우클릭 이벤트 추가
        var favoriteEventTrigger = _favoriteBtn.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerClick;
        entry.callback.AddListener((data) => {
            var pointerData = (PointerEventData)data;
            if (pointerData.button == PointerEventData.InputButton.Right)
            {
                ResetFavorite();    
            }
        });
        favoriteEventTrigger.triggers.Add(entry);

        // 초기 이미지 설정
        UpdateFavoriteButtonSprite();
    }
    
    // BaseUI의 Hide를 오버라이드해서 캐릭터 상태 처리 추가
    public override void Hide()
    {
        base.Hide();
        
        if (_character != null)
        {
            if (_character.CurTowerSlot.HasValue)
                _character.ChangeState(new TowerDecisionState(_character));
            else if (_character.CurSofaSlot.HasValue)
                _character.ChangeState(new SofaState(_character));
            else
                _character.ChangeState(new DecisionState(_character));
                
            _character = null;
        }
    }
    
    // 기존 공개 메서드들 (하위 호환성)
    public void SetPosition(Vector3 pos, CharacterController character)
    {
        _character = character;
        Vector3 panelPos = pos;
        transform.position = panelPos;
        
        // PopupManager에 등록하여 ESC로 닫을 수 있게 함!
        if (PopupManager.Instance != null)
        {
            PopupManager.Instance.ShowPopup(this);
        }
        else
        {
            Show(); // fallback
        }

        _character.ChangeState(new ToolState(_character));
    }
    
    #region Private 메서드들
    
    private void UpdateFavoriteButtonSprite()
    {
        if (_favoriteBtn != null)
        {
            var image = _favoriteBtn.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = string.IsNullOrEmpty(DataManager.Instance.FavoriteExePath) 
                    ? _selectFavoriteSprite 
                    : _favoriteSprite;
            }
        }
    }

    private void OpenProgram(string programName)
    {
        // PopupManager를 통해 일관되게 닫기 (상태 동기화)
        if (PopupManager.Instance != null)
        {
            PopupManager.Instance.ClosePopup<ToolPanel>();
        }
        else
        {
            Close(); // fallback
        }
        
        try
        {
            System.Diagnostics.Process.Start(programName);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to open {programName}: {e.Message}");
        }
    }

    private void OpenFavorite()
    {
        // PopupManager를 통해 일관되게 닫기 (상태 동기화)
        if (PopupManager.Instance != null)
        {
            PopupManager.Instance.ClosePopup<ToolPanel>();
        }
        else
        {
            Close(); // fallback
        }

        string path = DataManager.Instance.FavoriteExePath;
        
        if (string.IsNullOrEmpty(path))
        {
            OpenFavoriteExeDialog();
        }
        else if (File.Exists(path))
        {
            try
            {
                System.Diagnostics.Process.Start(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to open favorite program: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("Favorite program path does not exist. Opening file dialog...");
            OpenFavoriteExeDialog();
        }
    }

    private void OpenFavoriteExeDialog()
    {
        FilePanel.Settings settings = new FilePanel.Settings();
        settings.filters = new FilePanel.Filter[]
        {
            new FilePanel.Filter("Executable Files", "exe"),
            new FilePanel.Filter("All files", "*")
        };
        settings.title = "Select Favorite Program";
        settings.flags = FilePanel.Flag.FileMustExist;

        FilePanel.OpenFilePanel(settings, (files) =>
        {
            if (files != null && files.Length > 0)
            {
                DataManager.Instance.FavoriteExePath = files[0];
                UpdateFavoriteButtonSprite();
                
                try
                {
                    System.Diagnostics.Process.Start(files[0]);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to start selected program: {e.Message}");
                }
            }
        });
    }

    private void ResetFavorite()
    {
        DataManager.Instance.FavoriteExePath = "";
        UpdateFavoriteButtonSprite();
        Debug.Log("Favorite program reset.");
    }
    
    #endregion
}
