using UnityEngine;
using Core.Base;
using Core.Data;
using System;
using System.Text;
using System.Collections.Generic;
using Objects.Data;
using Character.Data;

namespace Core.Managers
{
    /// <summary>
    /// 게임 전체 데이터를 관리하는 중앙 매니저
    /// - 사용자 게임 데이터 (코인, 구매 기록 등)
    /// - 설정 데이터 (윈도우, 사운드 등)  
    /// - 투두리스트 데이터
    /// - 오브젝트 위치 데이터
    /// </summary>
    [DefaultExecutionOrder(-200)] // 가장 먼저 실행 (데이터 로드)
    public class DataManager : Singleton<DataManager>
    {

        #region Private Fields
        
        private SettingData _settingData;
        private UserData _userData;
        private const string USER_DATA_KEY;
        private const string SETTINGS_DATA_KEY;
        
        #endregion
        
        #region Properties
        public int Language => _settingData?.language ?? 0;
        
        public void SetLanguage(int languageIndex)
        {
            if (_settingData != null)
            {
                _settingData.language = languageIndex;
                SaveSettings();
            }
        }
        
        #endregion
        
        #region Events
        
        public event Action<WindowSettings> onWindowSettingsChanged;
        public event Action<SoundSettings> onSoundSettingsChanged;
        public event Action<CharacterSettings> onCharacterSettingsChanged;
        public event Action<int> OnCoinsChanged;
        public event Action OnCharacterChanged;
        
        #endregion
        
        #region Unity
        
        protected override void Awake()
        {
            base.Awake();
            LoadUserData();
            LoadSettings();
        }
        
        #endregion
        
        #region Settings Management (윈도우, 사운드, 스케일 설정)
        
        public static readonly float[] ScaleFactors = { 0.5f, 0.6f, 0.8f, 1.0f, 1.2f };
        public float GetCurScaleFactor()
        {
            int step = WindowSettings.scaleStep;
            if (step < 0 || step >= ScaleFactors.Length)
                step = 3; // 기본값
            return ScaleFactors[step];
        }
        
        public string FavoriteExePath
        {
            get => _settingData.favoriteExePath;
            set
            {
                _settingData.favoriteExePath = value;
                SaveSettings();
            }
        }
        
        public void UpdateWindowSettings(WindowSettings newSettings)
        {
            _settingData.windowSettings = newSettings;
            SaveSettings();
            onWindowSettingsChanged?.Invoke(newSettings);
        }

        public void UpdateSoundSettings(SoundSettings newSettings)
        {
            _settingData.soundSettings = newSettings;
            SaveSettings();
            onSoundSettingsChanged?.Invoke(newSettings);
        }

        public void UpdateCharacterSettings(CharacterSettings newSettings)
        {
            _settingData.characterSettings = newSettings;
            SaveSettings();
            onCharacterSettingsChanged?.Invoke(newSettings);
        }
        #endregion
        
        #region First Run
        
        #endregion
        
        #region Coin Management (코인 관리)
        
        public int Coins => _userData?.coins ?? 0;

        public void AddCoins(int amount)
        {
            if (_userData == null) return;
            
            _userData.coins += amount;
            _userData.totalCoinsEarned += amount;
            SaveUserData();
            OnCoinsChanged?.Invoke(_userData.coins);
        }

        public bool SpendCoins(int amount)
        {
            if (_userData == null || _userData.coins < amount) return false;
            
            _userData.coins -= amount;
            _userData.totalCoinsSpent += amount;
            SaveUserData();
            OnCoinsChanged?.Invoke(_userData.coins);
            return true;
        }
        
        #endregion
        

        #region Feed Management (밥 관리)
        

        #endregion
        
        #region Data Persistence (데이터 저장/로드)
        
        private void LoadSettings()
        {
            try
            {
                if (PlayerPrefs.HasKey(SETTINGS_DATA_KEY))
                {
                    string json = PlayerPrefs.GetString(SETTINGS_DATA_KEY);
                    _settingData = JsonUtility.FromJson<SettingData>(json);
                }
                else
                {
                    _settingData = new SettingData();
                    SaveSettings();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"설정 데이터 로드 실패, 새로운 데이터 생성: {e.Message}");
                _settingData = new SettingData();
                SaveSettings();
            }
            
            if (_settingData?.windowSettings == null)
                _settingData = new SettingData();
                
            onWindowSettingsChanged?.Invoke(_settingData.windowSettings);
        }

        private void SaveSettings()
        {
            if (_settingData == null) return;
            
            string json = JsonUtility.ToJson(_settingData, true);
            PlayerPrefs.SetString(SETTINGS_DATA_KEY, json);
            PlayerPrefs.Save();
        }

        private void LoadUserData()
        {
            try
            {
                // 1) 스팀 클라우드에서 우선 로드 시도
                if (SteamManager.Instance != null && SteamManager.Instance.IsSteamInitialized())
                {
                    if (SteamManager.Instance.TryLoadUserData(out var cloudBytes))
                    {
                        string cloudJson = Encoding.UTF8.GetString(cloudBytes);
                        if (!string.IsNullOrEmpty(cloudJson))
                        {
                            _userData = JsonUtility.FromJson<UserData>(cloudJson);
                            
                            MigrateCurCharacters();
                            return; // 클라우드 로드 성공 시 종료
                        }
                    }
                }

                if (PlayerPrefs.HasKey(USER_DATA_KEY))
                {
                    string json = PlayerPrefs.GetString(USER_DATA_KEY);
                    _userData = JsonUtility.FromJson<UserData>(json);
                    
                    MigrateCurCharacters();
                    
                }
                else
                {
                    _userData = new UserData();
                    SaveUserData();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"유저 데이터 로드 실패, 새로운 데이터 생성: {e.Message}");
                _userData = new UserData();
                SaveUserData();
            }
        }

        public void SaveUserData()
        {
            if (_userData == null) return;
            
            string json = JsonUtility.ToJson(_userData, true);
            PlayerPrefs.SetString(USER_DATA_KEY, json);
            PlayerPrefs.Save();

            // 스팀 클라우드에도 동기화 (가능 시)
            if (SteamManager.Instance != null && SteamManager.Instance.IsSteamInitialized())
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                SteamManager.Instance.SaveUserData(bytes);
            }
        }
        
        #endregion
        
        #region Object Position System
        
        public Vector3 GetFoodBowlPosition()
        {
            Vector3 position = _userData.foodBowlPosition;
            // Y값은 항상 현재 바닥 높이로 보정
            position.y = WindowManager.Instance.GetCanvasBottomY();
            
            return position;
        }
        
        public void SetFoodBowlPosition(Vector3 position)
        {
            // Y값은 항상 바닥 높이로 고정
            position.y = WindowManager.Instance.GetCanvasBottomY();
            _userData.foodBowlPosition = position;
            SaveUserData();
        }
        
        // 타워 위치 가져오기
        public Vector3 GetTowerPosition(int instanceId)
        {
            if (instanceId < 0) 
            {
                Debug.Log($"[DataManager] GetTowerPosition - 잘못된 ID: {instanceId}");
                return Vector3.zero;
            }
            
            // 범위 초과시 Vector3.zero 반환
            if (instanceId >= _userData.towerPositions.Count)
            {
                Debug.Log($"[DataManager] 타워 위치 없음 - ID: {instanceId} (범위 초과)");
                return Vector3.zero;
            }
            
            Vector3 position = _userData.towerPositions[instanceId];
            if (position != Vector3.zero)
            {
                // Y값은 항상 현재 바닥 높이로 보정
                position.y = WindowManager.Instance.GetCanvasBottomY();
                Debug.Log($"[DataManager] 타워 위치 발견 - ID: {instanceId}, 위치: {position}");
            }
            else
            {
                Debug.Log($"[DataManager] 타워 위치 없음 - ID: {instanceId}");
            }
            
            return position;
        }
        
        // 타워 위치 저장
        public void SetTowerPosition(int instanceId, Vector3 position)
        {
            if (instanceId < 0) 
            {
                Debug.Log($"[DataManager] SetTowerPosition - 잘못된 ID: {instanceId}");
                return;
            }
            
            // Y값은 항상 바닥 높이로 고정, Z값은 타워용으로 고정
            position.y = WindowManager.Instance.GetCanvasBottomY();
            position.z = 2f; // 타워는 항상 z=2
            
            // List 크기가 부족하면 자동 확장
            while (instanceId >= _userData.towerPositions.Count)
            {
                _userData.towerPositions.Add(Vector3.zero);
            }
            
            // 인덱스에 직접 저장 
            _userData.towerPositions[instanceId] = position;
            Debug.Log($"[DataManager] 타워 위치 저장 - ID: {instanceId}, 위치: {position}");
            
            SaveUserData();
        }

        // 소파 위치 가져오기
        public Vector3 GetSofaPosition(int instanceId)
        {
            if (instanceId < 0) return new Vector3(0, 0, 1);
            
            // 범위 초과시 Vector3.zero 반환 (확장성 고려)
            if (instanceId >= _userData.sofaPositions.Count)
                return new Vector3(0, 0, 1);
            
            Vector3 position = _userData.sofaPositions[instanceId];
            if (position != Vector3.zero)
            {
                // Y값은 항상 현재 바닥 높이로 보정
                position.y = WindowManager.Instance.GetCanvasBottomY();
            }

            return position;
        }
        
        // 소파 위치 저장
        public void SetSofaPosition(int instanceId, Vector3 position)
        {
            if (instanceId < 0) 
            {
                Debug.Log($"[DataManager] SetSofaPosition - 잘못된 ID: {instanceId}");
                return;
            }
            
            // Y값은 항상 바닥 높이로 고정, Z값은 소파용으로 고정
            position.y = WindowManager.Instance.GetCanvasBottomY();
            position.z = 1f; // 소파는 항상 z=1
            
            // List 크기가 부족하면 자동 확장
            while (instanceId >= _userData.sofaPositions.Count)
            {
                _userData.sofaPositions.Add(Vector3.zero);
            }
            
            // 인덱스에 직접 저장 
            _userData.sofaPositions[instanceId] = position;
            Debug.Log($"[DataManager] 소파 위치 저장 - ID: {instanceId}, 위치: {position}");
            
            SaveUserData();
        }
        
        // 배치된 가구 관리
        public void AddPlacedSofa(int sofaId)
        {
            if (sofaId >= 0 && !_userData.placedSofaIds.Contains(sofaId))
            {
                _userData.placedSofaIds.Add(sofaId);
                SaveUserData();
            }
        }
        
        public void RemovePlacedSofa(int sofaId)
        {
            if (_userData.placedSofaIds.Contains(sofaId))
            {
                _userData.placedSofaIds.Remove(sofaId);
                
                // 해당 인덱스 위치를 Vector3.zero로 초기화
                if (sofaId >= 0 && sofaId < _userData.sofaPositions.Count)
                {
                    _userData.sofaPositions[sofaId] = Vector3.zero;
                    Debug.Log($"[DataManager] 소파 위치 데이터 초기화 - ID: {sofaId}");
                }
                
                SaveUserData();
            }
        }
        
        public void AddPlacedTower(int towerId)
        {
            if (towerId >= 0 && !_userData.placedTowerIds.Contains(towerId))
            {
                _userData.placedTowerIds.Add(towerId);
                SaveUserData();
            }
        }
        
        public void RemovePlacedTower(int towerId)
        {
            if (_userData.placedTowerIds.Contains(towerId))
            {
                _userData.placedTowerIds.Remove(towerId);
                
                // 해당 인덱스 위치를 Vector3.zero로 초기화
                if (towerId >= 0 && towerId < _userData.towerPositions.Count)
                {
                    _userData.towerPositions[towerId] = Vector3.zero;
                    Debug.Log($"[DataManager] 타워 위치 데이터 초기화 - ID: {towerId}");
                }
                
                SaveUserData();
            }
        }
        
        #endregion
        
        #region Data Resources
        
        [Header("=== 데이터 리소스 ===")]
        
        #region Purchase System
        
        /// <summary>
        /// 아이템 타입을 구분하는 열거형
        /// </summary>
        public enum ItemType
        {
            Bowl,
            Tower,
            Toy,
            Character,
            Sofa,
            License,
        }
        
        /// <summary>
        /// 통합된 구매 확인 메소드
        /// </summary>
        private bool IsItemPurchased(ItemType itemType, int itemId)
        {
            return itemType switch
            {
                ItemType.Bowl => _userData.purchasedBowlIds.Contains(itemId),
                ItemType.Tower => _userData.purchasedTowerIds.Contains(itemId),
                ItemType.Toy => _userData.purchasedToyIds.Contains(itemId),
                ItemType.Character => _userData.purchasedCharacterIds.Contains(itemId),
                ItemType.Sofa => _userData.purchasedSofaIds.Contains(itemId),
                ItemType.License => _userData.purchasedLicenseIds.Contains(itemId),
                _ => false
            };
        }
        
        /// <summary>
        /// 통합된 구매 처리 메소드 (기본 로직)
        /// </summary>
        private bool PurchaseItemInternal(ItemType itemType, int itemId, int cost, Action onSuccess = null)
        {
            if (IsItemPurchased(itemType, itemId)) return false;
            if (!SpendCoins(cost)) return false;

            switch (itemType)
            {
                case ItemType.Bowl:
                    _userData.purchasedBowlIds.Add(itemId);
                    _userData.curBowlId = itemId; // 구매 즉시 선택
                    break;

                case ItemType.Tower:
                    _userData.purchasedTowerIds.Add(itemId);
                    // 다중 가구 시스템: 구매 즉시 선택하지 않음
                    break;

                case ItemType.Toy:
                    _userData.purchasedToyIds.Add(itemId);
                    if (itemId == 2)
                    {
                        _userData.curPlacedToyId = itemId;
                    }
                    else
                    {
                        OnToyChanged?.Invoke();
                    }
                    break;

                case ItemType.Character:
                    _userData.purchasedCharacterIds.Add(itemId);
                    break;

                case ItemType.Sofa:
                    _userData.purchasedSofaIds.Add(itemId);
                    // 다중 가구 시스템: 구매 즉시 선택하지 않음
                    break;

                case ItemType.License:
                    _userData.purchasedLicenseIds.Add(itemId);
                    _userData.curLicenseId = itemId;
                    break;
            }
            
            onSuccess?.Invoke();
            SaveUserData();
            return true;
        }
        
        #endregion

        #region Equipment System
        
        // 현재 선택된 용품 조회
        public int CurBowlId => _userData.curBowlId;
        public int CurLicenseId => _userData.curLicenseId;
        public int MaxCharacters => GetMaxCharacters(_userData.curLicenseId);
        
        // 용품 선택 (구매한 것 중에서)
        public bool SelectBowl(int bowlId)
        {
            if (_userData.purchasedBowlIds.Contains(bowlId))
            {
                _userData.curBowlId = bowlId;
                SaveUserData();
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool PurchaseBowl(int bowlId, int cost)
        {
            return PurchaseItemInternal(ItemType.Bowl, bowlId, cost);
        }
        
        public bool PurchaseTower(int towerId, int cost)
        {
            return PurchaseItemInternal(ItemType.Tower, towerId, cost);
        }

        public bool PurchaseSofa(int sofaId, int cost)
        {
            return PurchaseItemInternal(ItemType.Sofa, sofaId, cost);
        }

        public bool PurchaseLicense(int licenseId, int cost)
        {
            return PurchaseItemInternal(ItemType.License, licenseId, cost);
        }

        // 구매 여부 확인 (통합된 메소드 사용)
        public bool IsBowlPurchased(int bowlId) => IsItemPurchased(ItemType.Bowl, bowlId);
        public bool IsTowerPurchased(int towerId) => IsItemPurchased(ItemType.Tower, towerId);
        public bool IsSofaPurchased(int sofaId) => IsItemPurchased(ItemType.Sofa, sofaId);
        public bool IsLicensePurchased(int licenseId) => IsItemPurchased(ItemType.License, licenseId);
        
        #endregion
        
        
        #region ToDo System

        private List<TodoItem> _todoItems = new List<TodoItem>();
        
        public List<TodoItem> GetTodoItems()
        {
            _todoItems.Clear();
            foreach (var data in _userData.todoItems)
            {
                _todoItems.Add(data.ToTodoItem());
            }
            return _todoItems;
        }
        
        #endregion

        #region Character Purchase/Selection

        public bool IsCharacterPurchased(int characterId) => IsItemPurchased(ItemType.Character, characterId);

        public bool PurchaseCharacter(int characterId, int price)
        {
            // 선행 단계 체크
            if (characterId > 0 && !IsCharacterPurchased(characterId - 1)) return false;
            
            System.Action onCharacterPurchase = () =>
            {
                OnCharacterChanged?.Invoke();
            };
            
            return PurchaseItemInternal(ItemType.Character, characterId, price, onCharacterPurchase);
        }

        public bool IsCharacterSelected(int characterId)
        {
            return Array.Exists(_userData.curCharacterIds, id => id == characterId);
        }

        public bool SelectCharacter(int characterId)
        {
            if (!IsCharacterPurchased(characterId)) return false;
            if (IsCharacterSelected(characterId)) return true;

            // 찾기 빈 슬롯
            for (int i = 0; i < MaxCharacters; i++)
            {
                if (_userData.curCharacterIds[i] == -1)
                {
                    _userData.curCharacterIds[i] = characterId;
                    SaveUserData();
                    OnCharacterChanged?.Invoke();
                    return true;
                }
            }
            // 슬롯 가득 참
            return false;
        }

        public bool DeselectCharacter(int characterId)
        {
            if (!IsCharacterSelected(characterId)) return false;

            // 최소 1마리 유지
            int currentCount = 0;
            foreach (var id in _userData.curCharacterIds)
            {
                if (id != -1) currentCount++;
            }
            if (currentCount <= 1) return false;

            for (int i = 0; i < MaxCharacters; i++)
            {
                if (_userData.curCharacterIds[i] == characterId)
                {
                    _userData.curCharacterIds[i] = -1;
                    SaveUserData();
                    OnCharacterChanged?.Invoke();
                    return true;
                }
            }
            return false;
        }

        #endregion

        [Serializable]
        public class UserData
        {
            public int coins = 500;
            public List<int> purchasedBowlIds = new List<int>();
            public List<int> purchasedTowerIds = new List<int>();
            public List<int> purchasedToyIds = new List<int>();
            public List<int> purchasedCharacterIds = new List<int>();
            public List<int> purchasedSofaIds = new List<int>();
            public List<int> purchasedLicenseIds = new List<int>();

            public int curBowlId = 0; // 현재 선택된 밥그릇 ID
            public int curLicenseId = -1;
            
            public Vector3 foodBowlPosition = Vector3.zero; // 밥그릇 위치
            // 다중 인스턴스 위치 저장용 (데이터 ID 기반)
            public List<Vector3> towerPositions = new List<Vector3>();
            public List<Vector3> sofaPositions = new List<Vector3>();
            
            // 배치된 가구 ID 목록 (게임 시작 시 복원용)
            public List<int> placedSofaIds = new List<int>();
            public List<int> placedTowerIds = new List<int>();
            
            public int totalCoinsEarned = 0; // 총 획득한 코인
            public int totalCoinsSpent = 0; // 총 사용한 코인
            
            public List<TodoItemData> todoItems = new List<TodoItemData>();
            

            // 플레이어 상태 정보
            public bool isFirstRun; // 첫 플레이 여부
            public bool isDemoPlayer; // 데모 버전 플레이 경험

            public UserData()
            {
                isFirstRun = true;
                isDemoPlayer = true;

                purchasedCharacterIds.Add(0);
                for (int i = 0; i < curCharacterIds.Length; i++) curCharacterIds[i] = -1;
                curCharacterIds[0] = 0;
                purchasedBowlIds.Add(0);
            }

            public int curPlacedToyId = -1;
            public Vector3 placedToyPosition = Vector3.zero;

            public int curMovingToyId = -1;
            public Vector3 movingToyPosition = Vector3.zero;
            public int[] curCharacterIds = new int[10]; // -1 빈 슬롯

            public int feedCnt = 0;
        }

        [Serializable]
        public class TodoItem
        {
            public int id;
            public string memo;
            public DateTime alarmTime;
            public bool isCompleted;
            public bool hasAlarm;

            public TodoItem()
            {
                id = 0; 
                memo = "";
                alarmTime = DateTime.Now;
                isCompleted = false;
                hasAlarm = false;
            }

            public TodoItem(string memo, DateTime alarmTime, bool hasAlarm = false)
            {
                this.id = 0; 
                this.memo = memo;
                this.alarmTime = alarmTime;
                this.isCompleted = false;
                this.hasAlarm = hasAlarm;
            }

            /// <summary>
            /// ID를 생성합니다. 직렬화가 완료된 후 호출해야 합니다.
            /// </summary>
            public void GenerateId()
            {
                if (id == 0) // ID가 설정되지 않은 경우에만 생성
                {
                    id = UnityEngine.Random.Range(1000, 9999);
                }
            }
        }

        [Serializable]
        public class TodoItemData
        {
            public int id;
            public string memo;
            public string alarmTimeString;
            public bool isCompleted;
            public bool hasAlarm;

            public TodoItemData(TodoItem item)
            {
                id = item.id;
                memo = item.memo;
                alarmTimeString = item.alarmTime.ToBinary().ToString();
                isCompleted = item.isCompleted;
                hasAlarm = item.hasAlarm;
            }

            public TodoItem ToTodoItem()
            {
                var item = new TodoItem();
                item.id = id;
                item.memo = memo;
                
                if (long.TryParse(alarmTimeString, out long binary))
                {
                    item.alarmTime = DateTime.FromBinary(binary);
                }
                else
                {
                    item.alarmTime = DateTime.Now;
                }
                
                item.isCompleted = isCompleted;
                item.hasAlarm = hasAlarm;
                
                // ID가 없는 경우 새로 생성 (기존 데이터 호환성)
                if (item.id == 0)
                {
                    item.GenerateId();
                }
                
                return item;
            }
        }

    }
} 