namespace BlackHole.Core
{
    // 이정표 하나: 이 성장도에 닿으면 받는다. 그 앞 성장도의 판이 목표 Level에 닿는 순간 판이 끝나고, 결산이 그 판에서 번 Gold 대신 이 보상을 준다.
    // 금액은 기획자가 정한다 [사용자].
    public sealed class HqMilestone
    {
        public int Stage { get; }
        public long Reward { get; }

        public HqMilestone(int stage, long reward)
        {
            Stage = stage;
            Reward = DefinitionGuard.NotNegative(reward, nameof(reward));
        }
    }
}
