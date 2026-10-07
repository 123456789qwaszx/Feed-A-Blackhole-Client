namespace BlackHole.Unity
{
    // 전투 한 Step의 결과(BattleSystem.Tick). 진행 중인 판이 없으면 기본값(끝나지 않음, Level업 0)이다.
    public readonly struct BattleStepResult
    {
        // 이번 Step에 판이 끝났는가.
        public bool BattleEnded { get; }

        // 이번 Step에 오른 블랙홀 Level 수.
        public int Raised { get; }

        public BattleStepResult(bool battleEnded, int raised)
        {
            BattleEnded = battleEnded;
            Raised = raised;
        }
    }
}
