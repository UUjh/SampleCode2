using UnityEngine;
using Character.Controller;
using Objects;
using Core.Managers;

namespace Character.States
{
    /// <summary>
    /// 캐릭터 이동 상태
    /// - 정적/동적 목표(밥그릇/타워/소파/장난감)까지 X축 기준 이동
    /// - 도착 후 컨텍스트에 따라 다음 상태로 전이
    /// </summary>
    public class MoveState : StateBase
    {
        private Vector3 _target;
        private StateBase _nextState;
        private bool _hasArrived = false;
        private bool _isRunning = false;
        
        // 동적 목표 추적용
        private bool _isDynamicTarget; // 동적 목표인지 여부
        private FoodBowl _targetBowl; // 밥그릇을 향해 이동하는 경우
        public FoodBowl TargetBowl => _targetBowl;  // 목표 밥그릇 반환
        private float _bowlXOffset; // 밥그릇에서의 X 오프셋 (처음에 정하고 유지)
        
        // 타워 목표인지 여부
        private Tower _targetTower;  // 목표 타워
        public Tower TargetTower => _targetTower;  // 목표 타워 반환
        private float _towerXOffset; // 타워워 X 오프셋 (처음에 정하고 유지)

        // 장난감 목표
        private PlacedToy _targetPlacedToy;
        public PlacedToy TargetPlacedToy => _targetPlacedToy;
        private float _placedToyXOffset; // 장난감 X 오프셋 (처음에 정하고 유지)

        private MovingToy _targetMovingToy;
        public MovingToy TargetMovingToy => _targetMovingToy;

        // 소파 목표인지 여부
        private Sofa _targetSofa;
        public Sofa TargetSofa => _targetSofa;

        // 새로운 생성자 (동적 목표 - 타워)
        public MoveState(CharacterController character, Tower targetTower, StateBase nextState) : base(character)
        {
            Debug.Log("[MoveState] - 타워 목표");
            _character = character;
            _targetTower = targetTower;
            
            Vector3 towerPos = targetTower.transform.position;
            Vector3 characterPos = character.transform.position;

            float baseXOffset = 1f;
            float scaledOffset = baseXOffset * DataManager.Instance.GetCurScaleFactor();
            float xOffset = characterPos.x < towerPos.x ? -scaledOffset : scaledOffset;
            _target = new Vector3(towerPos.x + xOffset, WindowManager.Instance.GetCanvasBottomY(), towerPos.z);

            _nextState = nextState;
            _isDynamicTarget = true;
            _towerXOffset = xOffset;
            _targetBowl = null;
            
        }

        // slot 없이 nextState만 받는 생성자 (동적 목표 - 밥그릇)
        public MoveState(CharacterController character, FoodBowl targetBowl, StateBase nextState) : base(character)
        {
            _character = character;
            _targetBowl = targetBowl;
            Vector3 bowlPos = targetBowl.transform.position;
            Vector3 characterPos = character.transform.position;
            float baseXOffset = targetBowl.bowlData.xOffset;
            float scaledOffset = baseXOffset * DataManager.Instance.GetCurScaleFactor();
            float xOffset = characterPos.x < bowlPos.x ? -scaledOffset : scaledOffset;
            _target = new Vector3(bowlPos.x + xOffset, WindowManager.Instance.GetCanvasBottomY(), bowlPos.z);
            _nextState = nextState;
            _isDynamicTarget = true;
            _bowlXOffset = xOffset;
        }

        // 자리 인자와 nextState를 받는 생성자 (동적 목표 - 밥그릇)
        public MoveState(CharacterController character, FoodBowl targetBowl, int slot, StateBase nextState) : base(character)
        {
            _character = character;
            _targetBowl = targetBowl;
            Vector3 bowlPos = targetBowl.transform.position;
            float baseXOffset = targetBowl.bowlData.xOffset;
            float scaledOffset = baseXOffset * DataManager.Instance.GetCurScaleFactor();
            float xOffset = slot == 0 ? -scaledOffset : scaledOffset;
            _target = new Vector3(bowlPos.x + xOffset, WindowManager.Instance.GetCanvasBottomY(), bowlPos.z);
            _nextState = nextState;
            _isDynamicTarget = true;
            _bowlXOffset = xOffset;
        }

        public MoveState(CharacterController character, PlacedToy targetToy, bool isRunning) : base(character)
        {
            _character = character;
            _targetPlacedToy = targetToy;
            Vector3 characterPos = character.transform.position;
            Vector3 toyPos = targetToy.transform.position;
            float baseOffset = 0.5f;
            float scaledOffset = baseOffset * DataManager.Instance.GetCurScaleFactor();
            float xOffset = characterPos.x < toyPos.x ? -scaledOffset : scaledOffset;

            _target = new Vector3(toyPos.x + xOffset, WindowManager.Instance.GetCanvasBottomY(), toyPos.z);
            _isRunning = isRunning;
            _nextState = null; // PlacedToy는 도착하면 바로 ToyState로 전환되므로 nextState 불필요
            _isDynamicTarget = true;
            _placedToyXOffset = xOffset;
        }

        public MoveState(CharacterController character, MovingToy targetToy, bool isRunning) : base(character)
        {
            _character = character;
            _targetMovingToy = targetToy;
            Vector3 toyPos = targetToy.transform.position;
            
            _target = new Vector3(toyPos.x, WindowManager.Instance.GetCanvasBottomY(), toyPos.z);
            _isRunning = isRunning;
            _nextState = null; // MovingToy는 도착하면 바로 ToyState로 전환되므로 nextState 불필요
            _isDynamicTarget = true;
        }

        public MoveState(CharacterController character, Sofa targetSofa, StateBase nextState) : base(character)
        {
            _character = character;
            _targetSofa = targetSofa;
            Vector3 sofaPos = targetSofa.transform.position;

            _target = new Vector3(sofaPos.x, WindowManager.Instance.GetCanvasBottomY(), sofaPos.z);
            _nextState = nextState;
            _isDynamicTarget = true;
        }

        public override void Enter()
        {
            _hasArrived = false;
            _character.WalkingAnim(Mathf.Sign(_target.x - _character.transform.position.x));
        }

        public override void Update()
        {
            // 동적 목표물의 위치가 변경되었다면 목표 위치 업데이트
            if (_isDynamicTarget)
            {
                if (_targetBowl != null)
                {
                    Vector3 bowlPos = _targetBowl.transform.position;
                    _target = new Vector3(bowlPos.x + _bowlXOffset, WindowManager.Instance.GetCanvasBottomY(), 0);
                }
                else if (_targetTower != null)
                {
                    Vector3 towerPos = _targetTower.transform.position;
                    _target = new Vector3(towerPos.x + _towerXOffset, WindowManager.Instance.GetCanvasBottomY(), 0);
                }
                else if (_targetMovingToy != null)
                {
                    Vector3 toyPos = _targetMovingToy.transform.position;
                    _target = new Vector3(toyPos.x, WindowManager.Instance.GetCanvasBottomY(), 0);
                }
                else if (_targetSofa != null)
                {
                    Vector3 sofaPos = _targetSofa.transform.position;
                    _target = new Vector3(sofaPos.x, WindowManager.Instance.GetCanvasBottomY(), 0);
                }
            }
            
            if (!_hasArrived)
            {
                // 목표물 쪽으로 방향 계산
                float dir = (_target.x - _character.transform.position.x) > 0 ? 1f : -1f;
                if (_character.GetDirection() != dir)
                    _character.SetDirection(dir);

                _character.MoveTowards(_target, _isRunning ? _character.characterData.runSpeed : _character.characterData.speed);

                if (_targetBowl != null && _targetBowl.IsDragging)
                    return;
                if (_targetTower != null && _targetTower.IsDragging)
                    return;
                if (_targetSofa != null && _targetSofa.IsDragging)
                    return;

                if (Mathf.Abs(_character.transform.position.x - _target.x) < 0.05f)
                {
                    _hasArrived = true;
                    
                    // 목표 방향으로 Idle 애니메이션
                    float targetDirection = _target.x > _character.transform.position.x ? 1f : -1f;

                    if (_targetTower != null)
                    {
                        _character.IdleBackAnim(targetDirection);
                    }
                    else if (_targetPlacedToy != null)  // PlacedToy에 도착하면 바로 장난감 상태로 전환
                    {
                        _character.ChangeState(new ToyState(_character, _targetPlacedToy.ToyData));
                        return;
                    }
                    else if (_targetMovingToy != null)  // MovingToy에 도착하면 바로 장난감 상태로 전환
                    {
                        _character.ChangeState(new ToyState(_character, _targetMovingToy.ToyData));
                        return;
                    }
                    else if (_targetSofa != null)
                    {
                        _character.IdleBackAnim(targetDirection);
                        return;
                    }
                    else if (_targetBowl != null)
                    {
                        
                    }
                    else
                    {
                        _character.IdleFrontAnim(targetDirection);
                    }
                }
            }
            else
            {
                if (_targetBowl != null && _targetBowl.HasFood()) 
                {
                    int? slot = _targetBowl.CheckUseSlotOnArrival(_character);
                    if (slot.HasValue) 
                    {
                        float dx = _character.transform.position.x - _targetBowl.GetEatingPos(slot.Value).x;
                        if (Mathf.Abs(dx) < 0.1f) 
                        {
                            _character.ChangeState(new EatState(_character, _targetBowl, slot));
                        } 
                        else 
                        {
                            _character.ChangeState(new MoveState(_character, _targetBowl, slot.Value, new EatState(_character, _targetBowl, slot)));
                        }
                        return;
                    }
                }
                // 슬롯이 없거나 타겟이 없으면 DecisionState로 전환
                if (_nextState != null)
                {
                    _character.ChangeState(_nextState);
                }
                else
                {
                    _character.ChangeState(new DecisionState(_character));
                }
            }
        }

        public override void Exit()
        {
            _isRunning = false;
            // _character.IdleFrontAnim();
        }
    }
}