using System.Collections.Generic;
using Character.Controller;
using Character.States;
using Core.Base;
using Core.Managers;
using Objects.Data;
using UnityEngine;

namespace Objects
{
    /// <summary>
    /// 소파 오브젝트
    /// - 슬롯 점유/해제, 캐릭터 드롭/할당, 위치 저장/복원
    /// - 창 높이 변경 등 환경 변화에 따른 위치 보정
    /// </summary>
    public class Sofa : MonoBehaviour, IClickable
    {
        public SofaData sofaData;

        [SerializeField]
        private SpriteRenderer _spriteRenderer;
        
        // 고유 ID 시스템
        private int _instanceId;
        public int InstanceId => _instanceId;
        private bool _isDragging = false;
        public bool IsDragging => _isDragging;

        [SerializeField]
        private BoxCollider2D _collider;

        private HashSet<CharacterController> _usingCharacters = new HashSet<CharacterController>();
        public Dictionary<int, bool> usingSlots = new Dictionary<int, bool>();
        private Dictionary<CharacterController, int> _characterToSlot = new Dictionary<CharacterController, int>();

        private Transform[] _sitTransforms;

        void Start()
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_collider == null)
                _collider = GetComponent<BoxCollider2D>();

            // 데이터가 없으면 DataManager에서 로드
            if (sofaData == null)
            {
                LoadSofaData();
            }

            LoadPosition();

            SetupSitTransforms();

            if (!Utils.IsInCameraView(transform.position))
                Utils.ResetToCanvasBottomCenter(transform);

            if (DataManager.Instance != null)
                DataManager.Instance.onWindowSettingsChanged += OnWindowSettingsChanged;

            if (sofaData == null)
            {
                gameObject.SetActive(false);
                return;
            }

            _collider.offset = sofaData.colliderOffset;
            _collider.size = sofaData.colliderSize;

            _spriteRenderer.sprite = sofaData.sofaSprite;
        }

        void OnDestroy()
        {
            // 이벤트 구독 해제
            if (DataManager.Instance != null)
            {
                DataManager.Instance.onWindowSettingsChanged -= OnWindowSettingsChanged;
            }
        }
        
        private void OnWindowSettingsChanged(Core.Data.WindowSettings settings)
        {
            // 먼저 사용 중/접근 중인 캐릭터들을 떨어뜨린 뒤
            DropAllCharacters();

            // 소파 위치를 새 바닥 높이에 맞춰 스냅
            if (!Utils.IsInCameraView(transform.position))
            {
                Utils.ResetToCanvasBottomCenter(transform);
                SavePosition();
            }
            else
            {
                // 화면 안에 있어도 Y좌표는 바닥으로 조정
                Vector3 curPos = transform.position;
                float newY = WindowManager.Instance.GetCanvasBottomY();
                if (Mathf.Abs(curPos.y - newY) > 0.1f)
                {
                    curPos.y = newY;
                    curPos.z = 1f; // 소파는 항상 z=1
                    transform.position = curPos;
                    SavePosition();
                }
            }

            // 좌표가 변했으니 앉기 포인트도 갱신
            SetupSitTransforms();
        }
        
        public Vector3[] GetSitPositions()
        {
            if (_sitTransforms == null || _sitTransforms.Length == 0)
                return new Vector3[] { transform.position };
            
            Vector3[] positions = new Vector3[_sitTransforms.Length];
            for (int i = 0; i < _sitTransforms.Length; i++)
            {
                positions[i] = _sitTransforms[i].position;
            }
            return positions;
        }
        
        private void SetupSitTransforms()
        {
            // 기존 자식 오브젝트들 제거
            for (int i = transform.childCount - 1; i >= 0; i--) 
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            
            if (sofaData?.sitPositions == null || sofaData.sitPositions.Length == 0)
            {
                _sitTransforms = new Transform[0];
                return;
            }
            
            // towerData.climbPositions 기반으로 자식 오브젝트 생성
            _sitTransforms = new Transform[sofaData.sitPositions.Length];
            for (int i = 0; i < sofaData.sitPositions.Length; i++)
            {
                GameObject sitPoint = new GameObject($"SitPoint_{i}");
                sitPoint.transform.SetParent(transform);
                sitPoint.transform.localPosition = sofaData.sitPositions[i];
                
                _sitTransforms[i] = sitPoint.transform;
            }
        }
                
        public Vector3[] GetDownPositions(int? startSlot)
        {
            float groundY = WindowManager.Instance.GetCanvasBottomY();
            
            if (startSlot.HasValue && startSlot.Value >= 0 && startSlot.Value < _sitTransforms.Length)
            {
                // 시작 슬롯에서 좌우로 0.5~1 정도 떨어진 바닥 위치
                Vector3 startPos = _sitTransforms[startSlot.Value].position;
                float randomOffset = Random.Range(-1f, 1f); // -1 ~ 1 사이 랜덤
                Vector3 groundPos = new Vector3(startPos.x + randomOffset, groundY, startPos.z);
                return new Vector3[] { groundPos };
            }
            else
            {
                // 기본 위치에서 좌우로 0.5~1 정도 떨어진 바닥 위치
                float randomOffset = Random.Range(-1f, 1f); // -1 ~ 1 사이 랜덤
                Vector3 groundPos = new Vector3(transform.position.x + randomOffset, groundY, transform.position.z);
                return new Vector3[] { groundPos };
            }
        }
        
        /// <summary>
        /// 소파 등반 위치 배열 반환 (사용하지 않는 슬롯 중 하나)
        /// </summary>
        public Vector3[] GetClimbPositions()
        {
            if (_sitTransforms == null || _sitTransforms.Length == 0)
                return new Vector3[] { transform.position };
            
            // 사용하지 않는 슬롯 중 하나 찾기
            for (int i = 0; i < _sitTransforms.Length; i++)
            {
                if (!IsUsingSlot(i))
                {
                    return new Vector3[] { _sitTransforms[i].position };
                }
            }
            
            // 모든 슬롯이 사용 중이면 첫 번째 위치 반환
            return new Vector3[] { _sitTransforms[0].position };
        }

        /// <summary>
        /// 특정 슬롯으로 등반할 때의 위치를 반환
        /// </summary>
        public Vector3[] GetClimbPositionsForSlot(int slotIndex)
        {
            if (_sitTransforms == null || _sitTransforms.Length == 0)
                return new Vector3[] { transform.position };

            if (slotIndex >= 0 && slotIndex < _sitTransforms.Length && _sitTransforms[slotIndex] != null)
            {
                return new Vector3[] { _sitTransforms[slotIndex].position };
            }

            return new Vector3[] { _sitTransforms[0] != null ? _sitTransforms[0].position : transform.position };
        }
        public void DropAllCharacters()
        {
            // 1. 현재 소파를 사용 중인 캐릭터들 떨어뜨리기
            DropUsingCharacters();
            
            // 2. 소파로 이동 중이거나 등반 중인 캐릭터들 처리
            DropApproachingCharacters();
        }
        
        private void DropUsingCharacters()
        {
            var charactersToDrop = new List<CharacterController>(_usingCharacters);
            
            foreach (var character in charactersToDrop)
            {
                if (character != null)
                {
                    // 슬롯/소유 정보 해제 후 상태 전환 순으로 변경해 낙하 판정이 정확하게 되게 함
                    ReleaseSlot(character);
                    character.ChangeState(new InteractionState(character, false));
                }
            }
        }
        
        private void DropApproachingCharacters()
        {
            foreach (var character in ObjectManager.Instance.Characters)
            {
                bool shouldDrop = false;
                
                // 이 소파로 이동 중인 캐릭터
                if (character.CurState is MoveState move && move.TargetSofa == this)
                {
                    shouldDrop = true;
                }
                
                // 이 소파를 사용 중인 캐릭터 (현재 슬롯 점유 여부로 확인)
                if (character.CurState is ClimbState climb && climb.TargetSofa == this)
                {
                    shouldDrop = true;
                }
                
                if (shouldDrop)
                {
                    DropCharacter(character);
                }
            }
        }
        
        private void DropCharacter(CharacterController character)
        {
            Debug.Log($"[Sofa] DropCharacter: {character.name} 떨어뜨리기 (슬롯: {character.CurSofaSlot})");
            float groundY = WindowManager.Instance.GetCanvasBottomY();
            
            // RootHeight 변경 등으로 바닥 높이가 급변해도, 약간의 높이 차이가 있으면 낙하 연출을 적용
            if (character.transform.position.y > groundY + 0.1f)
            {
                character.ChangeState(new InteractionState(character, false));
            }
            else
            {
                Vector3 groundPos = new Vector3(character.transform.position.x, groundY, character.transform.position.z);
                character.transform.position = groundPos;
                character.ChangeState(new DecisionState(character));
            }
            
            ReleaseSlot(character);
        }
        
        public void StartUsing(CharacterController character)
        {
            if (character == null) return;
            if (_characterToSlot.ContainsKey(character)) return;

            // 빈 슬롯 탐색 - IsUsingSlot 사용으로 통일
            int slotIndex = -1;
            for (int i = 0; i < _sitTransforms.Length; i++)
            {
                if (!IsUsingSlot(i)) // slotToCharacter 기반으로 체크
                {
                    slotIndex = i;
                    break;
                }
            }
            
            if (slotIndex == -1)
            {
                // 모든 슬롯이 사용 중이면 바닥으로 떨어뜨림
                Debug.LogWarning($"[Sofa] 모든 슬롯이 사용 중입니다. {character.name}을 바닥으로 떨어뜨립니다.");
                DropToFloor(character);
                return;
            }

            // TryAssignSlot으로 안전하게 할당
            if (TryAssignSlot(character, slotIndex))
            {
                Debug.Log($"[Sofa] {character.name}이 슬롯 {slotIndex}에 할당되었습니다.");
            }
            else
            {
                // 할당 실패 시 바닥으로
                Debug.LogWarning($"[Sofa] 슬롯 {slotIndex} 할당 실패. {character.name}을 바닥으로 떨어뜨립니다.");
                DropToFloor(character);
            }
        }

        // 슬롯 인덱스별 점유 캐릭터
        private Dictionary<int, CharacterController> slotToCharacter = new Dictionary<int, CharacterController>();

        // 해당 슬롯이 사용 중인지 체크
        public bool IsUsingSlot(int index)
        {
            return slotToCharacter.ContainsKey(index) && slotToCharacter[index] != null;
        }

        // 해당 슬롯을 사용중인 캐릭터 반환
        public CharacterController GetSlotOwner(int index)
        {
            if (slotToCharacter.ContainsKey(index))
                return slotToCharacter[index];
            return null;
        }

        // 드롭된 위치에서 어떤 ClimbPos 슬롯인지 찾기 (+-0.2 범위)
        public int GetSlotAtPosition(Vector3 dropPos)
        {
            if (_sitTransforms == null || _sitTransforms.Length == 0)
                return -1;
            
            float threshold = 0.2f; // 드롭 영역 범위 +-0.2
            
            for (int i = _sitTransforms.Length - 1; i >= 0; i--)
            {
                if (_sitTransforms[i] != null)
                {
                    Vector3 slotPos = _sitTransforms[i].position;
                    
                    // 각 ClimbPos 주변 +-0.2 영역에 드롭되었는지 체크
                    if (Mathf.Abs(dropPos.x - slotPos.x) <= threshold && dropPos.y >= slotPos.y)
                    {
                        return i; // 해당 슬롯 번호 반환
                    }
                }
            }
            
            return -1; // 어떤 ClimbPos 근처도 아님
        }

        // 캐릭터에게 슬롯 할당 시도 (성공 시 true, 실패 시 false)
        public bool TryAssignSlot(CharacterController character, int slotIndex)
        {
            if (IsUsingSlot(slotIndex)) {
                return false;
            }
            
            // 모든 Dictionary 동기화 업데이트
            slotToCharacter[slotIndex] = character;
            usingSlots[slotIndex] = true;
            _characterToSlot[character] = slotIndex;
            _usingCharacters.Add(character);
            
            // 캐릭터 상태 업데이트
            character.CurSofaSlot = slotIndex;
            
            // 현재 사용 중인 소파로 설정 (스마트 선택을 위해)
            character.SetCurSofa(this);
            
            character.UpdateSofaSortingOrder(slotIndex);
            
            return true;
        }

        // 캐릭터의 슬롯 점유 해제
        public void ReleaseSlot(CharacterController character)
        {
            if (character == null) return;
            
            int? slot = character.CurSofaSlot;
            Debug.Log($"[Sofa] ReleaseSlot: {character.name} 슬롯 {slot} 해제");
            
            if (slot.HasValue)
            {
                // 모든 Dictionary에서 일관되게 제거
                if (slotToCharacter.ContainsKey(slot.Value) && slotToCharacter[slot.Value] == character)
                    slotToCharacter.Remove(slot.Value);
                    
                if (usingSlots.ContainsKey(slot.Value))
                    usingSlots[slot.Value] = false;
            }
            
            // 캐릭터 관련 정보 정리
            if (_characterToSlot.ContainsKey(character))
                _characterToSlot.Remove(character);
                
            _usingCharacters.Remove(character);
            
            // 캐릭터 상태 초기화
            character.CurSofaSlot = null;
            
            // 현재 사용 중인 소파 해제 (스마트 선택을 위해)
            character.ClearCurSofa();

            character.ResetSortingOrder();

            // UpdateRemainingCharactersSortingOrder();
        }

        private void UpdateRemainingCharactersSortingOrder()
        {
            for (int i = 0; i < sofaData.sitPositions.Length; i++)
            {
                if (IsUsingSlot(i))
                {
                    var character = GetSlotOwner(i);
                    if (character != null)
                    {
                        character.UpdateSofaSortingOrder(i);
                    }
                }
            }
        }

        
        public void DropToFloor(CharacterController character)
        {
            if (character == null) return;
            ReleaseSlot(character);
            character.ChangeState(new InteractionState(character, false));
        }
        
        public bool IsCharacterUsing(CharacterController character)
        {
            return character != null && _usingCharacters.Contains(character);
        }
        
        // 용품 선택 변경 시 데이터 새로고침
        public void RefreshSofaData()
        {
            LoadSofaData();
            
            if (sofaData != null)
            {
                // 소파 활성화
                gameObject.SetActive(true);
                _spriteRenderer.sprite = sofaData.sofaSprite;
                _collider.offset = sofaData.colliderOffset;
                _collider.size = sofaData.colliderSize;
                
                SetupSitTransforms();
                
                // 위치도 다시 로드
                LoadPosition();
            }
            else
            {
                DropAllCharacters();
                
                gameObject.SetActive(false);
            }
        }
        
        public void GenerateInstanceId()
        {
            // 소파 데이터 ID를 인스턴스 ID로 사용 (같은 ID는 하나만 배치 가능)
            if (sofaData != null)
            {
                _instanceId = sofaData.sofaId;
            }
            else
            {
                _instanceId = -1; // 무효한 ID
            }
        }
        
        public void LoadPosition()
        {
            if (DataManager.Instance != null && _instanceId >= 0)
            {
                Vector3 savedPosition = DataManager.Instance.GetSofaPosition(_instanceId);
                if (savedPosition != Vector3.zero)
                {
                    // 저장된 위치가 있으면 사용 (z값은 항상 1로 강제)
                    savedPosition.z = 1f; // 소파는 항상 z=1
                    transform.position = savedPosition;
                    Debug.Log($"[Sofa] 저장된 위치로 복원 - {savedPosition}");
                }
                else
                {
                    // 저장된 위치가 없으면 기본 위치 (Canvas 바닥)
                    Vector3 curPos = transform.position;
                    curPos.y = WindowManager.Instance.GetCanvasBottomY();
                    curPos.z = 1f; // 소파는 항상 z=1
                    transform.position = curPos;
                }
            }
            else
            {
                Debug.LogError($"[Sofa] LoadPosition 실패 - DataManager: {DataManager.Instance != null}, ID: {_instanceId}");
            }
        }
        
        private void SavePosition()
        {
            if (DataManager.Instance != null && _instanceId >= 0)
            {
                DataManager.Instance.SetSofaPosition(_instanceId, transform.position);
                Debug.Log($"[Sofa] 위치 저장 완료 - ID: {_instanceId}");
            }
            else
            {
                Debug.LogError($"[Sofa] 위치 저장 실패 - DataManager: {DataManager.Instance != null}, ID: {_instanceId}");
            }
        }
        
        private void LoadSofaData()
        {
            // ObjectManager에서 이미 sofaData를 할당하므로 별도 로딩 불필요
            // 데이터 검증만 수행
            if (sofaData == null)
            {
                Debug.LogError("[Sofa] sofaData가 할당되지 않았습니다!");
                return;
            }
            
            // 소파 배치 시 BGM 볼륨 조정
            if (sofaData.sofaId >= 0)
            {
                var curSettings = DataManager.Instance.SoundSettings;
                if (curSettings.bgmVolume >= 0.1f) // 10% 이상
                {
                    var newSettings = curSettings;
                    newSettings.bgmVolume = 0.05f; // 5%로 낮추기
                    DataManager.Instance.UpdateSoundSettings(newSettings);
                }
            }
        }
        public void OnClick()
        {
            
        }

        public void OnDragStart()
        {
            _isDragging = true;

            DropAllCharacters();
        }
        
        public void OnDragEnd()
        {
            if (!_isDragging) return;
            _isDragging = false;

            Vector3 curPos = transform.position;
            curPos.y = WindowManager.Instance.GetCanvasBottomY();
            curPos.z = 1f; // 소파는 항상 z=1
            transform.position = curPos;

            SavePosition();

        }

    }
}
