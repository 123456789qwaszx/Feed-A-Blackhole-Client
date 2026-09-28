using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 현재 아래 값은 모두 [임시].
    // 스킬 설정 에셋: 스킬 종류마다 기본 수치 칸을 따로 둔다(한 칸에 모든 종류의 수치를 섞지 않는다).
    // 칸의 값은 Core의 저작 형식(BreakerData)으로 옮겨져 ContentLoader가 검증한다.
    // 값의 원본은 데이터 시트(Skills 탭)이고, 이 에셋은 가져오기가 채운다.
    [CreateAssetMenu(fileName = "SkillSetup", menuName = "BlackHole/Skill Setup")]
    public sealed class SkillSetup : ScriptableObject
    {
        [Header("Breaker: 조준점 중심 원 안의 적 전부를 주기마다 친다")]
        [SerializeField] private float breakerDamage = 2;
        [Tooltip("공격 주기(초).")]
        [SerializeField] private float breakerInterval = 1;
        [Tooltip("공격 원의 반지름. 화면의 범위 표시도 이 값이다.")]
        [SerializeField] private float breakerRadius = 1.5f;
        [Tooltip("한 Tick이 치명타일 확률(0 ~ 1).")]
        [SerializeField] private float breakerCritChance;
        [Tooltip("치명타 Tick의 피해 배율(1 이상). 혜성의 확정 치명타도 이 배율을 쓴다.")]
        [SerializeField] private float breakerCritMultiplier = 2;

        [Header("관통 레이저: 경계 원 위의 무작위 지점에서 조준점을 향해 예고한 뒤 관통한다")]
        [SerializeField] private float laserDamage = 3;
        [Tooltip("예고를 시작하는 주기(초).")]
        [SerializeField] private float laserInterval = 4;
        [Tooltip("발사선의 굵기. 선에서 굵기의 절반 안에 있는 적이 맞는다.")]
        [SerializeField] private float laserWidth = 0.5f;
        [Tooltip("예고가 보이는 시간(초). 예고가 끝나는 순간 발사한다.")]
        [SerializeField] private float laserTelegraphDuration = 0.4f;
        [Tooltip("시작점이 놓이는 경계 원의 반지름(HQ 중심). 시작점이 화면 밖에 있도록 화면을 덮는 값을 쓴다.")]
        [SerializeField] private float laserBoundaryRadius = 11;

        // WriteTo의 반대. 데이터 시트 가져오기(메뉴 BlackHole > Data Sheets)만 부른다.
        internal void Replace(BreakerData breaker, LaserData laser)
        {
            breakerDamage = breaker.Damage;
            breakerInterval = breaker.Interval;
            breakerRadius = breaker.Radius;
            breakerCritChance = breaker.CritChance;
            breakerCritMultiplier = breaker.CritMultiplier;
            laserDamage = laser.Damage;
            laserInterval = laser.Interval;
            laserWidth = laser.Width;
            laserTelegraphDuration = laser.TelegraphDuration;
            laserBoundaryRadius = laser.BoundaryRadius;
        }

        public void WriteTo(ContentData data)
        {
            data.Breaker = new BreakerData
            {
                Damage = breakerDamage,
                Interval = breakerInterval,
                Radius = breakerRadius,
                CritChance = breakerCritChance,
                CritMultiplier = breakerCritMultiplier,
            };
            data.Laser = new LaserData
            {
                Damage = laserDamage,
                Interval = laserInterval,
                Width = laserWidth,
                TelegraphDuration = laserTelegraphDuration,
                BoundaryRadius = laserBoundaryRadius,
            };
        }
    }
}
