using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 새 게임의 첫 화면(튜토리얼 판)의 처음 모습. 판 규칙은 원래대로이고 처음 놓는 적과 카메라만 정한다.
    // - 시작 연출 없이 블랙홀 가까이(CameraZoom)에서 머문다.
    // - 빨간 소행성(색 등급 0, 크기 1) 두 개가 블랙홀 양쪽에 떠 있다. 공전은 원래대로다.
    // - 시간 제한이 없다(GameSession.BeginStaged). 판 시작 로그를 남기지 않아 서버 진행도와 무관하다(ScreenFlow.StartTutorial).
    internal static class TutorialStage
    {
        // 카메라 배율(전장 전체 = 1). 판 시작 연출의 시작 배율과 같다 — 화면 세로 반 폭이 약 3이다.
        public const float CameraZoom = 0.3f;

        private const int RedTier = 0;

        // 소행성 자리(블랙홀 = 원점). 배율 0.3의 화면 안, 블랙홀(반지름 약 0.1)에서 충분히 떨어진 양쪽.
        private static readonly Point2[] AsteroidPositions =
        {
            new(1.8f, 0.6f),
            new(-1.6f, -0.9f),
        };

        public static IReadOnlyList<StagedEnemy> EnemiesFrom(EnemyContent content)
        {
            EnemyDefinition asteroid = KindOf(content, EnemyType.Asteroid);
            var enemies = new StagedEnemy[AsteroidPositions.Length];

            for (int i = 0; i < enemies.Length; i++)
                enemies[i] = new StagedEnemy(asteroid, RedTier, SizeRule.Base, AsteroidPositions[i]);

            return enemies;
        }

        private static EnemyDefinition KindOf(EnemyContent content, EnemyType type)
        {
            foreach (EnemyDefinition kind in content.Enemies)
            {
                if (kind.Type == type)
                    return kind;
            }

            throw new InvalidOperationException($"튜토리얼에 쓸 적 종류 '{type}'이(가) 콘텐츠에 없다.");
        }
    }
}
