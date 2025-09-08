using Core.Base;
using Objects;
using Character.Controller;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core.Managers
{
    /// <summary>
    /// 오브젝트 레지스트리/복원 매니저
    /// - 씬 내 주요 오브젝트 등록/해제/조회(캐릭터, 소파, 타워 등)
    /// - 배치 복원/제거, 스케일 일괄 적용, 최적 대상 탐색(소파/타워)
    /// </summary>
    public class ObjectManager : Singleton<ObjectManager>
    {
        [Header("=== 가구 프리팹 ===")]
        [SerializeField] private GameObject _sofaPrefab; // 소파 프리팹
        [SerializeField] private GameObject _towerPrefab; // 타워 프리팹
        [SerializeField] private Transform _objectTransform; // 오브젝트 트랜스폼
        
        // 게임 오브젝트 참조들
        private FoodBowl _foodBowl;
        
        // 다중 인스턴스 관리용 리스트
        private List<Tower> _towers = new List<Tower>();
        private List<Sofa> _sofas = new List<Sofa>();
        
        // === 원본 크기 저장 ===
        private Dictionary<CharacterController, Vector3> _characterOriginalScales = new();
        private Vector3 _foodBowlOriginalScale = Vector3.one;
        private Vector3 _placedToyOriginalScale = Vector3.one;
        private Vector3 _fishingOriginalScale = Vector3.one;
        private Vector3 _movingToyOriginalScale = Vector3.one;
        private Dictionary<Tower, Vector3> _towerOriginalScales = new Dictionary<Tower, Vector3>();
        private Dictionary<Sofa, Vector3> _sofaOriginalScales = new Dictionary<Sofa, Vector3>();
        // 등록 이벤트들
        public event Action<FoodBowl> OnFoodBowlRegistered;
        public event Action<Tower> OnTowerRegistered;
        public event Action<CharacterController> OnCharacterRegistered;
        public event Action<PlacedToy> OnPlacedToyRegistered;
        public event Action<Fishing> OnFishingRegistered;
        public event Action<MovingToy> OnMovingToyRegistered;
        public event Action<Sofa> OnSofaRegistered;
        // 해제 이벤트들
        public event Action OnFoodBowlUnregistered;
        public event Action OnTowerUnregistered;
        public event Action OnCharacterUnregistered;
        public event Action OnPlacedToyUnregistered;
        public event Action OnFishingUnregistered;
        public event Action OnMovingToyUnregistered;
        public event Action OnSofaUnregistered;
        private readonly List<CharacterController> _characters = new();
        public IReadOnlyList<CharacterController> Characters => _characters;
        public CharacterController Character => _characters.Count > 0 ? _characters[0] : null;

        public FoodBowl FoodBowl => _foodBowl;
        
        // 다중 인스턴스 접근용 프로퍼티
        public IReadOnlyList<Tower> Towers => _towers;
        public IReadOnlyList<Sofa> Sofas => _sofas;
        
        // 중복 배치 방지용 메서드 (같은 ID는 하나만 배치 가능)
        public bool HasSofaId(int sofaId)
        {
            return _sofas.Any(s => s != null && s.sofaData != null && s.sofaData.sofaId == sofaId);
        }
        
        public bool HasTowerId(int towerId)
        {
            return _towers.Any(t => t != null && t.towerData != null && t.towerData.towerId == towerId);
        }
        
        public Sofa GetSofaId(int sofaId)
        {
            return _sofas.FirstOrDefault(s => s != null && s.sofaData != null && s.sofaData.sofaId == sofaId);
        }
        
        public Tower GetTowerId(int towerId)
        {
            return _towers.FirstOrDefault(t => t != null && t.towerData != null && t.towerData.towerId == towerId);
        }

        #region 가구 선택 로직

        /// <summary>
        /// 캐릭터에게 가장 적합한 타워를 찾아 반환
        /// 거리, 빈 슬롯 수, 사용 가능성을 종합 고려
        /// </summary>
        public Tower FindBestTower(CharacterController character)
        {
            if (character == null || _towers.Count == 0) return null;

            Tower bestTower = null;
            float bestScore = float.NegativeInfinity;
            Vector3 characterPosition = character.transform.position;

            foreach (var tower in _towers)
            {
                if (tower == null || !tower.gameObject.activeInHierarchy || tower.towerData == null) 
                    continue;

                // 이미 이 캐릭터가 사용 중이면 건너뛰기
                if (tower.IsCharacterUsing(character)) continue;

                // 빈 슬롯 수 계산
                int availableSlots = 0;
                for (int i = 0; i < tower.towerData.climbPositions.Length; i++)
                {
                    if (!tower.IsUsingSlot(i)) availableSlots++;
                }
                if (availableSlots == 0) continue;

                // 예약(pending): 이 타워를 등반/이동 타깃으로 둔 캐릭터 수
                int pending = 0;
                for (int i = 0; i < _characters.Count; i++)
                {
                    var c = _characters[i];
                    if (c == null || c == character) continue;
                    var s = c.CurState;
                    if (s is Character.States.ClimbState climb && climb.TargetTower == tower) pending++;
                    else if (s is Character.States.MoveState move && move.TargetTower == tower) pending++;
                }

                int freeSlots = availableSlots - pending;
                if (freeSlots <= 0) continue;

                float distance = Vector3.Distance(characterPosition, tower.transform.position);
                bool hasLoad = (availableSlots - freeSlots) > 0 || pending > 0;
                float loadPenalty = hasLoad ? 0.5f : 0f;
                float tieBreaker = (character.CharacterSlotIndex + 1) * 0.001f;
                // 빈자리 1개 이상이면 동등 취급: 가까운 곳 우선 + 소폭 분산
                float score = -distance - loadPenalty + tieBreaker;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTower = tower;
                }
            }

            return bestTower;
        }

        /// <summary>
        /// 캐릭터에게 가장 적합한 소파를 찾아 반환
        /// 거리, 빈 슬롯 수, 사용 가능성을 종합 고려
        /// </summary>
        public Sofa FindBestSofa(CharacterController character)
        {
            if (character == null || _sofas.Count == 0) return null;

            Sofa bestSofa = null;
            float bestScore = float.NegativeInfinity;
            Vector3 characterPosition = character.transform.position;

            foreach (var sofa in _sofas)
            {
                if (sofa == null || !sofa.gameObject.activeInHierarchy || sofa.sofaData == null) 
                    continue;

                // 이미 이 캐릭터가 사용 중이면 건너뛰기
                if (sofa.IsCharacterUsing(character)) continue;

                // 빈 슬롯 수 계산
                int availableSlots = 0;
                for (int i = 0; i < sofa.sofaData.sitPositions.Length; i++)
                {
                    if (!sofa.IsUsingSlot(i)) availableSlots++;
                }
                if (availableSlots == 0) continue;

                // pending(등반/이동으로 이 소파를 타깃으로 둔 캐릭터 수) 차감
                int pending = 0;
                for (int i = 0; i < _characters.Count; i++)
                {
                    var c = _characters[i];
                    if (c == null || c == character) continue;
                    var s = c.CurState;
                    if (s is Character.States.ClimbState climb && climb.TargetSofa == sofa) pending++;
                    else if (s is Character.States.MoveState move && move.TargetSofa == sofa) pending++;
                }

                int freeSlots = availableSlots - pending;
                if (freeSlots <= 0) continue;

                float distance = Vector3.Distance(characterPosition, sofa.transform.position);
                bool hasLoad = (availableSlots - freeSlots) > 0 || pending > 0;
                float loadPenalty = hasLoad ? 0.5f : 0f;
                float tie = (character.CharacterSlotIndex + 1) * 0.001f;
                float score = -distance - loadPenalty + tie;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSofa = sofa;
                }
            }

            return bestSofa;
        }

        #endregion
        protected override void Awake()
        {
            base.Awake();
        }
        
        protected override void Start()
        {
            // 게임 시작 시 저장된 배치 정보로 가구들 복원
            RestorePlacedFurniture();
        }
        private void ApplyObjectScale(Transform transform, ref Vector3 originalScale)
        {
            originalScale = transform.localScale;
            float scale = DataManager.Instance.GetCurScaleFactor();
            transform.localScale = originalScale * scale;
        }
        
        #region FoodBowl 관리
        
        public void RegisterFoodBowl(FoodBowl bowl)
        {
            if (bowl == null) return;
            _foodBowl = bowl;
            ApplyObjectScale(bowl.transform, ref _foodBowlOriginalScale);
            OnFoodBowlRegistered?.Invoke(bowl);
        }
        
        public void UnregisterFoodBowl()
        {
            if (_foodBowl != null)
            {
                _foodBowl = null;
                _foodBowlOriginalScale = Vector3.one;
                OnFoodBowlUnregistered?.Invoke();
            }
        }
        
        #endregion
        
        #region Tower 관리
        
        public void RegisterTower(Tower tower)
        {
            if (tower == null) return;
            
            // 다중 인스턴스 리스트에 추가
            if (!_towers.Contains(tower))
            {
                _towers.Add(tower);
                
                // 원본 스케일 저장
                Vector3 originalScale = tower.transform.localScale;
                _towerOriginalScales[tower] = originalScale;
                ApplyObjectScale(tower.transform, ref originalScale);
            }
            
            OnTowerRegistered?.Invoke(tower);
        }
        
        public void UnregisterTower(Tower tower)
        {
            if (tower == null) return;
            
            // 다중 인스턴스 리스트에서 제거
            if (_towers.Contains(tower))
            {
                _towers.Remove(tower);
                _towerOriginalScales.Remove(tower);
            }
            
            OnTowerUnregistered?.Invoke();
        }
        
        #endregion
        
        #region CharacterController 관리
        
        public void RegisterCharacter(CharacterController character)
        {
            if (character == null || _characters.Contains(character)) return;
            _characters.Add(character);
            
            // 원본 크기 저장 및 적용
            if (!_characterOriginalScales.ContainsKey(character))
            {
                Vector3 originalScale = character.transform.localScale;
                ApplyObjectScale(character.transform, ref originalScale);
                _characterOriginalScales[character] = originalScale;
            }
            
            OnCharacterRegistered?.Invoke(character);
        }
        
        public void UnregisterCharacter(CharacterController character)
        {
            _characters.Remove(character);
            if (_characterOriginalScales.ContainsKey(character))
                _characterOriginalScales.Remove(character);
            OnCharacterUnregistered?.Invoke();
        }
        
        #endregion

        #region Fishing 관리
        
        #endregion

        #region PlacedToy 관리
        
        
        #endregion

        #region MovingToy 관리
        
        #endregion

        #region Sofa 관리
        public void RegisterSofa(Sofa sofa)
        {
            if (sofa == null) return;
            
            // 다중 인스턴스 리스트에 추가
            if (!_sofas.Contains(sofa))
            {
                _sofas.Add(sofa);
                
                // 원본 스케일 저장
                Vector3 originalScale = sofa.transform.localScale;
                _sofaOriginalScales[sofa] = originalScale;
                ApplyObjectScale(sofa.transform, ref originalScale);
            }
            
            OnSofaRegistered?.Invoke(sofa);
        }

        public void UnregisterSofa(Sofa sofa)
        {
            if (sofa == null) return;
            
            // 다중 인스턴스 리스트에서 제거
            if (_sofas.Contains(sofa))
            {
                _sofas.Remove(sofa);
                _sofaOriginalScales.Remove(sofa);
            }
            
            OnSofaUnregistered?.Invoke();
        }
        #endregion
        // === 일괄 크기 조절 ===
        public void ApplyScaleToAllObjects(float scale)
        {
            // 크기 조절 시 모든 타워에서 캐릭터 떨어뜨리기
            foreach (var tower in _towers)
            {
                if (tower != null)
                    tower.DropAllCharacters();
            }
            
            // 모든 소파에서도 캐릭터 떨어뜨리기
            foreach (var sofa in _sofas)
            {
                if (sofa != null)
                    sofa.DropAllCharacters();
            }
            
            // 캐릭터들
            foreach (var character in _characters)
            {
                if (character != null && _characterOriginalScales.ContainsKey(character))
                    character.transform.localScale = _characterOriginalScales[character] * scale;
            }
            
            // 모든 타워들
            foreach (var tower in _towers)
            {
                if (tower != null && _towerOriginalScales.ContainsKey(tower))
                    tower.transform.localScale = _towerOriginalScales[tower] * scale;
            }
            
            // 모든 소파들
            foreach (var sofa in _sofas)
            {
                if (sofa != null && _sofaOriginalScales.ContainsKey(sofa))
                    sofa.transform.localScale = _sofaOriginalScales[sofa] * scale;
            }
            
            // 밥그릇
            if (_foodBowl != null)
                _foodBowl.transform.localScale = _foodBowlOriginalScale * scale;
        }
        
        #region 가구 생성/제거 관리
        
        /// <summary>
        /// 게임 시작 시 저장된 배치 정보로 가구들 복원
        /// </summary>
        private void RestorePlacedFurniture()
        {
            if (DataManager.Instance == null) return;
            
            // 배치된 소파들 복원
            var placedSofaIds = DataManager.Instance.GetPlacedSofaIds();
            foreach (int sofaId in placedSofaIds)
            {
                CreateSofaInstance(sofaId);
            }
            
            // 배치된 타워들 복원
            var placedTowerIds = DataManager.Instance.GetPlacedTowerIds();
            foreach (int towerId in placedTowerIds)
            {
                CreateTowerInstance(towerId);
            }
            
            // 외부 소리 감지 연동 비활성화
            
            Debug.Log($"가구 복원 완료: 소파 {placedSofaIds.Count}개, 타워 {placedTowerIds.Count}개");
        }
        
        /// <summary>
        /// 소파 배치 - 중복 체크 후 생성
        /// </summary>
        public bool PlaceSofa(int sofaId, System.Action onComplete = null)
        {
            // 이미 배치된 소파인지 확인
            if (HasSofaId(sofaId))
            {
                Debug.LogWarning($"소파 ID {sofaId}는 이미 배치되어 있습니다.");
                return false;
            }
            
            if (CreateSofaInstance(sofaId))
            {
                DataManager.Instance.AddPlacedSofa(sofaId);
                
                // 외외부 소리 감지 연동 비활성화
                
                onComplete?.Invoke(); // 작업 완료 후 콜백 호출
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 소파 제거 - 인스턴스 제거 및 데이터 정리
        /// </summary>
        public bool RemoveSofa(int sofaId, System.Action onComplete = null)
        {
            var sofa = GetSofaId(sofaId);
            if (sofa == null)
            {
                Debug.LogWarning($"제거할 소파 ID {sofaId}를 찾을 수 없습니다.");
                return false;
            }
            
            // 소파 사용 중인 캐릭터들 떨어뜨리기
            sofa.DropAllCharacters();
            
            // 즉시 등록 해제 (HasSofaId()가 바로 false 반환하도록)
            UnregisterSofa(sofa);
            
            // 소파 오브젝트 제거
            Destroy(sofa.gameObject);
            
            // 데이터에서도 제거
            DataManager.Instance.RemovePlacedSofa(sofaId);
            
            // 외부 소리 감지 연동 비활성화
            
            Debug.Log($"소파 ID {sofaId} 제거 완료");
            onComplete?.Invoke(); // 작업 완료 후 콜백 호출
            return true;
        }
        
        /// <summary>
        /// 타워 배치 - 중복 체크 후 생성
        /// </summary>
        public bool PlaceTower(int towerId, System.Action onComplete = null)
        {
            // 이미 배치된 타워인지 확인
            if (HasTowerId(towerId))
            {
                Debug.LogWarning($"타워 ID {towerId}는 이미 배치되어 있습니다.");
                return false;
            }
            
            if (CreateTowerInstance(towerId))
            {
                DataManager.Instance.AddPlacedTower(towerId);
                onComplete?.Invoke(); // 작업 완료 후 콜백 호출
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 타워 제거 - 인스턴스 제거 및 데이터 정리
        /// </summary>
        public bool RemoveTower(int towerId, System.Action onComplete = null)
        {
            var tower = GetTowerId(towerId);
            if (tower == null)
            {
                Debug.LogWarning($"제거할 타워 ID {towerId}를 찾을 수 없습니다.");
                return false;
            }
            
            // 타워 사용 중인 캐릭터들 떨어뜨리기
            tower.DropAllCharacters();
            
            // 즉시 등록 해제 (HasTowerId()가 바로 false 반환하도록)
            UnregisterTower(tower);
            
            // 타워 오브젝트 제거
            Destroy(tower.gameObject);
            
            // 데이터에서도 제거
            DataManager.Instance.RemovePlacedTower(towerId);
            
            Debug.Log($"타워 ID {towerId} 제거 완료");
            onComplete?.Invoke(); // 작업 완료 후 콜백 호출
            return true;
        }
        
        /// <summary>
        /// 소파 인스턴스 생성 (내부용)
        /// </summary>
        private bool CreateSofaInstance(int sofaId)
        {
            // 소파 데이터 가져오기
            var sofaData = DataManager.Instance.GetSofaData(sofaId);
            if (sofaData == null)
            {
                Debug.LogError($"소파 ID {sofaId}의 데이터를 찾을 수 없습니다.");
                return false;
            }
            
            // 프리팹 확인
            if (_sofaPrefab == null)
            {
                Debug.LogError("소파 프리팹이 설정되지 않았습니다!");
                return false;
            }
            
            // 프리팹으로 소파 인스턴스 생성
            GameObject sofaObj = Instantiate(_sofaPrefab, _objectTransform);
            sofaObj.name = $"Sofa_{sofaId}";
            
            var sofa = sofaObj.GetComponent<Sofa>();
            if (sofa == null)
            {
                Debug.LogError("소파 프리팹에 Sofa 컴포넌트가 없습니다!");
                Destroy(sofaObj);
                return false;
            }
            
            // 소파 데이터 설정 (Start()에서 GenerateInstanceId() 자동 호출됨)
            sofa.sofaData = sofaData;
            
            // 확실하게 인스턴스 ID 생성 (드래그 시 위치 저장을 위해 필수)
            sofa.GenerateInstanceId();
            
            // ID 생성 후 저장된 위치 다시 로드
            sofa.LoadPosition();
            
            RegisterSofa(sofa);
            
            Debug.Log($"소파 ID {sofaId} 생성 완료, 인스턴스 ID: {sofa.InstanceId}");
            
            return true;
        }
        
        /// <summary>
        /// 타워 인스턴스 생성 (내부용)
        /// </summary>
        private bool CreateTowerInstance(int towerId)
        {
            // 타워 데이터 가져오기
            var towerData = DataManager.Instance.GetTowerData(towerId);
            if (towerData == null)
            {
                Debug.LogError($"타워 ID {towerId}의 데이터를 찾을 수 없습니다.");
                return false;
            }
            
            // 프리팹 확인
            if (_towerPrefab == null)
            {
                Debug.LogError("타워 프리팹이 설정되지 않았습니다!");
                return false;
            }
            
            // 프리팹으로 타워 인스턴스 생성
            GameObject towerObj = Instantiate(_towerPrefab, _objectTransform);
            towerObj.name = $"Tower_{towerId}";
            
            var tower = towerObj.GetComponent<Tower>();
            if (tower == null)
            {
                Debug.LogError("타워 프리팹에 Tower 컴포넌트가 없습니다!");
                Destroy(towerObj);
                return false;
            }
            
            // 타워 데이터 설정
            tower.towerData = towerData;
            
            // 확실하게 인스턴스 ID 생성 (드래그 시 위치 저장을 위해 필수)
            tower.GenerateInstanceId();
            
            // ID 생성 후 저장된 위치 다시 로드
            tower.LoadPosition();
            
            RegisterTower(tower);
            
            Debug.Log($"타워 ID {towerId} 생성 완료, 인스턴스 ID: {tower.InstanceId}");
            
            return true;
        }
        
        #endregion
    }
} 