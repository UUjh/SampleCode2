using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Core.Base;
using Objects;
using Objects.Data;
using Character.States;

namespace Core.Managers
{
    /// <summary>
    /// 게임 전반 매니저
    /// - 자동 골드 지급(UI 게이지 트윈 포함)
    /// - 타이틀 페이드 연출(DOTween)
    /// - 코인 스폰 및 간단한 장난감 선택/해제 흐름
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        [Header("=== Auto Gold ===")]
        [SerializeField] private int _autoGoldAmount = 250;
        [SerializeField] private float _goldInterval = 30f; // 30초마다
        
        [Header("=== Auto Gold UI ===")]
        public Image _coinGauge;
        [SerializeField] private GameObject _coinPrefab;
        [SerializeField] private Transform _pawTransform;
        [SerializeField] private UI.Panels.MainPanel _mainPanel;
        private Tween _goldTween;
        
        [Header("=== Game Title Fade ===")]
        [SerializeField] private GameObject _titleObj;
        [SerializeField] private Image _titleBGImg;
        [SerializeField] private Image[] _titleWordsImgs;
        [SerializeField] private Image _titleCharacterImg;
        [SerializeField] private Image _titlePawImg;
        [SerializeField] private float _fadeDuration = 1f;
        private Sequence _titleSequence;
        
        
        protected override void Awake()
        {
            base.Awake();
            
            // 자동 골드 시스템 시작
            StartGoldGaugeTween();
            
            // 게임 타이틀 페이드 시작
            StartGameTitleFade();
        }
        
        protected void Start()
        {
            // 모든 매니저 초기화 완료 후 Steam 상태 최종 체크
            if (SteamManager.Instance != null)
            {
                SteamManager.Instance.ChkSteam();
                Debug.Log("[GameManager] Steam 연결 상태 최종 확인 완료");
            }
        }
        
        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
        
        #region Auto Gold System
        
        private void GiveGoldReward()
        {
            if (DataManager.Instance != null)
            {
                SpawnGoldCoin();
            }
        }
        
        #endregion
        
        #region Game Title Fade System
        
        
        #endregion
        
        #region Auto Gold UI System
        
        private void StartGoldGaugeTween()
        {
            if (_coinGauge == null) return;

            // 기존 트윈이 있으면 제거
            _goldTween?.Kill();

            _coinGauge.fillAmount = 0f;
            _goldTween = DOTween.To(() => _coinGauge.fillAmount,
                                     x => _coinGauge.fillAmount = x,
                                     1f,
                                     _goldInterval)
                                   .SetEase(Ease.Linear)
                                   .OnComplete(() =>
                                   {
                                       GiveGoldReward();
                                       StartGoldGaugeTween(); // 반복
                                   });
        }
        
        private void SpawnGoldCoin()
        {
            if (_coinPrefab == null) return;
            
            Vector3 spawnPos = _pawTransform != null ? _pawTransform.position + Vector3.up * 0.8f + Vector3.left * 0.4f : Vector3.zero;
            SpawnCoin(spawnPos, _autoGoldAmount);
        }
        
        #endregion
        
        #region Coin 
        public void SpawnCoin(Vector3 position, int amount)
        {
            if (_coinPrefab == null) return;
            
            if (DataManager.Instance != null)
            {
                DataManager.Instance.AddCoins(amount);
            }
                        
            // MainPanel이 열려있으면 스폰하지 않음
            if (_mainPanel != null && _mainPanel.IsOpen)
            {
                return;
            }

            GameObject coinObj = Instantiate(_coinPrefab);
            Coin coin = coinObj.GetComponent<Coin>();
            
            if (coin != null)
            {
                coin.InitCoin(position, amount);
            }
            else
            {
                Destroy(coinObj);
            }
        }
        
        #endregion
        
    }
} 