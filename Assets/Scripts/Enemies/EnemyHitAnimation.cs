using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 피격 시 좌우로 흔들리는 짧은(0.3초) 연출의 상태와 계산. Unity 컴포넌트가 아니라 순수 C# 객체다.
    // 살아있는 적마다 하나씩 있지만 스스로 Update를 갖지 않는다 — EnemyView.Synchronize가 매 프레임
    // 살아있는 적을 순회하는 김에 Advance를 같이 불러준다. 적마다 컴포넌트를 붙이고 각자 Update를
    // 돌리던 이전 방식은 흔들리지 않는 대부분의 프레임에도 살아있는 적 수만큼 빈 호출이 쌓였다.
    internal sealed class EnemyHitAnimation
    {
        private const float Duration = 0.3f;
        private const float Angle = 30f;
        private const float Oscillations = 2.5f;

        private float _elapsed = Duration;
        private float _direction = 1f;

        // Enemy.Damaged(Action<Enemy>)에 그대로 구독·해제할 수 있도록 시그니처를 맞춘다.
        public void Play(Enemy enemy)
        {
            _direction = -_direction;
            _elapsed = 0f;
        }

        // 매 프레임 EnemyView가 호출한다. 흔들리는 중이 아니면 계산 없이 바로 반환한다.
        public void Advance(float delta, Transform target)
        {
            if (_elapsed >= Duration)
                return;

            _elapsed += delta;
            float progress = Mathf.Clamp01(_elapsed / Duration);
            float envelope = 1f - progress;
            float angle = Mathf.Sin(progress * Oscillations * Mathf.PI * 2f) * Angle * envelope * _direction;
            target.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
