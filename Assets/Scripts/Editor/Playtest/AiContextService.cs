using System;
using System.Collections.Generic;
using System.IO;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;
using UnityEngine;

namespace BlackHole.EditorTools
{
    // AI 묶음 쓰기(M4). 에디터를 켜면 늘 돈다.
    // - 메모가 새로 쌓이면(테스트 세팅 창·개발 패널 어디서든) 그 세팅 키의 묶음을 다시 쓴다(1초마다 메모 파일을 본다).
    // - 수치·프로필(AI 초안 포함)이 바뀌면(LiveDataSignal) 있는 묶음을 최근에 쓴 것부터 MaxRefresh개 다시 쓴다
    //   (묶음 하나에 흐름 계산이 원본·초안 두 번이라 다 쓰면 에디터가 멈칫한다).
    //   AI가 초안을 쓰면 몇 초 안에 묶음의 draft 칸(검사 결과·원본과 다른 확정 정보·흐름)이 채워진다.
    // - 창의 "AI 묶음 만들기"는 메모 없이도 지금 세팅의 묶음을 쓴다.
    // 묶음: PlaytestData/context/<setupKey>.json, 목록: PlaytestData/context/index.json
    [InitializeOnLoad]
    internal static class AiContextService
    {
        private const double PollSeconds = 1;
        private const int MaxRefresh = 3;
        private const int FirstScan = 5;
        private const string IndexName = "index.json";

        // 묶음을 다시 썼다(창이 표시를 고친다).
        public static event Action Written;

        public static string Folder => Path.Combine(PlaytestNotes.DataFolder, "context");
        public static string DraftPath => PlaytestLibrary.ProfilesFolder + "/" + BalanceProfile.DraftName + ".json";

        private static (DateTime Write, long Length) _notesStamp;
        private static int _notesSeen = -1;
        private static double _nextPoll;

        static AiContextService()
        {
            EditorApplication.update += Poll;
            LiveDataSignal.Changed += OnLiveData;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        public static string PathOf(string setupKey) => Path.Combine(Folder, setupKey + ".json");

        private static void Stop()
        {
            EditorApplication.update -= Poll;
            LiveDataSignal.Changed -= OnLiveData;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll)
                return;

            _nextPoll = EditorApplication.timeSinceStartup + PollSeconds;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            (DateTime Write, long Length) stamp = PlaytestNotes.Stamp();

            if (stamp == _notesStamp)
                return;

            _notesStamp = stamp;
            List<JsonObject> notes = PlaytestNotes.ReadRaw();
            var keys = new List<string>();

            if (_notesSeen < 0)
            {
                // 에디터를 켠 뒤 처음: 최근 메모 가운데 묶음이 없는 세팅만 쓴다(켤 때마다 모두 다시 쓰지 않는다).
                for (int i = notes.Count - 1; i >= 0 && keys.Count < FirstScan; i--)
                {
                    string key = notes[i].Text("setupKey");

                    if (key != null && !keys.Contains(key) && !File.Exists(PathOf(key)))
                        keys.Add(key);
                }
            }
            else
            {
                for (int i = Math.Min(_notesSeen, notes.Count); i < notes.Count; i++)
                {
                    string key = notes[i].Text("setupKey");

                    if (key != null && !keys.Contains(key))
                        keys.Add(key);
                }
            }

            _notesSeen = notes.Count;

            if (keys.Count > 0)
                WriteKeys(keys, notes);
        }

        private static void OnLiveData(LiveDataChange change)
        {
            if (!change.ContentChanged)
                return;

            List<string> keys = ExistingKeys(MaxRefresh);

            if (keys.Count > 0)
                WriteKeys(keys, PlaytestNotes.ReadRaw());
        }

        // 있는 묶음의 세팅 키(최근에 쓴 것부터 max개).
        private static List<string> ExistingKeys(int max)
        {
            var keys = new List<string>();

            if (!Directory.Exists(Folder))
                return keys;

            var files = new List<FileInfo>(new DirectoryInfo(Folder).GetFiles("*.json"));
            files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));

            foreach (FileInfo file in files)
            {
                if (file.Name != IndexName && keys.Count < max)
                    keys.Add(Path.GetFileNameWithoutExtension(file.Name));
            }

            return keys;
        }

        // 지금 창의 세팅으로 쓴다(메모가 없어도). 성공하면 파일 경로.
        public static bool WriteFor(PlaytestScenario setup, string setupKey, out string path, out string error)
        {
            path = PathOf(setupKey);
            Loaded loaded = Load(out error);

            if (loaded == null)
                return false;

            List<JsonObject> notes = PlaytestNotes.ReadRaw();

            if (!Write(loaded, setup, setupKey, NotesOf(notes, setupKey), out error))
                return false;

            WriteIndex(notes);
            Written?.Invoke();
            return true;
        }

        private static void WriteKeys(List<string> keys, List<JsonObject> notes)
        {
            Loaded loaded = Load(out string error);

            if (loaded == null)
            {
                Debug.LogWarning($"[AI 묶음] 쓰지 못했다: {error}");
                return;
            }

            int written = 0;

            foreach (string key in keys)
            {
                List<JsonObject> mine = NotesOf(notes, key);
                PlaytestScenario setup = mine.Count > 0 ? AiContext.SetupFromNote(mine[0]) : ReadSetup(key);

                if (setup == null)
                    continue;

                if (Write(loaded, setup, key, mine, out error))
                    written++;
                else
                    Debug.LogWarning($"[AI 묶음] {key}: {error}");
            }

            WriteIndex(notes);

            if (written > 0)
                Written?.Invoke();
        }

        // 이 세팅 키의 메모(최근 것이 앞).
        private static List<JsonObject> NotesOf(List<JsonObject> notes, string key)
        {
            var mine = new List<JsonObject>();

            for (int i = notes.Count - 1; i >= 0; i--)
            {
                if (notes[i].Text("setupKey") == key)
                    mine.Add(notes[i]);
            }

            return mine;
        }

        // 메모 없이 만든 묶음은 묶음 안의 세팅으로 다시 쓴다.
        private static PlaytestScenario ReadSetup(string key)
        {
            try
            {
                return PlaytestJson.Parse(File.ReadAllText(PathOf(key))) is JsonObject context
                    ? AiContext.SetupFromNote(context)
                    : null;
            }
            catch (Exception exception) when (exception is IOException || exception is FormatException || exception is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static bool Write(Loaded loaded, PlaytestScenario setup, string key, List<JsonObject> notes, out string error)
        {
            var changes = PlaytestChanges.ReadRaw();
            changes.Reverse();

            var input = new AiContextInput
            {
                Setup = setup,
                SetupKey = key,
                Notes = notes,
                Changes = changes,
                BaseData = loaded.BaseData,
                BaseNodes = loaded.BaseNodes,
                BaseContent = loaded.Base.Content,
                BaseTree = loaded.Base.NodeTree,
                BaseFingerprint = loaded.BaseFingerprint,
                SelectedProfile = PlayerPrefs.GetString(PlaytestSession.ProfileKey, ""),
                Draft = loaded.Draft,
                DraftErrors = loaded.DraftErrors,
                DraftContent = loaded.DraftLoaded?.Content,
                DraftTree = loaded.DraftLoaded?.NodeTree,
                DraftFingerprint = loaded.DraftFingerprint,
            };

            try
            {
                string json = PlaytestJson.Write(AiContext.Build(input), true);
                Directory.CreateDirectory(Folder);
                File.WriteAllText(PathOf(key), json + "\n");
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        // 묶음 목록: 세팅 키마다 이름·마지막 메모·초안 여부. AI는 여기서 읽을 묶음을 고른다.
        private static void WriteIndex(List<JsonObject> notes)
        {
            var items = new List<object>();

            foreach (string key in ExistingKeys(int.MaxValue))
            {
                List<JsonObject> mine = NotesOf(notes, key);
                JsonObject last = mine.Count > 0 ? mine[0] : null;
                items.Add(new JsonObject
                {
                    { "setupKey", key },
                    { "file", $"PlaytestData/context/{key}.json" },
                    { "setupName", last?.Object("setup")?.Text("name") },
                    { "notes", mine.Count },
                    { "lastNoteId", last?.Text("id") },
                    { "lastNoteAtUtc", last?.Text("atUtc") },
                    { "updatedAtUtc", File.GetLastWriteTimeUtc(PathOf(key)).ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture) },
                });
            }

            var index = new JsonObject
            {
                { "schema", AiContext.Schema },
                { "guide", "Docs/BalanceLoop/AI-GUIDE.md" },
                { "draft", File.Exists(DraftPath) ? DraftPath : null },
                { "contexts", items },
            };

            try
            {
                File.WriteAllText(Path.Combine(Folder, IndexName), PlaytestJson.Write(index, true) + "\n");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[AI 묶음] 목록을 쓰지 못했다: {exception.Message}");
            }
        }

        // 원본과 AI 초안을 게임과 같은 로더로 한 번 불러 둔다(여러 묶음이 같이 쓴다).
        private sealed class Loaded
        {
            public LoadedContent Base;
            public ContentData BaseData;
            public NodeContentData BaseNodes;
            public string BaseFingerprint;
            public BalanceProfile Draft;
            public List<string> DraftErrors = new List<string>();
            public LoadedContent DraftLoaded;
            public string DraftFingerprint;
        }

        private static Loaded Load(out string error)
        {
            GameContentSetup setup = FindAsset<GameContentSetup>();

            if (setup == null)
            {
                error = "게임 콘텐츠 세트(GameContentSetup) 에셋을 찾지 못했다.";
                return null;
            }

            var loaded = new Loaded { Base = GameContentLoader.Load(setup) };

            if (loaded.Base == null)
            {
                error = "원본 콘텐츠를 불러오지 못했다. 콘솔의 [콘텐츠] 오류를 본다.";
                return null;
            }

            var readErrors = new List<ContentDiagnostic>();
            loaded.BaseData = setup.ToData();
            loaded.BaseNodes = setup.Nodes.Content.Read(readErrors);
            loaded.BaseFingerprint = ContentFingerprint.Of(loaded.BaseData, loaded.BaseNodes);
            LoadDraft(setup, loaded);
            error = null;
            return loaded;
        }

        private static void LoadDraft(GameContentSetup setup, Loaded loaded)
        {
            if (!File.Exists(DraftPath))
                return;

            BalanceProfile draft;

            try
            {
                draft = BalanceProfile.Parse(File.ReadAllText(DraftPath), out string parseError);

                if (draft == null)
                {
                    loaded.Draft = new BalanceProfile { name = BalanceProfile.DraftName, note = "(읽지 못함)" };
                    loaded.DraftErrors.Add(parseError);
                    return;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                loaded.Draft = new BalanceProfile { name = BalanceProfile.DraftName, note = "(읽지 못함)" };
                loaded.DraftErrors.Add(exception.Message);
                return;
            }

            loaded.Draft = draft;
            IReadOnlyList<string> patchErrors = Array.Empty<string>();
            var logErrors = new List<string>();

            void Collect(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception)
                    logErrors.Add(message);
            }

            LoadedContent result;
            Application.logMessageReceived += Collect;

            try
            {
                result = GameContentLoader.Load(setup, (content, nodes) =>
                {
                    patchErrors = BalanceProfilePatcher.Apply(draft, content, nodes);

                    if (patchErrors.Count == 0)
                        loaded.DraftFingerprint = ContentFingerprint.Of(content, nodes);

                    return patchErrors;
                });
            }
            finally
            {
                Application.logMessageReceived -= Collect;
            }

            if (patchErrors.Count > 0)
                loaded.DraftErrors.AddRange(patchErrors);
            else if (result == null)
                loaded.DraftErrors.AddRange(logErrors.Count > 0 ? logErrors : new List<string> { "초안 값이 콘텐츠 검사를 통과하지 못했다." });
            else
                loaded.DraftLoaded = result;
        }

        private static T FindAsset<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
