using BlackHole.Unity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace BlackHole.EditorTools
{
    // 블랙홀 성장 설정의 Inspector. 값의 원본이 데이터 시트이므로 여기서는 읽기만 한다.
    [CustomEditor(typeof(HqGrowthSetup))]
    internal sealed class HqGrowthSetupEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("값의 원본은 데이터 시트(Growth·Milestones 탭)다. 시트에서 고친 뒤 BlackHole > Data Sheets에서 가져온다.",
                HelpBoxMessageType.Info));
            root.Add(new Button(DataSheetWindow.Open) { text = "Data Sheets 열기" });

            var fields = new VisualElement();
            fields.style.marginTop = 6;
            InspectorElement.FillDefaultInspector(fields, serializedObject, this);
            fields.SetEnabled(false);
            root.Add(fields);
            return root;
        }
    }
}
