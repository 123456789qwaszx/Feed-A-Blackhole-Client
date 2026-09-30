using UnityEngine;

namespace BlackHole.Unity
{
    public readonly struct BattleStepResult
    {
        // 이번 Tick에서 전투가 종료되었는가?
        public bool BattleEnded { get; }

        // 이번 Tick에서 발생한 레벨업 횟수
        public int Raised { get; }

        public BattleStepResult(bool battleEnded, int raised)
        {
            BattleEnded = battleEnded;
            Raised = raised;
        }
    }
}

