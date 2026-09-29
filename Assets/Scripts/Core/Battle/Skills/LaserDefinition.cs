namespace BlackHole.Core
{
    public sealed class LaserDefinition
    {
        public float Damage { get; }

        // 예고를 시작하는 주기(초).
        public float Interval { get; }

        // 발사선의 굵기. 선에서 굵기의 절반 안에 원(반지름 = 적의 크기)이 닿는 적이 맞는다. 화면의 발사선도 이 굵기.
        public float Width { get; }

        // 예고가 보이는 시간(초). 예고가 끝나는 순간 발사.
        public float TelegraphDuration { get; }

        // 시작점이 놓이는 경계 원의 반지름. 공간 값이라 노드가 바꾸는 수치가 아님.
        public float BoundaryRadius { get; }

        public LaserDefinition(float damage, float interval, float width, float telegraphDuration, float boundaryRadius)
        {
            Damage = DefinitionGuard.Positive(damage, nameof(damage));
            Interval = DefinitionGuard.Positive(interval, nameof(interval));
            Width = DefinitionGuard.Positive(width, nameof(width));
            TelegraphDuration = DefinitionGuard.Positive(telegraphDuration, nameof(telegraphDuration));
            BoundaryRadius = DefinitionGuard.Positive(boundaryRadius, nameof(boundaryRadius));
        }
    }
}
