using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using BlackHole.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlackHole.EditorTools
{
    // 기획 시트 연동(메뉴 BlackHole > Sheet Sync, M5). 노드 시트(구글 시트 v3)와 레포 노드 CSV를 맞춘다.
    // - 설정: 시트의 Apps Script 웹 앱 주소와 토큰(PlaytestData/sheet.json, git 무시). 설치는 Docs/BalanceLoop/M5-sheet-sync.md.
    // - 끌어오기: 시트를 읽어 행 단위 차이와 검사 결과를 보이고, 확인하면 CSV를 덮는다(수치 감시 M2가 바로 반영).
    //   시트에 아직 안 옮긴 레포 변경(승격·되돌리기)이 있으면 덮기 전에 알린다.
    // - 시트에 반영: 승격·되돌리기한 노드 값을 시트의 그 칸에 쓴다. 시트 값이 기록의 이전 값과 다르면 아무것도 쓰지 않는다.
    // - 자동 끌어오기: 켜면 간격마다 시트를 보고, 덮어도 잃을 것이 없을 때만 덮는다(SheetSync).
    internal sealed class SheetSyncWindow : EditorWindow
    {
        private const int MaxRowsShown = 60;

        private SheetSyncConfig _config;
        private string _message;
        private bool _error;
        private string _ping;

        private Task<SheetReply> _task;
        private Action<SheetReply> _onDone;
        private string _busy;

        private SheetSync.PullPlan _plan;
        private List<ChangeRecord> _pushPending;
        private List<SheetUpdate> _pushUpdates;

        private VisualElement _root;

        [MenuItem("BlackHole/Sheet Sync")]
        internal static void Open() => GetWindow<SheetSyncWindow>("Sheet Sync");

        private void OnEnable()
        {
            EditorApplication.update += Poll;
            SheetSync.Changed += Rebuild;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Poll;
            SheetSync.Changed -= Rebuild;
            SheetSync.ManualBusy = false;
        }

        private void CreateGUI()
        {
            var scroll = new ScrollView();
            scroll.style.paddingLeft = 8;
            scroll.style.paddingRight = 8;
            rootVisualElement.Add(scroll);
            _root = scroll;
            _config = SheetSync.LoadConfig(out string error);

            if (error != null)
                Show(error, true);

            Rebuild();
        }

        private void OnFocus()
        {
            if (_root != null && _task == null)
                Rebuild();
        }

        // 보낸 요청이 끝났나 매 프레임 본다(결과는 메인 스레드에서 쓴다).
        private void Poll()
        {
            if (_task == null || !_task.IsCompleted)
                return;

            Task<SheetReply> task = _task;
            Action<SheetReply> done = _onDone;
            _task = null;
            _onDone = null;
            _busy = null;
            SheetSync.ManualBusy = false;
            SheetReply reply = task.IsFaulted ? SheetReply.Fail(task.Exception?.GetBaseException().Message) : task.Result;
            done(reply);
            Rebuild();
        }

        private void Send(string what, Task<SheetReply> task, Action<SheetReply> done)
        {
            _busy = what;
            _task = task;
            _onDone = done;
            SheetSync.ManualBusy = true;
            Rebuild();
        }

        private void Rebuild()
        {
            if (_root == null)
                return;

            _root.Clear();
            BuildConfig();

            if (!string.IsNullOrEmpty(_message))
                _root.Add(new HelpBox(_message, _error ? HelpBoxMessageType.Error : HelpBoxMessageType.Info));

            if (_busy != null)
                _root.Add(new HelpBox(_busy + " 중…", HelpBoxMessageType.None));

            BuildPull();
            BuildPush();
        }

        #region 설정

        private void BuildConfig()
        {
            _root.Add(Header("시트 웹 앱"));
            _root.Add(Note("시트의 Apps Script 웹 앱 주소(…/exec)와 토큰. PlaytestData/sheet.json에 저장한다(git 무시). 설치: Docs/BalanceLoop/M5-sheet-sync.md"));

            var endpoint = new TextField("웹 앱 주소") { value = _config.Endpoint };
            endpoint.RegisterValueChangedCallback(evt => _config.Endpoint = evt.newValue.Trim());
            _root.Add(endpoint);

            var token = new TextField("토큰") { value = _config.Token, isPasswordField = true, maskChar = '•' };
            token.RegisterValueChangedCallback(evt => _config.Token = evt.newValue.Trim());
            _root.Add(token);

            var row = Row();
            row.Add(Grow(new Button(SaveConfig) { text = "저장" }));
            var ping = new Button(Ping) { text = "연결 확인" };
            ping.SetEnabled(_task == null);
            row.Add(Grow(ping));
            _root.Add(row);

            if (!_config.HasEndpoint && _config.CanRead)
                _root.Add(Note("웹 앱이 없어 탭 CSV 주소로 읽기만 한다(sheet.json의 csv). 시트에 반영은 할 수 없다."));

            if (_ping != null)
                _root.Add(Note(_ping));
        }

        private void SaveConfig()
        {
            Show(SheetSync.SaveConfig(_config, out string error) ? $"저장했다: {SheetSync.ConfigPath}" : $"저장하지 못했다: {error}", error != null);
            Rebuild();
        }

        private void Ping()
        {
            Send("연결 확인", new SheetClient(_config).Ping(), reply =>
            {
                if (!reply.Ok)
                {
                    _ping = null;
                    Show("연결 확인 실패: " + reply.Describe(), true);
                    return;
                }

                List<object> missing = reply.Body.Array("missing") ?? new List<object>();
                _ping = $"연결됨: 시트 '{reply.Body.Text("sheet")}' · 탭 {(reply.Body.Array("tabs") ?? new List<object>()).Count}/{SheetTabs.Names.Length}" +
                        (missing.Count > 0 ? $" · 없는 탭: {string.Join(", ", missing)}" : string.Empty);
                Show(missing.Count > 0 ? "시트에 없는 탭이 있다. 탭 이름을 맞추거나 sheet.json의 tabs로 이름을 알려 준다." : "연결됐다.", missing.Count > 0);
            });
        }

        #endregion

        #region 끌어오기

        private void BuildPull()
        {
            _root.Add(Header("시트 → 레포 (끌어오기)"));

            var auto = Row();
            var toggle = new Toggle("자동 끌어오기") { value = _config.AutoPull };
            toggle.RegisterValueChangedCallback(evt =>
            {
                _config.AutoPull = evt.newValue;
                SaveConfig();
            });
            auto.Add(toggle);
            var seconds = new IntegerField("간격(초)") { value = _config.AutoPullSeconds };
            seconds.RegisterValueChangedCallback(evt => _config.AutoPullSeconds = Math.Max(SheetSyncConfig.MinAutoPullSeconds, evt.newValue));
            seconds.style.width = 160;
            auto.Add(seconds);
            _root.Add(auto);
            _root.Add(Note(SheetSync.AutoStatus + " · 간격을 바꾸면 \"저장\"을 누른다. 덮어도 잃을 것이 없을 때만 저절로 덮는다."));

            var pull = new Button(StartPull) { text = "시트에서 끌어오기" };
            pull.style.height = 24;
            pull.SetEnabled(_task == null && _config.CanRead);
            _root.Add(pull);

            if (_plan == null)
                return;

            foreach (SheetTabDiff diff in _plan.Diffs)
                _root.Add(Note(diff.Summary()));

            foreach (string error in _plan.Errors)
                _root.Add(new HelpBox(error, HelpBoxMessageType.Error));

            foreach (string warning in _plan.Warnings)
                _root.Add(new HelpBox(warning, HelpBoxMessageType.Warning));

            var losing = _plan.PendingStates.FindAll(item => item.State != SheetCellState.Present);
            if (losing.Count > 0)
            {
                var lines = new List<string> { $"시트에 아직 안 옮긴 레포 변경 {losing.Count}칸이 덮으면 사라진다(먼저 \"시트에 반영\"을 권한다):" };
                foreach ((SheetUpdate update, SheetCellState state, double value) in losing)
                    lines.Add($"· {update.Describe()}: 레포 {Number(update.After)}, 시트 {(state == SheetCellState.NotFound ? "행 없음" : Number(value))}" +
                              (state == SheetCellState.Conflict ? $" (기록의 이전 값 {Number(update.Before)}과도 다르다)" : string.Empty));

                _root.Add(new HelpBox(string.Join("\n", lines), HelpBoxMessageType.Warning));
            }

            int shown = 0;
            foreach (SheetTabDiff diff in _plan.Diffs)
            {
                foreach (SheetRowChange change in diff.Changed)
                {
                    if (shown++ >= MaxRowsShown)
                        break;

                    var parts = new List<string>();
                    foreach ((string column, string before, string after) in change.Cells)
                        parts.Add($"{column} {before} → {after}");

                    _root.Add(Wrapped($"{diff.Tab} {change.Key.Replace('\n', ' ')}: {string.Join(", ", parts)}"));
                }

                foreach (SheetRowChange added in diff.Added)
                {
                    if (shown++ < MaxRowsShown)
                        _root.Add(Wrapped($"{diff.Tab} + {added.Key.Replace('\n', ' ')}"));
                }

                foreach (SheetRowChange removed in diff.Removed)
                {
                    if (shown++ < MaxRowsShown)
                        _root.Add(Wrapped($"{diff.Tab} − {removed.Key.Replace('\n', ' ')}"));
                }
            }

            if (shown > MaxRowsShown)
                _root.Add(Note($"… 그 밖에 {shown - MaxRowsShown}행"));

            var row = Row();
            var apply = new Button(ApplyPull) { text = _plan.LosesLocal ? "그래도 덮어쓰기" : "덮어쓰기" };
            apply.SetEnabled(_task == null && _plan.Errors.Count == 0 && _plan.NeedsWrite);
            row.Add(Grow(apply));
            row.Add(Grow(new Button(() => { _plan = null; Rebuild(); }) { text = "닫기" }));
            _root.Add(row);
        }

        private void StartPull()
        {
            _plan = null;
            Send("시트 읽기", new SheetClient(_config).Read(), reply =>
            {
                if (!reply.Ok)
                {
                    Show("시트를 읽지 못했다: " + reply.Describe(), true);
                    return;
                }

                _plan = SheetSync.Prepare(reply);
                Show(_plan.Errors.Count > 0 ? "시트 값이 검사를 통과하지 못해 덮어쓸 수 없다."
                    : !_plan.NeedsWrite ? "레포 CSV가 시트와 같다."
                    : _plan.HasChanges ? "아래 차이를 보고 덮어쓴다."
                    : "값은 같고 글자 모양만 다르다. 덮어쓰면 시트 모양으로 맞춘다.", _plan.Errors.Count > 0);
            });
        }

        private void ApplyPull()
        {
            if (_plan.LosesLocal && !EditorUtility.DisplayDialog(
                    "시트 값으로 덮어쓰기",
                    "시트에 아직 옮기지 않은 레포 변경이 시트 값으로 바뀝니다. 그 변경은 변경 기록에만 남습니다.",
                    "덮어쓰기",
                    "취소"))
                return;

            Show(SheetSync.ApplyPull(_plan, "human", out ChangeRecord record, out string error)
                ? $"끌어왔다({record.Id}): {record.Summary} · 지문 {record.FingerprintBefore} → {record.FingerprintAfter}"
                : $"끌어오지 못했다: {error}", error != null);
            _plan = null;
            Rebuild();
        }

        #endregion

        #region 시트에 반영

        private void BuildPush()
        {
            _root.Add(Header("레포 → 시트 (시트에 반영)"));
            List<ChangeRecord> pending = SheetSyncPlan.Pending(PlaytestChanges.ReadAll());
            List<SheetUpdate> updates = SheetSyncPlan.NetUpdates(pending);

            if (pending.Count == 0)
            {
                _root.Add(Note("시트에 옮길 변경이 없다(승격·되돌리기한 노드 값이 모두 시트와 맞춰졌다)."));
                return;
            }

            _root.Add(Note($"시트에 아직 안 옮긴 기록 {pending.Count}개 → 칸 {updates.Count}개(같은 칸은 처음 이전 값 → 마지막 이후 값으로 합친다)"));

            foreach (SheetUpdate update in updates)
                _root.Add(Wrapped($"{update.Describe()}: {Number(update.Before)} → {Number(update.After)}"));

            var push = new Button(StartPush) { text = updates.Count > 0 ? "시트에 반영" : "기록 정리(쓸 칸 없음)" };
            push.style.height = 24;
            push.SetEnabled(_task == null && (updates.Count == 0 || _config.HasEndpoint));
            _root.Add(push);

            if (!_config.HasEndpoint)
                _root.Add(Note("시트에 쓰려면 웹 앱 주소와 토큰이 필요하다."));
        }

        private void StartPush()
        {
            _pushPending = SheetSyncPlan.Pending(PlaytestChanges.ReadAll());
            _pushUpdates = SheetSyncPlan.NetUpdates(_pushPending);

            if (_pushUpdates.Count == 0)
            {
                ChangeRecord record = SheetSync.RecordPush(_pushPending, _pushUpdates, null, out string error);
                Show(error == null ? $"정리했다({record.Id}): {record.Summary}" : $"기록을 쓰지 못했다: {error}", error != null);
                Rebuild();
                return;
            }

            if (!EditorUtility.DisplayDialog("시트에 반영", $"기획 시트의 칸 {_pushUpdates.Count}개를 고칩니다. 시트 값이 기록의 이전 값과 다르면 아무것도 쓰지 않습니다.", "반영", "취소"))
                return;

            Send("시트에 쓰기", new SheetClient(_config).Write(_pushUpdates, false), reply =>
            {
                if (!reply.Ok)
                {
                    Show("시트에 쓰지 못했다(아무것도 바꾸지 않았다): " + reply.Describe(), true);
                    return;
                }

                ChangeRecord record = SheetSync.RecordPush(_pushPending, _pushUpdates, reply, out string error);
                Show(error == null ? $"시트에 반영했다({record.Id}): {record.Summary}" : $"시트에는 썼지만 기록을 쓰지 못했다: {error}", error != null);
            });
        }

        #endregion

        #region 모양

        private void Show(string message, bool error)
        {
            _message = message;
            _error = error;
        }

        private static string Number(double value) => value.ToString("#,0.####", CultureInfo.InvariantCulture);

        private static Label Header(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 10;
            label.style.marginBottom = 4;
            return label;
        }

        private static Label Note(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.opacity = 0.7f;
            label.style.marginBottom = 4;
            return label;
        }

        private static Label Wrapped(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.selection.isSelectable = true;
            return label;
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private static T Grow<T>(T element) where T : VisualElement
        {
            element.style.flexGrow = 1;
            return element;
        }

        #endregion
    }
}
