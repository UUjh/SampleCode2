using UnityEngine;
using Character.Controller;
using Objects;
using Core.Managers;

namespace Character.States
{
    /// <summary>
    /// 캐릭터가 타워나 소파를 올라가는 상태
    /// 타워: 여러 위치를 순서대로 이동하고 최종적으로 누워있기
    /// 소파: 무조건 한 칸만 올라가고 옆으로 여러 자리
    /// </summary>
    public class ClimbState : StateBase
    {
        private Tower _tower = null;
        private Vector3[] _climbPositions;
        private int _curPosIndex = 0;
        private bool _isMoving = false;
        private bool _isPausing = false;
        private float _pauseTimer = 0f;
        private float _curPauseTime = 0f;
        private bool _isDown = false;
        private bool _isHungry = false;
        private bool _isDescendCompleted = false;

        private Sofa _sofa = null;

        private float _jumpDelay = 0.8f;
        private float _jumpDelayTimer = 0f;

        private float _landTime = 0.8f;
        private float _landTimer = 0f;

        private float _jumpDuration = 0.5f;
        private float _moveSpeed;
        
        private StateBase _nextState;
        private int _targetSlot = -1; // 목표 슬롯 저장
        private bool _hasSwitchedToBackSorting = false; // 소파 뒤 sorting order로 변경했는지 여부

        // 추가: 드래그 앤 드롭 시 정확한 가구 체크를 위한 프로퍼티
        public Tower TargetTower => _tower;
        public Sofa TargetSofa => _sofa;

        public ClimbState(CharacterController character, Tower tower, bool isDown = false, bool isHungry = false) : base(character)
        {
            _tower = tower;
            _isDown = isDown;
            _isHungry = isHungry;
            _jumpDelay = isDown ? 0.6f : 0.7f;
            _jumpDuration = isDown ? 0.7f : 0.5f;
            _landTime = isDown ? 0.7f : 0.8f;
        }

        // 다음 상태를 예약할 수 있는 생성자 추가
        public ClimbState(CharacterController character, Tower tower, bool isDown, StateBase nextState) : base(character)
        {
            _tower = tower;
            _isDown = isDown;
            _isHungry = false;
            _nextState = nextState;
            _jumpDelay = isDown ? 0.6f : 0.7f;
            _jumpDuration = isDown ? 0.7f : 0.5f;
            _landTime = isDown ? 0.7f : 0.8f;
        }

        public ClimbState(CharacterController character, Sofa sofa, bool isDown = false, bool isHungry = false) : base(character)
        {
            _sofa = sofa;
            _isDown = isDown;
            _isHungry = isHungry;
            _jumpDelay = isDown ? 0.6f : 0.7f;
            _jumpDuration = isDown ? 0.7f : 0.5f;
            _landTime = isDown ? 0.7f : 0.8f;
        }
        public ClimbState(CharacterController character, Sofa sofa, bool isDown, StateBase nextState) : base(character)
        {
            _sofa = sofa;
            _isDown = isDown;
            _isHungry = false;
            _nextState = nextState;
            _jumpDelay = isDown ? 0.6f : 0.7f;
            _jumpDuration = isDown ? 0.7f : 0.5f;
            _landTime = isDown ? 0.7f : 0.8f;
        }

        // 소파 특정 슬롯으로 등반하기 위한 간단한 생성자
        public ClimbState(CharacterController character, Sofa sofa, int targetSofaSlot) : base(character)
        {
            _sofa = sofa;
            _isDown = false;
            _isHungry = false;
            _targetSlot = targetSofaSlot;
            _jumpDelay = 0.7f;
            _jumpDuration = 0.5f;
            _landTime = 0.8f;
        }

        public override void Enter()
        {
            if (_isDown)
            {
                StartJumpDown();
            }
            else
            {
                StartJumpUp();
            }
        }

        #region Enter 로직 분리
        
        private void StartJumpDown()
        {
            if (_tower != null)
            {
                int? startSlot = _character.CurTowerSlot;
                _tower.ReleaseSlot(_character);
                _climbPositions = _tower.GetDownPositions(startSlot);
            }
            else if (_sofa != null)
            {
                int? startSlot = _character.CurSofaSlot;
                _sofa.ReleaseSlot(_character);
                _climbPositions = _sofa.GetDownPositions(startSlot);
            }
            _jumpDelayTimer = 0f;
            _curPosIndex = 0;
            _isMoving = true;
            _isPausing = false;
            Vector3 targetPos = _climbPositions[_curPosIndex];
            float distance = Vector3.Distance(_character.transform.position, targetPos);
            _moveSpeed = distance / _jumpDuration;
            _character.JumpDownAnim();
        }

        private void StartJumpUp()
        {
            int targetSlot = _targetSlot >= 0 ? _targetSlot : FindUsableTargetSlot();
            
            if (targetSlot == -1)
            {
                Debug.Log($"[ClimbState] {_character.characterData.characterName}: 모든 타워 슬롯이 사용중! DecisionState로 복귀");
                _character.ChangeState(new WanderState(_character));
                return;
            }

            // 안전망: 대상에 맞춰 등반 위치 배열 설정
            if (_climbPositions == null || _climbPositions.Length == 0)
            {
                if (_sofa != null)
                {
                    _climbPositions = _sofa.GetClimbPositionsForSlot(targetSlot);
                }
                else if (_tower != null)
                {
                    _climbPositions = _tower.GetClimbPositionsForSlot(targetSlot);
                    
                    // 목표 슬롯이 사용중이거나 경로가 없으면 다른 빈 슬롯 찾기
                    if (_climbPositions == null)
                    {
                        targetSlot = FindUsableTargetSlot();
                        if (targetSlot == -1)
                        {
                            Debug.Log($"[ClimbState] {_character.characterData.characterName}: 동시 접근으로 인해 모든 슬롯 사용중! WanderState로 복귀");
                            _character.ChangeState(new WanderState(_character));
                            return;
                        }
                        _climbPositions = _tower.GetClimbPositionsForSlot(targetSlot);
                    }
                }
            }

            if (_climbPositions == null || _climbPositions.Length == 0)
            {
                Debug.Log($"[ClimbState] {_character.characterData.characterName}: 등반 경로를 찾을 수 없음! WanderState로 복귀");
                _character.ChangeState(new WanderState(_character));
                return;
            }

            InitJumpUp(targetSlot);
        }

        private int FindUsableTargetSlot()
        {
            if (_tower != null)
            {
                // 제일 높은 층부터 빈 슬롯 찾기!
                for (int i = _tower.towerData.climbPositions.Length - 1; i >= 0; i--)
                {
                    if (!_tower.IsUsingSlot(i))
                    {
                        return i;
                    }
                }
            }
            else if (_sofa != null)
            {
                // 소파는 1층: 지정 슬롯이 있으면 우선 사용, 아니면 빈 슬롯 하나 선택
                if (_targetSlot >= 0)
                {
                    _climbPositions = _sofa.GetClimbPositionsForSlot(_targetSlot);
                    if (!_sofa.IsUsingSlot(_targetSlot))
                    {
                        return _targetSlot;
                    }
                }
                _climbPositions = _sofa.GetClimbPositions();
                for (int i = 0; i < _climbPositions.Length; i++)
                {
                    if (!_sofa.IsUsingSlot(i))
                    {
                        _targetSlot = i;
                        return i;
                    }
                }
            }
            
            return -1;
        }

        private void InitJumpUp(int targetSlot)
        {
            _targetSlot = targetSlot;
            _curPosIndex = 0;
            _isMoving = true;
            _isPausing = false;
            _jumpDelayTimer = 0f;
            _landTimer = 0f;
            
            Vector3 firstPos = _climbPositions[0];
            float distance = Vector3.Distance(_character.transform.position, firstPos);
            _moveSpeed = distance / _jumpDuration;
            _character.JumpAnim();
            
            Debug.Log($"[ClimbState] {_character.characterData.characterName}: 목표는 슬롯 {targetSlot}! 0번부터 순차적으로 올라가기 시작!");
        }

        #endregion
        public override void Update()
        {
            if (_isPausing)
            {
                HandlePause();
            }
            else if (_isMoving)
            {
                HandleMoving();
            }
            else
            {
                HandleStuckState();
            }
        }

        private void HandleStuckState()
        {
            // 현재 상황에 따라 적절한 상태로 복구
            if (_isDown)
            {
                if (_isDescendCompleted)
                {
                    CompleteDown();
                }
                else
                {
                    ContinueJumpDown();
                }
            }
            else
            {
                if (ReachedTarget())
                {
                    StartResting();
                }
                else
                {
                    HandleMoving();
                }
            }
        }

        #region Update 로직 분리

        private void HandlePause()
        {
            _pauseTimer += Time.deltaTime;
            if (_pauseTimer >= _curPauseTime)
            {
                _isPausing = false;
                _pauseTimer = 0f;
                
                if (_isDown)
                {
                    HandlePauseDown();
                }
                else
                {
                    HandlePauseUp();
                }
            }
        }

        private void HandlePauseDown()
        {
            _jumpDelayTimer = 0f;
            _landTimer = 0f;
            
            if (_isDescendCompleted)
            {
                CompleteDown();
                return;
            }
            
            _curPosIndex++;
            if (_curPosIndex < _climbPositions.Length)
            {
                ContinueJumpDown();
            }
            else
            {
                StartJumpDownCompletion();
            }

        }

        private void HandlePauseUp()
        {
            _jumpDelayTimer = 0f;
            _landTimer = 0f;
            
            int currentSlot = _curPosIndex;

            // 다음 배열 인덱스로 이동
            int nextIndex = _curPosIndex + 1;
            
            // 배열 끝에 도달하면 휴식 시작
            if (nextIndex >= _climbPositions.Length)
            {
                StartResting();
                return;
            }
            
            // 목표 슬롯이 선점되었는지 확인 (점프 시작 전에만)
            if (_tower != null && _tower.IsUsingSlot(_targetSlot))
            {
                Debug.Log($"[ClimbState] {_character.characterData.characterName}: 목표 슬롯 {_targetSlot}이 선점됨! 현재 위치({currentSlot})에 정착");
                
                // 현재 위치에 해당하는 실제 슬롯 찾기
                Vector3 currentPos = _climbPositions[_curPosIndex];
                int actualCurrentSlot = _tower.GetSlotAtPosition(currentPos);
                
                if (actualCurrentSlot >= 0 && !_tower.IsUsingSlot(actualCurrentSlot))
                {
                    // 현재 위치 슬롯에 정착
                    if (_tower.TryAssignSlot(_character, actualCurrentSlot))
                    {
                        Debug.Log($"[ClimbState] {_character.characterData.characterName}: 슬롯 {actualCurrentSlot}에 정착!");
                        StartResting();
                        return;
                    }
                }
                
                // 정착할 수 없으면 떨어지기
                _character.ChangeState(new InteractionState(_character, false));
                return;
            }
            
            _curPosIndex = nextIndex;
            ReleaseCurSlotJumpNext(currentSlot);
        }

        private void HandleMoving()
        {
            // 안전망: 등반 경로가 비었으면 즉시 복구 시도
            if (_climbPositions == null || _climbPositions.Length == 0)
            {
                if (_sofa != null)
                {
                    int slot = _targetSlot >= 0 ? _targetSlot : 0;
                    _climbPositions = _sofa.GetClimbPositionsForSlot(slot);
                }
                else if (_tower != null)
                {
                    int slot = _targetSlot >= 0 ? _targetSlot : 0;
                    _climbPositions = _tower.GetClimbPositionsForSlot(slot);
                    
                    // 목표 슬롯이 사용중이면 다른 빈 슬롯 찾기
                    if (_climbPositions == null)
                    {
                        int newTargetSlot = FindUsableTargetSlot();
                        if (newTargetSlot != -1)
                        {
                            _targetSlot = newTargetSlot;
                            _climbPositions = _tower.GetClimbPositionsForSlot(newTargetSlot);
                        }
                    }
                }
                if (_climbPositions == null || _climbPositions.Length == 0)
                {
                    _character.ChangeState(new InteractionState(_character, false));
                    return;
                }
            }

            if (_curPosIndex < 0 || _curPosIndex >= _climbPositions.Length)
            {
                _curPosIndex = 0;
            }

            Vector3 targetPos = _climbPositions[_curPosIndex];
            float direction = targetPos.x > _character.transform.position.x ? 1f : -1f;
            _character.SetDirection(direction);

            if (_jumpDelayTimer <= _jumpDelay)
            {
                _jumpDelayTimer += Time.deltaTime;
            }
            else
            {
                MoveTo(targetPos);
            }
        }

        private void CompleteDown()
        {
            if (_nextState != null)
            {
                _character.ChangeState(_nextState);
            }
            else if (_isHungry)
            {
                // 배고픔으로 인한 하강일 때는 무조건 MoveState로 이동
                var foodBowl = _character.FoodBowl;
                if (foodBowl != null && foodBowl.HasFood())
                {
                    int? slot = foodBowl.CheckUseSlotOnArrival(_character);
                    if (slot.HasValue)
                    {
                        _character.ChangeState(new MoveState(_character, foodBowl, slot.Value, new EatState(_character, foodBowl, slot)));
                        return;
                    }
                }
                // 슬롯이 없거나 밥이 없으면 WaitForFoodState로
                var waitState = new WaitForFoodState(_character, foodBowl, _character.IsStarving());
                _character.ChangeState(new MoveState(_character, foodBowl, waitState));
            }
            else
            {
                _character.ChangeState(new WanderState(_character));
            }
        }

        private void ContinueJumpDown()
        {
            _isMoving = true;
            Vector3 targetPos = _climbPositions[_curPosIndex];
            float distance = Vector3.Distance(_character.transform.position, targetPos);
            _moveSpeed = distance / _jumpDuration;
            float direction = targetPos.x > _character.transform.position.x ? 1f : -1f;
            _character.JumpDownAnim(direction);
        }

        private void StartJumpDownCompletion()
        {
            _isDescendCompleted = true;
            _isMoving = false;
            _isPausing = true;
            _curPauseTime = 1.0f;

            _character.IdleFrontAnim();
        }

        private void ReleaseCurSlotJumpNext(int currentSlot)
        {
            if (currentSlot >= 0 && _tower != null && _tower.GetSlotOwner(currentSlot) == _character)
            {
                _tower.ReleaseSlot(_character);
            }
            
            _isMoving = true;
            Vector3 targetPos = _climbPositions[_curPosIndex];
            float distance = Vector3.Distance(_character.transform.position, targetPos);
            _moveSpeed = distance / _jumpDuration;
            float direction = targetPos.x > _character.transform.position.x ? 1f : -1f;
            _character.JumpAnim(direction);
        }

        private void MoveTo(Vector3 targetPos)
        {
            Vector3 moveDirection = (targetPos - _character.transform.position).normalized;
            Vector3 newPosition = _character.transform.position + moveDirection * _moveSpeed * Time.deltaTime;
            
            // 점프 중간에 소파 높이를 넘어가면 뒤로 보내기 (자연스러운 타이밍)
            if (!_isDown && _tower != null && !_hasSwitchedToBackSorting)
            {
                float sofaHeight = WindowManager.Instance.GetCanvasBottomY() + 1.5f; // 대략적인 소파 높이
                if (_character.transform.position.y > sofaHeight)
                {
                    _character.SetTowerClimbingSortingOrder();
                    _hasSwitchedToBackSorting = true;
                }
            }
            
            if (Vector3.Distance(_character.transform.position, targetPos) < 0.1f)
            {
                HandleArrival(targetPos);
            }
            else
            {
                _character.transform.position = newPosition;
            }
        }

        private void HandleArrival(Vector3 targetPos)
        {
            _character.transform.position = targetPos;
            
            if (!_isDown && ReachedTarget() && _targetSlot != -1) 
            {
                // 최종 목표에 도달했을 때 슬롯 할당
                if (!TryUseSlot())
                {
                    return;
                }
                StartResting();
                return;
            }
            
            StartPause(targetPos);
        }

        private bool TryUseSlot()
        {
            if (_tower != null)
            {
                // 목표 슬롯이 사용중인지 확인
                if (_tower.IsUsingSlot(_targetSlot))
                {
                    Debug.Log($"[ClimbState] {_character.characterData.characterName}: 목표 슬롯 {_targetSlot}이 이미 사용중!");
                    _character.ChangeState(new InteractionState(_character, false));
                    return false;
                }
                
                return _tower.TryAssignSlot(_character, _targetSlot);
            }
            else if (_sofa != null)
            {
                int slotIndexToUse = _targetSlot >= 0 ? _targetSlot : _curPosIndex;
                if (_sofa.IsUsingSlot(slotIndexToUse))
                {
                    _character.ChangeState(new InteractionState(_character, false));
                    return false;
                }
                
                _sofa.TryAssignSlot(_character, slotIndexToUse);
                return true;
            }

            return false;
        }

        private bool ReachedTarget()
        {
            if (_sofa != null) return true; // 소파는 한 번 도약으로 종료
            
            // 타워: 등반 경로 배열의 마지막에 도달하면 목표 달성
            return _curPosIndex >= _climbPositions.Length - 1;
        }

        private void StartPause(Vector3 targetPos)
        {
            _isMoving = false;
            _isPausing = true;
            _curPauseTime = 0.5f;
            
            float currentDirection = targetPos.x > _character.transform.position.x ? 1f : -1f;
            if (_landTimer <= _landTime)
            {
                _landTimer += Time.deltaTime;
            }
            else
            {
                _character.IdleBackAnim(currentDirection);
                _landTimer = 0f;
            }
        }

        #endregion

        private void StartResting()
        {
            if (_tower != null)
            {
                bool isBack = Random.value < 0.35f;
                
                var lyingState = new LyingState(_character, isBack, true);
                _character.ChangeState(lyingState);
            }
            else if (_sofa != null)
            {
                var sofaState = new SofaState(_character);
                _character.ChangeState(sofaState);
            }
        }

        public override void Exit()
        {
			// 안전망: 상태 종료 시 여전히 타워/소파 사용 중이면 해제 보강
			if (_isDown)
            {
                if (_tower != null && _character.CurTowerSlot.HasValue)
                {
                    _tower.ReleaseSlot(_character);
                }
                if (_sofa != null && _character.CurSofaSlot.HasValue)
                {
                    _sofa.ReleaseSlot(_character);
                }
            }
        }
        

    }
}
