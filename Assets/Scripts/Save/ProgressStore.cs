using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 진행 저장의 창구. 앱 시작 때 콘텐츠를 불러온 뒤 한 번 불러 검사한다.
    // 불러오기: 본 파일 → 읽지 못하거나 깨졌으면 보관하고 직전 저장 → 그것도 그렇으면 보관하고 이어 할 저장이 없다.
    public sealed class ProgressStore
    {
        private readonly ProgressSaveFile _file;

        // 이어 할 저장. 없으면 null.
        public SavedProgress Saved { get; private set; }

        private ProgressStore(ProgressSaveFile file)
        {
            _file = file;
        }

        public static ProgressStore Load(string directory, NodeContent nodes, HqGrowthDefinition growth)
        {
            var file = new ProgressSaveFile(directory);
            var store = new ProgressStore(file);

            if (file.TryReadCurrent(out ProgressSaveData current) && store.TryUse(current, "본 저장", nodes, growth))
                return store;

            LogBackup(file.MoveCurrentToBackup());

            if (file.TryReadPrevious(out ProgressSaveData previous) && store.TryUse(previous, "직전 저장", nodes, growth))
                return store;

            LogBackup(file.MovePreviousToBackup());
            return store;
        }

        private bool TryUse(ProgressSaveData data, string which, NodeContent nodes, HqGrowthDefinition growth)
        {
            ProgressLoadResult result = ProgressSave.Load(data, nodes, growth);

            if (result.Diagnostics.Count > 0)
                Debug.LogError(Lines($"[저장] {which}이 깨졌다.", result.Diagnostics));

            if (result.Adjustments.Count > 0)
                Debug.LogWarning(Lines($"[저장] {which}을 지금 콘텐츠에 맞춰 썼다. 파일에는 원래 값이 남는다.", result.Adjustments));

            Saved = result.Progress;
            return result.Succeeded;
        }

        private static string Lines(string title, IReadOnlyList<ContentDiagnostic> diagnostics) =>
            title + "\n  " + string.Join("\n  ", diagnostics);

        private static void LogBackup(string backup)
        {
            if (backup != null)
                Debug.LogWarning($"[저장] 읽지 못한 저장 파일을 보관했다: {backup}");
        }
    }
}
