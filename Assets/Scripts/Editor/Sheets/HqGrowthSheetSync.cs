using System.IO;
using System.Text;
using BlackHole.Authoring;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;

namespace BlackHole.EditorTools
{
    // 성장도 표의 Unity 쪽: 시트 읽기(HqGrowthSheet)를 통과한 값을 블랙홀 성장 설정 에셋에 쓰고, 에셋을 CSV 파일로 낸다.
    internal static class HqGrowthSheetSync
    {
        public const string StagesFile = HqGrowthSheet.StagesTab + ".csv";
        public const string MilestonesFile = HqGrowthSheet.MilestonesTab + ".csv";

        public static HqGrowthSetup FindSetup()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(HqGrowthSetup));
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<HqGrowthSetup>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // 통과하면 에셋에 쓰고 저장한다. 값이 지금과 같으면 쓰지 않는다. 에셋을 바꿨으면 true다.
        public static bool Apply(HqGrowthSetup setup, string stagesCsv, string milestonesCsv, out HqGrowthSheetResult result)
        {
            result = HqGrowthSheet.Read(stagesCsv, milestonesCsv);

            if (!result.Succeeded || SameAs(setup.ToData(), result.Data))
                return false;

            Undo.RecordObject(setup, "데이터 시트 가져오기");
            setup.Replace(result.Data);
            EditorUtility.SetDirty(setup);
            AssetDatabase.SaveAssetIfDirty(setup);
            return true;
        }

        public static void Export(HqGrowthSetup setup, string folder)
        {
            HqGrowthData data = setup.ToData();
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(folder, StagesFile), HqGrowthSheet.StagesCsv(data), utf8);
            File.WriteAllText(Path.Combine(folder, MilestonesFile), HqGrowthSheet.MilestonesCsv(data), utf8);
        }

        private static bool SameAs(HqGrowthData a, HqGrowthData b) =>
            HqGrowthSheet.StagesCsv(a) == HqGrowthSheet.StagesCsv(b) && HqGrowthSheet.MilestonesCsv(a) == HqGrowthSheet.MilestonesCsv(b);
    }
}
