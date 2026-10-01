using System;

namespace BlackHole.Core
{
    // 특수 성질 하나의 정의: 성질 ID와 사망 효과.
    // 성질은 종류가 아니라 출현 때 한 마리에 붙는 특성이다(전기 소행성 = 전기 성질이 붙은 소행성). 종류(EnemyDefinition)가 붙을 수 있는 성질 목록을 가진다.
    // - 출현 때 종류의 성질마다 생성 확률(노드 enemy.<종류>.trait.<성질>.chance, %)로 하나를 고른다. 기본 0% — 노드를 사야 붙는다.
    // - 한 마리에 성질은 최대 하나다(배타). 한 종류의 성질 확률 합은 100%를 넘을 수 없다.
    // - 성질이 붙은 적은 수치(색·질량·크기)를 종류에서 그대로 받는다. 수치를 바꾸는 성질은 황금(Gold 배율)뿐이다.
    // - 성질이 붙은 적은 특수 적이다: 사망 때 성질의 효과가 발동하고, 사망 효과의 피해를 받지 않는다(DeathEffects).
    public sealed class EnemyTraitDefinition
    {
        public string Id { get; }
        public DeathEffectDefinition Effect { get; }

        public EnemyTraitDefinition(string id, DeathEffectDefinition effect)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("성질 ID가 비어 있다.", nameof(id));

            Id = id;
            Effect = effect ?? throw new ArgumentNullException(nameof(effect), "성질에는 사망 효과가 있어야 한다.");
        }

        // 이 판의 노드가 반영된 성질. 효과의 수치만 바뀌고 ID는 같다.
        internal EnemyTraitDefinition With(DeathEffectDefinition effect) => new EnemyTraitDefinition(Id, effect);
    }
}
