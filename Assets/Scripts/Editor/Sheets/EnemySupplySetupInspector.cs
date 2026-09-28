using BlackHole.Unity;
using UnityEditor;

namespace BlackHole.EditorTools
{
    [CustomEditor(typeof(EnemySupplySetup))]
    internal sealed class EnemySupplySetupInspector : SheetOwnedInspector
    {
        protected override string Tabs => "Supply·StartSupply";
    }
}
