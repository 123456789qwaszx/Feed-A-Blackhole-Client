using System;
using System.Collections.Generic;
using System.IO;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 진행 저장의 창구. 앱 시작 때 콘텐츠를 불러온 뒤 한 번 불러 검사한다.
    // 불러오기: 본 파일 → 읽지 못하거나 깨졌으면 보관하고 직전 저장 → 그것도 그렇으면 보관하고 이어 할 저장이 없다.
    // 저장: 새 게임·계속을 고른 뒤, 진행 상태가 바뀔 때마다(노드 구매, 결산)와 앱이 내려갈 때.
    // 고르기 전에는 저장하지 않는다 — 타이틀에서 앱이 내려가도 이전 저장을 덮지 않는다.
    public sealed class ProgressStore
    {
        private readonly ProgressSaveFile _file;
        // 이어 한 저장. 다음 저장 때 게임에 쓰지 않은 원래 값을 되돌려 적는다. 새 게임이면 null.
        private SavedProgress _basis;
        // 이번 실행에서 새 게임·계속을 골랐는가.
        private bool _started;
        // 테스트 세션: 테스트 도구가 진행 상태를 시나리오로 바꿨다. 이번 실행은 저장하지 않는다(저장 파일을 지킨다).
        private bool _testSession;

        // 앱 시작 때 불러온 저장. 없으면 null.
        public SavedProgress Saved { get; private set; }

        // 계속할 진행이 있는가: 불러온 저장이 있거나, 이번 실행에서 이미 진행 중이다.
        public bool CanContinue => _started || Saved != null;

        public bool IsTestSession => _testSession;

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

        // 계속. 이번 실행에서 이미 진행 중이면 메모리의 진행 상태가 가장 새것이라 그대로 둔다.
        public void Continue(ProgressState progress)
        {
            if (_started)
                return;

            ProgressSave.Restore(progress, Saved);
            _basis = Saved;
            _started = true;
        }

        // 새 게임. 진행 상태를 처음으로 되돌리고 바로 저장한다. 이전 저장은 직전 저장(.prev)으로 한 번 남는다.
        public void StartNew(ProgressState progress)
        {
            ProgressSave.StartNew(progress);
            _basis = null;
            _started = true;
            Save(progress);
        }

        // 테스트 도구가 진행 상태를 바꾸기 직전에 부른다. 이후 이번 실행은 진행 중으로 보고(계속은 메모리 상태 그대로) 저장하지 않는다.
        // 되돌리는 길은 없다. 앱(장면)을 다시 시작하면 저장 파일에서 다시 불러온다.
        public void EnterTestSession()
        {
            _started = true;
            _testSession = true;
        }

        public void Save(ProgressState progress)
        {
            if (!_started || _testSession)
                return;

            try
            {
                _file.Write(ProgressSave.Capture(progress, _basis, DateTime.UtcNow));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                // 저장 공간 부족 등. 게임은 그대로 이어 가고, 다음 저장 때 다시 쓴다.
                Debug.LogError($"[저장] 저장하지 못했다: {error.Message}");
            }
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
