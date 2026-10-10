using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BlackHole.EditorTools
{
    // 기획 시트 연동(M5)의 에디터 쪽 일: 설정 파일, 끌어오기(차이·검사·덮어쓰기·기록), 시트에 반영(기록), 자동 끌어오기.
    // 창(SheetSyncWindow)과 자동 끌어오기가 같은 함수를 쓴다. 덮어쓴 CSV는 수치 감시(M2)가 잡아 창·플레이 중인 판에 반영한다.
    // - 자동 끌어오기: 설정에서 켜면 간격마다(Unity가 앞에 있을 때만) 시트를 읽는다. 바뀐 것이 있고, 덮으면 사라질
    //   레포의 변경(시트에 아직 안 옮긴 반영)이 없을 때만 덮는다. 있으면 보류하고 창에 이유를 보인다.
    [InitializeOnLoad]
    internal static class SheetSync
    {
        private const double ConfigCheckSeconds = 1;

        // 상태가 바뀌었다(창이 다시 그린다).
        public static event Action Changed;

        // 창이 요청을 보내는 중이면 자동 끌어오기를 쉰다.
        public static bool ManualBusy;

        public static string AutoStatus { get; private set; } = "자동 끌어오기 꺼짐";

        public static string ConfigPath => Path.Combine(PlaytestNotes.DataFolder, SheetSyncConfig.FileName);

        private static SheetSyncConfig _config;
        private static DateTime _configWrite;
        private static double _nextConfigCheck;
        private static double _nextAutoPull;
        private static Task<SheetReply> _autoTask;
        private static string _heldReason;

        static SheetSync()
        {
            EditorApplication.update += Tick;
        }

        // 끌어오기 준비 결과: 시트 글(레포 줄 끝에 맞춤), 탭별 차이, 검사 오류·경고, 시트에 아직 안 옮긴 변경과 그 칸의 시트 상태.
        internal sealed class PullPlan
        {
            public readonly Dictionary<string, string> Remote = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> Paths = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly List<SheetTabDiff> Diffs = new List<SheetTabDiff>();
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public readonly List<ChangeRecord> Pending = new List<ChangeRecord>();
            public readonly List<(SheetUpdate Update, SheetCellState State, double SheetValue)> PendingStates =
                new List<(SheetUpdate Update, SheetCellState State, double SheetValue)>();

            public bool HasChanges => Diffs.Exists(diff => diff.HasChanges);
            public bool NeedsWrite => Diffs.Exists(diff => diff.HasChanges || diff.TextOnly);

            // 덮으면 사라지는 레포의 변경이 있다(시트가 그 칸을 아직 이전 값으로 갖고 있거나 따로 바뀌었다).
            public bool LosesLocal => PendingStates.Exists(item => item.State != SheetCellState.Present);
        }

        #region 설정

        public static SheetSyncConfig LoadConfig(out string error)
        {
            error = null;

            try
            {
                return File.Exists(ConfigPath) ? SheetSyncConfig.Parse(File.ReadAllText(ConfigPath), out error) : new SheetSyncConfig();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = exception.Message;
                return new SheetSyncConfig();
            }
        }

        public static bool SaveConfig(SheetSyncConfig config, out string error)
        {
            try
            {
                Directory.CreateDirectory(PlaytestNotes.DataFolder);
                File.WriteAllText(ConfigPath, config.ToJsonText());
                _configWrite = default;
                _nextAutoPull = 0;
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = exception.Message;
                return false;
            }
        }

        #endregion

        #region 끌어오기

        // 시트에서 읽은 탭으로 끌어오기를 준비한다(아직 쓰지 않는다).
        public static PullPlan Prepare(SheetReply reply)
        {
            var plan = new PullPlan();
            GameContentSetup setup = FindAsset<GameContentSetup>();

            if (!TryLocal(setup, plan.Paths, out Dictionary<string, string> local, out string error))
            {
                plan.Errors.Add(error);
                return plan;
            }

            foreach (string tab in SheetTabs.Names)
            {
                string remote = SheetDiff.MatchLineEndings(reply.Tabs[tab], local[tab]);
                plan.Remote[tab] = remote;
                SheetTabDiff diff = SheetDiff.Compare(tab, local[tab], remote);
                plan.Diffs.Add(diff);

                if (diff.Error != null)
                    plan.Errors.Add($"{tab}: {diff.Error}");
            }

            if (plan.Errors.Count > 0)
                return plan;

            // 게임과 같은 길로 검사한다. 원본 콘텐츠를 못 불러오면 노드 × 콘텐츠 검사만 건너뛴다.
            LoadedContent loaded = GameContentLoader.Load(setup);
            plan.Errors.AddRange(NodeSheetCheck.Check(plan.Remote, setup.Nodes.ToData(), loaded?.Content, plan.Warnings));

            List<ChangeRecord> records = PlaytestChanges.ReadAll();
            plan.Pending.AddRange(SheetSyncPlan.Pending(records));

            foreach (SheetUpdate update in SheetSyncPlan.NetUpdates(plan.Pending))
            {
                SheetCellState state = SheetSyncPlan.StateOf(update, plan.Remote[update.Tab], out double value);
                plan.PendingStates.Add((update, state, value));
            }

            return plan;
        }

        // 준비한 끌어오기를 쓴다: 바뀐 탭의 CSV를 시트 글로 덮고 기록을 남긴다. 덮은 뒤에는 시트에 안 옮긴 변경이 없다(레포 = 시트).
        public static bool ApplyPull(PullPlan plan, string appliedBy, out ChangeRecord record, out string error)
        {
            record = null;

            if (plan.Errors.Count > 0)
            {
                error = "검사 오류가 있어 덮어쓰지 않는다.";
                return false;
            }

            GameContentSetup setup = FindAsset<GameContentSetup>();
            string before = ProfilePromoter.Fingerprint(setup);
            var written = new List<string>();

            try
            {
                foreach (SheetTabDiff diff in plan.Diffs)
                {
                    if (!diff.HasChanges && !diff.TextOnly)
                        continue;

                    string path = plan.Paths[diff.Tab];
                    File.WriteAllText(path, plan.Remote[diff.Tab], new UTF8Encoding(ProfilePromoter.HasBom(path)));
                    written.Add(path);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = $"CSV를 쓰지 못했다({string.Join(", ", written)}까지 씀): {exception.Message}";
                return false;
            }

            foreach (string path in written)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            record = new ChangeRecord
            {
                Id = ChangeRecord.NewId(DateTime.UtcNow, new System.Random()),
                AtUtc = DateTime.UtcNow,
                Kind = ChangeRecord.Pull,
                Author = "sheet",
                AppliedBy = appliedBy,
                FingerprintBefore = before,
                FingerprintAfter = ProfilePromoter.Fingerprint(setup),
                SheetSynced = true,
            };

            var summary = new List<string>();
            foreach (SheetTabDiff diff in plan.Diffs)
            {
                if (diff.HasChanges || diff.TextOnly)
                    summary.Add(diff.Summary());

                record.Patches.AddRange(SheetDiff.NodeValuePatches(diff));
            }

            foreach (ChangeRecord pending in plan.Pending)
                record.SyncOf.Add(pending.Id);

            if (plan.LosesLocal)
                summary.Add($"시트에 없던 레포 변경 {plan.PendingStates.FindAll(item => item.State != SheetCellState.Present).Count}칸을 시트 값으로 덮음");

            record.Summary = string.Join("; ", summary);

            if (!PlaytestChanges.TryAppend(record, out error))
            {
                Debug.LogError($"[시트] 변경 기록을 쓰지 못했다({error}). 기록:\n{PlaytestJson.Write(record.ToJson())}");
                error = $"CSV는 덮었지만 변경 기록을 쓰지 못했다: {error}";
                return false;
            }

            Debug.Log($"[시트] 끌어옴({appliedBy}) {record.Id}: {record.Summary} · 지문 {record.FingerprintBefore} → {record.FingerprintAfter}");
            error = null;
            return true;
        }

        // 레포 노드 CSV 4개의 경로와 글(노드 콘텐츠가 가리키는 TextAsset).
        private static bool TryLocal(GameContentSetup setup, Dictionary<string, string> paths, out Dictionary<string, string> texts, out string error)
        {
            texts = new Dictionary<string, string>(StringComparer.Ordinal);
            NodeContentSource source = setup != null && setup.Nodes != null ? setup.Nodes.Content : null;

            if (source == null)
            {
                error = "게임 콘텐츠 세트의 노드 콘텐츠(NodeContentSource)를 찾지 못했다.";
                return false;
            }

            var assets = new Dictionary<string, TextAsset>(StringComparer.Ordinal)
            {
                { SheetTabs.UpgradeStats, source.UpgradeStatsCsv },
                { SheetTabs.Nodes, source.NodesCsv },
                { SheetTabs.NodeCost, source.NodeCostCsv },
                { SheetTabs.NodeEffects, source.NodeEffectsCsv },
            };

            foreach (KeyValuePair<string, TextAsset> pair in assets)
            {
                if (pair.Value == null)
                {
                    error = $"노드 콘텐츠에 {pair.Key} CSV가 연결되어 있지 않다.";
                    return false;
                }

                string path = AssetDatabase.GetAssetPath(pair.Value);
                paths[pair.Key] = path;
                texts[pair.Key] = File.ReadAllText(path);
            }

            error = null;
            return true;
        }

        #endregion

        #region 시트에 반영

        // 시트에 쓴 결과를 기록한다. 쓸 칸이 없어도(바꿨다 되돌렸으면) 기록해 미반영에서 뺀다.
        public static ChangeRecord RecordPush(List<ChangeRecord> pending, List<SheetUpdate> updates, SheetReply reply, out string error)
        {
            var record = new ChangeRecord
            {
                Id = ChangeRecord.NewId(DateTime.UtcNow, new System.Random()),
                AtUtc = DateTime.UtcNow,
                Kind = ChangeRecord.Sheet,
                Author = "human",
                SheetSynced = true,
            };

            int already = 0;
            var results = reply?.Body?.Array("results") ?? new List<object>();

            for (int i = 0; i < updates.Count; i++)
            {
                SheetUpdate update = updates[i];
                JsonObject result = i < results.Count ? results[i] as JsonObject : null;

                if (result?["already"] is bool done && done)
                    already++;

                record.Patches.Add(new ChangePatch
                {
                    Path = update.Path,
                    Target = "시트 " + update.Describe(),
                    Before = result?.Number("before") ?? update.Before,
                    After = update.After,
                    Reason = "승격·되돌리기를 시트에 옮김",
                });
            }

            foreach (ChangeRecord item in pending)
                record.SyncOf.Add(item.Id);

            record.Summary = updates.Count == 0
                ? $"쓸 칸 없음(바꿨다 되돌림) · 기록 {pending.Count}개 정리"
                : $"시트에 {updates.Count - already}칸 씀" + (already > 0 ? $"(이미 같은 값 {already}칸)" : string.Empty) + $" · 기록 {pending.Count}개";

            if (!PlaytestChanges.TryAppend(record, out error))
                Debug.LogError($"[시트] 변경 기록을 쓰지 못했다({error}). 기록:\n{PlaytestJson.Write(record.ToJson())}");
            else
                Debug.Log($"[시트] 반영 {record.Id}: {record.Summary}");

            return record;
        }

        #endregion

        #region 자동 끌어오기

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;

            if (now >= _nextConfigCheck)
            {
                _nextConfigCheck = now + ConfigCheckSeconds;
                RefreshConfig();
            }

            if (_autoTask != null)
            {
                if (_autoTask.IsCompleted)
                    FinishAutoPull();

                return;
            }

            if (_config == null || !_config.AutoPull || !_config.HasEndpoint)
                return;

            if (now < _nextAutoPull || ManualBusy || EditorApplication.isCompiling || EditorApplication.isUpdating
                || !InternalEditorUtility.isApplicationActive)
                return;

            _nextAutoPull = now + _config.AutoPullSeconds;
            _autoTask = new SheetClient(_config).Read();
        }

        private static void RefreshConfig()
        {
            DateTime write = File.Exists(ConfigPath) ? File.GetLastWriteTimeUtc(ConfigPath) : default;

            if (_config != null && write == _configWrite)
                return;

            _configWrite = write;
            _config = LoadConfig(out _);
            SetStatus(!_config.AutoPull ? "자동 끌어오기 꺼짐"
                : !_config.HasEndpoint ? "자동 끌어오기: 웹 앱 주소·토큰이 없어 쉰다"
                : $"자동 끌어오기 켜짐({_config.AutoPullSeconds}초마다, Unity가 앞에 있을 때)");
        }

        private static void FinishAutoPull()
        {
            Task<SheetReply> task = _autoTask;
            _autoTask = null;
            string time = DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

            if (task.IsFaulted || task.Result == null || !task.Result.Ok)
            {
                SetStatus($"{time} 자동 끌어오기 실패: {(task.IsFaulted ? task.Exception?.GetBaseException().Message : task.Result?.Describe())}");
                return;
            }

            PullPlan plan = Prepare(task.Result);

            if (plan.Errors.Count > 0)
            {
                Hold($"{time} 보류: 시트 값이 검사를 통과하지 못했다 — {plan.Errors[0]}");
                return;
            }

            if (!plan.NeedsWrite)
            {
                _heldReason = null;
                SetStatus($"{time} 시트와 같다");
                return;
            }

            if (plan.LosesLocal)
            {
                Hold($"{time} 보류: 시트에 아직 안 옮긴 레포 변경이 있다. Sheet Sync 창에서 \"시트에 반영\"하거나 직접 끌어온다.");
                return;
            }

            _heldReason = null;
            SetStatus(ApplyPull(plan, "auto", out ChangeRecord record, out string error)
                ? $"{time} 끌어옴: {record.Summary}"
                : $"{time} 끌어오기 실패: {error}");
        }

        // 같은 이유로는 콘솔에 한 번만 알린다.
        private static void Hold(string status)
        {
            string reason = status.Substring(status.IndexOf(' ') + 1);

            if (reason != _heldReason)
                Debug.LogWarning("[시트] " + reason);

            _heldReason = reason;
            SetStatus(status);
        }

        private static void SetStatus(string status)
        {
            AutoStatus = status;
            Changed?.Invoke();
        }

        #endregion

        internal static T FindAsset<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
