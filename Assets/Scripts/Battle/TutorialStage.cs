using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 새 게임의 첫 화면(튜토리얼 판). 판 규칙은 원래대로이고 처음 놓는 적, 블랙홀 성장, 카메라·블랙홀 크기만 정한다.
    // - 시작 연출 없이 블랙홀 가까이(CameraZoom)에서 머문다. 블랙홀은 원래보다 작게(BlackHoleScale) 보인다.
    // - 빨간 소행성(색 등급 0, 크기 1) 세 개가 블랙홀 둘레에 떠 있다. 공전은 원래대로다. 시간 제한은 없다(GameSession.BeginStaged).
    // - 블랙홀 성장: Level 1 하나가 이정표다(이정표 칸 하나). 소행성 하나를 부술 때마다 게이지가 1/3씩 차고,
    //   셋을 다 부수면 이정표에 닿아 판이 끝난다(달성 연출 → 결산 → 업그레이드). 결산은 진행 상태에 넣지 않는다(ScreenFlow).
    // - 판 시작 로그를 남기지 않아 서버 진행도와 무관하다(ScreenFlow.StartTutorial).
    internal static class TutorialStage
    {
        // 카메라 배율(전장 전체 = 1). 판 시작 연출의 시작 배율과 같다 — 화면 세로 반 폭이 약 3이다.
        public const float CameraZoom = 0.3f;

        // 블랙홀 크기 배율(1 = Level 0의 원래 크기).
        public const float BlackHoleScale = 0.5f;

        private const int RedTier = 0;

        // 소행성 자리(블랙홀 = 원점). 배율 0.3의 화면 안, 블랙홀 둘레에 고르게.
        private static readonly Point2[] AsteroidPositions =
        {
            new(1.9f, 0.8f),
            new(-1.9f, 0.6f),
            new(0.2f, -1.8f),
        };

        public static IReadOnlyList<StagedEnemy> EnemiesFrom(EnemyContent content)
        {
            EnemyDefinition asteroid = AsteroidOf(content);
            var enemies = new StagedEnemy[AsteroidPositions.Length];

            for (int i = 0; i < enemies.Length; i++)
                enemies[i] = new StagedEnemy(asteroid, RedTier, SizeRule.Base, AsteroidPositions[i]);

            return enemies;
        }

        // Level 1에 닿는 EXP = 놓은 소행성을 모두 부순 EXP(빨간 소행성 하나의 EXP × 수). 이정표는 Level 1 하나다.
        // 목표 잔액 0(결산 Gold 없음)·전장 배율 1. 진행 상태에 결산하지 않으므로 콘텐츠의 이정표와 겹치지 않는다.
        public static HqGrowthDefinition GrowthFrom(EnemyContent content)
        {
            long exp = AsteroidOf(content).Tiers[RedTier].Exp;

            if (exp <= 0)
                throw new InvalidOperationException("튜토리얼 소행성(빨강)의 EXP가 0이라 게이지가 차지 않는다.");

            return new HqGrowthDefinition(
                new[] { exp * AsteroidPositions.Length },
                new[] { new HqMilestone(1, 0, HqGrowthDefinition.StartFieldScale) });
        }

        private static EnemyDefinition AsteroidOf(EnemyContent content)
        {
            foreach (EnemyDefinition kind in content.Enemies)
            {
                if (kind.Type == EnemyType.Asteroid)
                    return kind;
            }

            throw new InvalidOperationException("튜토리얼에 쓸 소행성이 콘텐츠에 없다.");
        }
    }
}
