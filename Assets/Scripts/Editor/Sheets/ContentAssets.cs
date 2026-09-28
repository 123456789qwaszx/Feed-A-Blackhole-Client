using BlackHole.Unity;
using UnityEditor;
using UnityEngine;

namespace BlackHole.EditorTools
{
    // 데이터 시트가 채우거나 검사에 쓰는 콘텐츠 에셋. 종류마다 프로젝트에 하나라고 보고 처음 찾은 것을 쓴다.
    internal sealed class ContentAssets
    {
        public HqGrowthSetup Growth { get; private set; }
        public SkillSetup Skills { get; private set; }
        public EnemySupplySetup Supply { get; private set; }
        public EnemyCatalog Enemies { get; private set; }
        public NodeCatalog Nodes { get; private set; }

        public static ContentAssets Find() => new ContentAssets
        {
            Growth = Load<HqGrowthSetup>(),
            Skills = Load<SkillSetup>(),
            Supply = Load<EnemySupplySetup>(),
            Enemies = Load<EnemyCatalog>(),
            Nodes = Load<NodeCatalog>(),
        };

        // 빠진 에셋이 있으면 그 이름들, 없으면 null.
        public string Missing()
        {
            string missing = string.Empty;

            if (Growth == null) missing += " HqGrowthSetup";
            if (Skills == null) missing += " SkillSetup";
            if (Supply == null) missing += " EnemySupplySetup";
            if (Enemies == null) missing += " EnemyCatalog";
            if (Nodes == null) missing += " NodeCatalog";

            return missing.Length == 0 ? null : "프로젝트에 콘텐츠 에셋이 없다:" + missing;
        }

        private static T Load<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
