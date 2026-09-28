namespace BlackHole.Core
{
    // 관통 레이저의 공유 정의: 기본 수치(CONTENT_DEFINITION 2.3). 콘텐츠에 없으면 판에 레이저가 없다.
    // 주기마다 경계 원 위의 무작위 지점에서 조준점을 향해 예고하고, 예고가 끝나면 그 직선 위의 적 전부를 관통한다.
    public sealed class LaserDefinition
    {
        public float Damage { get; }
        // 예고를 시작하는 주기(초).
        public float Interval { get; }
        // 발사선의 굵기. 선에서 굵기의 절반 안에 있는 적이 맞는다. 화면의 발사선도 이 굵기다.
        public float Width { get; }
        // 예고가 보이는 시간(초). 예고가 끝나는 순간 발사한다.
        public float TelegraphDuration { get; }
        // 시작점이 놓이는 경계 원의 반지름(HQ 중심). 공간 값이라 노드가 바꾸는 수치가 아니다.
        // 시작점이 화면 밖에 있으려면 화면을 모두 덮어야 한다([미정], CONTENT_DEFINITION 2.3).
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
