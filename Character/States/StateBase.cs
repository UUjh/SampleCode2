using UnityEngine;
using Character.Controller;
using Core.Managers;
using Core.Data;

namespace Character.States
{
    /// <summary>
    /// 캐릭터의 상태를 관리하는 기본 클래스
    /// 모든 상태 클래스는 이 클래스를 상속받아야 함
    /// </summary>
    public abstract class StateBase
    {
        /// <summary>
        /// 현재 상태의 캐릭터 컨트롤러
        /// </summary>
        protected CharacterController _character;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="character">캐릭터 컨트롤러</param>
        public StateBase(CharacterController character)
        {
            _character = character;
        }

        /// <summary>
        /// 상태에 진입할 때 호출되는 메서드
        /// </summary>
        public virtual void Enter() { }

        /// <summary>
        /// 매 프레임마다 호출되는 상태 갱신 메서드
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// 상태에서 벗어날 때 호출되는 메서드
        /// </summary>
        public virtual void Exit() { }

        #region 공통 유틸리티 메서드들
        
        /// <summary>
        /// 타이머 업데이트
        /// </summary>
        /// <param name="timer">업데이트할 타이머 참조</param>
        /// <param name="targetTime">목표 시간</param>
        /// <returns>목표 시간에 도달했는지 여부</returns>
        protected bool UpdateTimer(ref float timer, float targetTime)
        {
            timer += Time.deltaTime;
            if (timer >= targetTime)
            {
                timer = 0f; // 자동으로 0으로 초기화
                return true;
            }
            return false;
        }
        
        // 확률 체크
        protected bool CheckProbability(float probability)
        {
            return Random.value < probability;
        }

        /// <summary>
        /// 캐릭터 성격에 따른 시간 조정
        /// </summary>
        /// <param name="baseTime">기본 시간</param>
        /// <returns>성격에 따라 조정된 시간</returns>
        protected float AdjustedTime(float baseTime)
        {
            if (DataManager.Instance == null) return baseTime;
            
            var personality = DataManager.Instance.CharacterSettings.personality;
            switch (personality)
            {
                case CharacterPersonality.Active:
                    return baseTime; // 활발함: 기본 시간 유지
                case CharacterPersonality.Normal:
                    return baseTime * 2f; // 보통: 2배 증가
                case CharacterPersonality.Lazy:
                    return baseTime * 5f; // 게으름: 5배 증가
                default:
                    return baseTime;
            }
        }

        #endregion
        
        #region 배고픔 체크 공통 메서드
        
        // 배고픔 상태 체크 및 적절한 행동 결정
        protected bool CheckHunger(float forceChance = -1f)
        {
            if (!_character.IsHungry()) return false;
            
            float hungerChance = forceChance >= 0f ? forceChance : (_character.IsStarving() ? 1.0f : 0.5f);
            
            if (Random.value < hungerChance)
            {
                // 실제 점유 여부 확인(슬롯 보유 기준) 후 하강 결정
                if (_character.Tower != null && _character.CurTowerSlot.HasValue)
                {
                    _character.ChangeState(new ClimbState(_character, _character.Tower, true, true));
                }
                else if (_character.Sofa != null && _character.CurSofaSlot.HasValue)
                {
                    _character.ChangeState(new ClimbState(_character, _character.Sofa, true, true));
                }
                else
                {
                    _character.ChangeState(new MoveState(_character, _character.FoodBowl, new WaitForFoodState(_character, _character.FoodBowl, _character.IsStarving())));
                }
                return true;
            }
            
            return false;
        }
        
        #endregion
        
        #region 오브젝트 접근 메서드들
        
        // 타워 사용 가능 여부 확인
        protected bool CanUseTower()
        {
            // 스마트 타워 선택: 거리, 빈 슬롯 수 등을 종합 고려
            var bestTower = ObjectManager.Instance.FindBestTower(_character);
            return bestTower != null;
        }
        
        // 타워로 이동
        public void GoTower()
        {
            // 스마트 타워 선택: 거리, 빈 슬롯 수 등을 종합 고려
            var tower = ObjectManager.Instance.FindBestTower(_character);
            if (tower == null) return;

            if (_character.CurSofaSlot.HasValue && _character.Sofa != null)
            {
                var nextState = new MoveState(_character, tower, new ClimbState(_character, tower));
                _character.ChangeState(new ClimbState(_character, _character.Sofa, true, nextState));
            }
            else
            {
                _character.ChangeState(new MoveState(_character, tower, new ClimbState(_character, tower)));
            }
        }
        
        // 밥그릇으로 이동
        public void GoFoodBowl()
        {
            var foodBowl = _character.FoodBowl;
            if (foodBowl == null)
            {
                return;
            }

            bool isStarving = _character.IsStarving();

            // 사용 중 설비가 있으면 먼저 내려온 뒤 밥그릇으로 이동
            if (_character.CurTowerSlot.HasValue && _character.Tower != null)
            {
                var nextAfterDown = new MoveState(_character, foodBowl, new WaitForFoodState(_character, foodBowl, isStarving));
                _character.ChangeState(new ClimbState(_character, _character.Tower, true, nextAfterDown));
                return;
            }
            if (_character.CurSofaSlot.HasValue && _character.Sofa != null)
            {
                var nextAfterDown = new MoveState(_character, foodBowl, new WaitForFoodState(_character, foodBowl, isStarving));
                _character.ChangeState(new ClimbState(_character, _character.Sofa, true, nextAfterDown));
                return;
            }

            // 슬롯/밥 체크 후 EatState/MoveState 우선 진입
            if (foodBowl.HasFood())
            {
                int? slot = foodBowl.CheckUseSlotOnArrival(_character);
                if (slot.HasValue)
                {
                    Vector3 slotPosition = foodBowl.GetEatingPos(slot.Value);
                    float dx = _character.transform.position.x - slotPosition.x;
                    if (Mathf.Abs(dx) < 0.1f)
                    {
                        _character.ChangeState(new EatState(_character, foodBowl, slot));
                    }
                    else
                    {
                        _character.ChangeState(new MoveState(_character, foodBowl, slot.Value, new EatState(_character, foodBowl, slot)));
                    }
                    return;
                }
            }
            // 슬롯이 없거나 밥이 없으면 WaitForFoodState로
            var waitState = new WaitForFoodState(_character, foodBowl, isStarving);
            _character.ChangeState(new MoveState(_character, foodBowl, waitState));
        }
        
        protected void GoPlacedToy()
        {
            var placedToy = _character.PlacedToy;
            if (placedToy == null || placedToy.ToyData == null || placedToy.IsDragging) return;

            if (_character.CurTowerSlot.HasValue && _character.Tower != null)
            {
                _character.ChangeState(new ClimbState(_character, _character.Tower, true, new MoveState(_character, placedToy, true)));
                return;
            }
            if (_character.CurSofaSlot.HasValue && _character.Sofa != null)
            {
                _character.ChangeState(new ClimbState(_character, _character.Sofa, true, new MoveState(_character, placedToy, true)));
                return;
            }
            _character.ChangeState(new MoveState(_character, placedToy, true));
        }

        protected void GoMovingToy()
        {
            var movingToy = _character.MovingToy;
            if (movingToy == null || movingToy.ToyData == null || movingToy.IsDragging) return;
            
            if (_character.CurTowerSlot.HasValue && _character.Tower != null)
            {
                _character.ChangeState(new ClimbState(_character, _character.Tower, true, new MoveState(_character, movingToy, true)));
                return;
            }
            if (_character.CurSofaSlot.HasValue && _character.Sofa != null)
            {
                _character.ChangeState(new ClimbState(_character, _character.Sofa, true, new MoveState(_character, movingToy, true)));
                return;
            }
            _character.ChangeState(new MoveState(_character, movingToy, true));
        }
        protected bool CanUseSofa()
        {
            // 스마트 소파 선택: 거리, 빈 슬롯 수 등을 종합 고려
            var bestSofa = ObjectManager.Instance.FindBestSofa(_character);
            return bestSofa != null;
        }

        // 소파로 이동
        public void GoSofa()
        {
            // 스마트 소파 선택: 거리, 빈 슬롯 수 등을 종합 고려
            var sofa = ObjectManager.Instance.FindBestSofa(_character);
            if (sofa == null) return;

            if (_character.CurTowerSlot.HasValue && _character.Tower != null)
            {
                var nextState = new MoveState(_character, sofa, new ClimbState(_character, sofa));
                _character.ChangeState(new ClimbState(_character, _character.Tower, true, nextState));
            }
            else
            {
                _character.ChangeState(new MoveState(_character, sofa, new ClimbState(_character, sofa)));
            }
        }
        
        protected void RandomState(
            int idleChance = 25, 
            int wanderChance = 25, 
            int lyingChance = 25, 
            int yawnChance = 0, 
            int decisionChance = 25)
        {
            // 배고픔 상태 우선 체크 - 굶주리면 무조건 밥그릇으로!
            if (_character.IsStarving())
            {
                GoFoodBowl();
                return;
            }
            
            // 일반 배고픔 상태 체크
            if (_character.IsHungry())
            {
                if (Random.value < 0.3f)
                {
                    GoFoodBowl();
                    return;
                }
            }
            
            // 확률 정규화 (총합이 100이 아닐 수 있으므로)
            int totalChance = idleChance + wanderChance + lyingChance + yawnChance + decisionChance;
            if (totalChance <= 0) totalChance = 1; // 0으로 나누기 방지
            
            int randomValue = Random.Range(0, totalChance);
            int currentSum = 0;
            
            // IdleState
            currentSum += idleChance;
            if (randomValue < currentSum)
            {
                bool isBack = CheckProbability(0.35f);
                _character.ChangeState(new IdleState(_character, isBack));
                return;
            }
            
            // WanderState
            currentSum += wanderChance;
            if (randomValue < currentSum)
            {
                _character.ChangeState(new WanderState(_character));
                return;
            }
            
            // LyingState
            currentSum += lyingChance;
            if (randomValue < currentSum)
            {
                bool isBackLying = CheckProbability(0.35f);
                _character.ChangeState(new LyingState(_character, isBackLying));
                return;
            }
            
            // YawnState
            currentSum += yawnChance;
            if (randomValue < currentSum)
            {
                _character.ChangeState(new YawnState(_character));
                return;
            }
            
            // DecisionState (기본값)
            _character.ChangeState(new DecisionState(_character));
        }
        
        #endregion
    }
}