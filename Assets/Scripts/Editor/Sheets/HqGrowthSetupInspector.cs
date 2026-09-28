using BlackHole.Unity;
using UnityEditor;

namespace BlackHole.EditorTools
{
    [CustomEditor(typeof(HqGrowthSetup))]
    internal sealed class HqGrowthSetupInspector : SheetOwnedInspector
    {
        protected override string Tabs => "Growth·Milestones";
    }
}
