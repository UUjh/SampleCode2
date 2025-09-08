using UnityEngine;
using Character.Controller;

namespace Character.States
{
    /// <summary>
    /// 의사결정 상태
    /// - 배고픔 우선 처리 후, 확률 기반으로 타워/장난감/랜덤 상태 전이
    /// - 일정 시간 대기 후 다음 행동 결정
    /// </summary>
    public class DecisionState : StateBase
    {
        private float _decisionTimer;
        private float _decisionTime;
        private float _maxTime = 3f;

        private float _towerChance = 0.1f;
        private float _placedToyChance = 0.1f;
        private float _movingToyChance = 0.1f;
        public DecisionState(CharacterController character, bool isRandom = true) : base(character) 
        { 
            if (isRandom)
            {
                _decisionTime = Random.Range(0f, _maxTime);
            } 
            else 
            {
                _decisionTime = 5f;
            }
        }

        public override void Enter()
        {
            _decisionTimer = 0f;
            _character.IdleFrontAnim();
        }

        public override void Update()
        {
            // 배고픔 상태 우선 체크
            if (CheckHunger())
            {
                return;
            }
            
            if (UpdateTimer(ref _decisionTimer, _decisionTime))
            {
                // 배고픔 상태 확인
                if (CheckHunger())
                {
                    return;
                }

                // 타워 확인 (구매했을 때만)
                if (CanUseTower() && CheckProbability(_towerChance)) // 10% 확률로 타워 사용
                {
                    GoTower();
                    return;
                }

                if (CheckProbability(_placedToyChance))
                {
                    GoPlacedToy();
                    return;
                }

                if (CheckProbability(_movingToyChance))
                {
                    GoMovingToy();
                    return;
                }

                RandomState
                (
                    idleChance: 25,      
                    wanderChance: 40,    
                    lyingChance: 20,     
                    yawnChance: 15,      
                    decisionChance: 0  
                );
            }
        }

        public override void Exit()
        {
            
        }
    }
}