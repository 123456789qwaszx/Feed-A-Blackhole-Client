using BlackHole.Unity;
using UnityEditor;

namespace BlackHole.EditorTools
{
    [CustomEditor(typeof(SkillSetup))]
    internal sealed class SkillSetupInspector : SheetOwnedInspector
    {
        protected override string Tabs => "Skills";
    }
}
