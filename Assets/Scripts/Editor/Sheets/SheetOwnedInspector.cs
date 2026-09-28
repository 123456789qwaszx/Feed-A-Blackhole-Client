using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace BlackHole.EditorTools
{
    // 데이터 시트가 채우는 에셋의 Inspector. 값의 원본이 시트이므로 여기서는 읽기만 한다.
    // 에셋마다 작은 하위 클래스가 CustomEditor로 붙는다(한 클래스에 CustomEditor를 여럿 붙일 수 없다).
    internal abstract class SheetOwnedInspector : Editor
    {
        // 이 에셋을 채우는 탭 이름.
        protected abstract string Tabs { get; }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox($"값의 원본은 데이터 시트({Tabs} 탭)다. 시트에서 고친 뒤 BlackHole > Data Sheets에서 가져온다.",
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
