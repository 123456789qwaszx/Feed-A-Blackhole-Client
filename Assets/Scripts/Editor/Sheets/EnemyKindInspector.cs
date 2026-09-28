using BlackHole.Unity;
using UnityEditor;

namespace BlackHole.EditorTools
{
    // 종류의 ID(시트와 짝짓는 열쇠)와 스프라이트는 에셋이 원본이다. 새 종류는 에셋을 만들어 ID·스프라이트를 넣고 적 종류 목록에 더한다.
    [CustomEditor(typeof(EnemyKind))]
    internal sealed class EnemyKindInspector : SheetOwnedInspector
    {
        protected override string Tabs => "Enemies·EnemyTiers·EnemyStageColors·EnemyMassLevels";
        protected override string[] AssetOwned => new[] { "id", "sprite" };
    }
}
