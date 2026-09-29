using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace BlackHole.EditorTools
{
    // 데이터 시트가 채우는 에셋의 Inspector. 값의 원본은 시트지만 Inspector에서도 모든 칸을 고칠 수 있다(스크립트 칸은 빼고).
    // 여기서 고친 값은 다음 가져오기 때 시트 값으로 덮이므로, 남기려면 CSV로 내보내 시트에 옮긴다.
    // 하위 클래스가 Editable을 거짓으로 두면 에셋이 원본인 칸(AssetOwned)만 고칠 수 있다.
    // 에셋마다 작은 하위 클래스가 CustomEditor로 붙는다(한 클래스에 CustomEditor를 여럿 붙일 수 없다).
    internal abstract class SheetOwnedInspector : Editor
    {
        // 이 에셋을 채우는 탭 이름.
        protected abstract string Tabs { get; }

        // 시트가 아니라 이 에셋이 원본인 칸의 직렬화 이름.
        protected virtual string[] AssetOwned => Array.Empty<string>();

        // 참이면 시트가 원본인 칸도 Inspector에서 고칠 수 있다(스크립트 칸은 빼고).
        protected virtual bool Editable => true;

        public override VisualElement CreateInspectorGUI()
        {
            string[] owned = AssetOwned;
            string note = Editable
                ? " 여기서 고친 값은 다음 가져오기 때 시트 값으로 덮인다. 남기려면 CSV로 내보내 시트에 옮긴다."
                : owned.Length == 0 ? string.Empty : $" 이 에셋에서는 {string.Join(", ", owned)}만 고친다.";

            var root = new VisualElement();
            root.Add(new HelpBox($"값의 원본은 데이터 시트({Tabs} 탭)다. 시트에서 고친 뒤 BlackHole > Data Sheets에서 가져온다.{note}",
                HelpBoxMessageType.Info));
            root.Add(new Button(DataSheetWindow.Open) { text = "Data Sheets 열기" });

            var fields = new VisualElement();
            fields.style.marginTop = 6;
            root.Add(fields);

            SerializedProperty property = serializedObject.GetIterator();

            for (bool more = property.NextVisible(true); more; more = property.NextVisible(false))
            {
                var field = new PropertyField(property.Copy());
                bool script = property.propertyPath == "m_Script";
                field.SetEnabled(!script && (Editable || Array.IndexOf(owned, property.name) >= 0));
                fields.Add(field);
            }

            return root;
        }
    }
}
