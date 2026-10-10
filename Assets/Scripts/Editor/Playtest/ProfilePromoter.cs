using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;
using UnityEngine;

namespace BlackHole.EditorTools
{
    // AI 초안의 승격·되돌리기(M4). 사람이 테스트 세팅 창의 버튼으로 부른다(AI는 초안까지만 쓴다).
    // - 승격: 초안의 패치마다 원본 칸(ProfileTargets)을 찾아 이전 값을 읽는다. 모두 찾았을 때만 한 번에 고친다(부분 반영 없음).
    //   노드 경로는 노드 CSV의 그 칸(NodeSheetEdits — 서식 유지), 나머지는 게임 콘텐츠 세트가 가리키는 에셋의 그 칸(SerializedObject)이다.
    //   고친 뒤 변경 기록(PlaytestData/changes.ndjson)에 남기고, 초안 파일은 PlaytestData/drafts/로 옮긴다.
    // - 되돌리기: 기록의 이전 값으로 같은 길을 간다. 그 뒤에 값이 또 바뀌었으면(지금 값 ≠ 기록의 이후 값) 하지 않는다.
    // 고친 파일은 수치 감시(LiveDataWatcher)가 잡아 창·플레이 중인 판에 바로 반영한다.
    internal static class ProfilePromoter
    {
        private sealed class Request
        {
            public string Path;
            public double Value;
            // 되돌리기: 지금 값이 이 값이어야 한다. 승격이면 null.
            public double? Expected;
            public string Reason;
            public List<string> NoteIds = new List<string>();
        }

        private sealed class Edit
        {
            public Request Request;
            public ProfileTarget Target;
            public double Before;
            public string CsvPath;
            public SerializedObject Serialized;
            public SerializedProperty Property;
        }

        private sealed class Plan
        {
            public readonly List<Edit> Edits = new List<Edit>();
            public readonly Dictionary<string, string> CsvTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<UnityEngine.Object, SerializedObject> Assets = new Dictionary<UnityEngine.Object, SerializedObject>();
        }

        public static bool HasDraft => File.Exists(AiContextService.DraftPath);

        public static BalanceProfile ReadDraft(out string error)
        {
            error = null;

            if (!HasDraft)
                return null;

            try
            {
                return BalanceProfile.Parse(File.ReadAllText(AiContextService.DraftPath), out error);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = exception.Message;
                return null;
            }
        }

        // 지금 원본 칸의 값(창에 이전 값을 보일 때).
        public static bool TryReadCurrent(string path, out double value, out string error)
        {
            value = 0;
            GameContentSetup setup = FindAsset<GameContentSetup>();

            if (setup == null)
            {
                error = "게임 콘텐츠 세트(GameContentSetup) 에셋을 찾지 못했다.";
                return false;
            }

            var plan = new Plan();
            Edit edit = Resolve(new Request { Path = path, Value = double.NaN }, setup, plan, readOnly: true, out error);

            if (edit == null)
                return false;

            value = edit.Before;
            return true;
        }

        public static bool Promote(out ChangeRecord record, out List<string> errors)
        {
            record = null;
            errors = new List<string>();
            BalanceProfile draft = ReadDraft(out string readError);

            if (draft == null)
            {
                errors.Add(readError ?? "AI 초안이 없다.");
                return false;
            }

            var requests = new List<Request>();
            foreach (BalanceProfile.Patch patch in draft.patches)
            {
                if (patch == null)
                    continue;

                requests.Add(new Request
                {
                    Path = patch.path,
                    Value = patch.value,
                    Reason = patch.reason,
                    NoteIds = patch.noteIds ?? new List<string>(),
                });
            }

            if (!Apply(requests, errors, out List<Edit> edits, out string before, out string after))
                return false;

            record = NewRecord(ChangeRecord.Promote, draft.name == BalanceProfile.DraftName ? "ai" : "human", edits, before, after);
            record.Profile = draft.name;
            record.ProfileNote = draft.note;
            record.Archive = Archive(record.Id);
            Save(record, errors);
            return true;
        }

        public static bool Revert(ChangeRecord change, out ChangeRecord record, out List<string> errors)
        {
            record = null;
            errors = new List<string>();
            var requests = new List<Request>();

            // 뒤에서부터(같은 칸을 두 번 바꿨어도 처음 값으로 돌아간다).
            for (int i = change.Patches.Count - 1; i >= 0; i--)
            {
                ChangePatch patch = change.Patches[i];
                requests.Add(new Request
                {
                    Path = patch.Path,
                    Value = patch.Before,
                    Expected = patch.After,
                    Reason = $"되돌리기 {change.Id}",
                    NoteIds = patch.NoteIds,
                });
            }

            if (!Apply(requests, errors, out List<Edit> edits, out string before, out string after))
                return false;

            record = NewRecord(ChangeRecord.Revert, "human", edits, before, after);
            record.Profile = change.Profile;
            record.ProfileNote = change.ProfileNote;
            record.RevertOf = change.Id;
            Save(record, errors);
            return true;
        }

        // 초안을 원본에 반영하지 않고 치운다(PlaytestData/drafts/로 옮김).
        public static bool Discard(out string archive, out string error)
        {
            archive = null;
            error = null;

            if (!HasDraft)
            {
                error = "AI 초안이 없다.";
                return false;
            }

            archive = Archive("discarded-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
            return archive != null;
        }

        private static bool Apply(List<Request> requests, List<string> errors, out List<Edit> edits, out string fingerprintBefore, out string fingerprintAfter)
        {
            edits = null;
            fingerprintBefore = null;
            fingerprintAfter = null;
            GameContentSetup setup = FindAsset<GameContentSetup>();

            if (setup == null)
            {
                errors.Add("게임 콘텐츠 세트(GameContentSetup) 에셋을 찾지 못했다.");
                return false;
            }

            if (requests.Count == 0)
            {
                errors.Add("바꿀 값이 없다.");
                return false;
            }

            var plan = new Plan();

            foreach (Request request in requests)
            {
                Edit edit = Resolve(request, setup, plan, readOnly: false, out string error);

                if (edit == null)
                    errors.Add($"'{request.Path}': {error}");
                else
                    plan.Edits.Add(edit);
            }

            if (errors.Count > 0)
                return false;

            fingerprintBefore = Fingerprint(setup);

            // 1. CSV: 바뀐 글을 한 파일에 한 번 쓴다(BOM 여부는 원래대로).
            foreach (KeyValuePair<string, string> csv in plan.CsvTexts)
            {
                bool bom = HasBom(csv.Key);
                File.WriteAllText(csv.Key, csv.Value, new UTF8Encoding(bom));
            }

            // 2. 에셋: 칸을 고치고 저장한다.
            foreach (KeyValuePair<UnityEngine.Object, SerializedObject> asset in plan.Assets)
            {
                asset.Value.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset.Key);
                AssetDatabase.SaveAssetIfDirty(asset.Key);
            }

            foreach (string path in plan.CsvTexts.Keys)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            fingerprintAfter = Fingerprint(setup);
            edits = plan.Edits;
            return true;
        }

        // 요청 하나 → 원본 칸과 이전 값. readOnly가 아니면 계획에 새 값을 넣어 둔다(아직 파일·에셋에 쓰지 않는다).
        private static Edit Resolve(Request request, GameContentSetup setup, Plan plan, bool readOnly, out string error)
        {
            if (!ProfileTargets.TryMap(request.Path, out ProfileTarget target, out error))
                return null;

            if (!readOnly && !BalanceProfilePatcher.Accepts(target.Kind, request.Value, out error))
                return null;

            var edit = new Edit { Request = request, Target = target };

            if (target.IsCsv)
            {
                NodeContentSource source = setup.Nodes != null ? setup.Nodes.Content : null;
                TextAsset csv = source == null ? null
                    : target.Source == TargetSource.NodeCostCsv ? source.NodeCostCsv : source.NodeEffectsCsv;

                if (csv == null)
                {
                    error = "노드 콘텐츠에 그 CSV가 연결되어 있지 않다.";
                    return null;
                }

                edit.CsvPath = AssetDatabase.GetAssetPath(csv);

                if (!plan.CsvTexts.TryGetValue(edit.CsvPath, out string text))
                    text = File.ReadAllText(edit.CsvPath);

                string result;
                bool ok;

                if (target.Source == TargetSource.NodeCostCsv)
                {
                    ok = readOnly
                        ? NodeSheetEdits.TryGetCost(text, target.NodeId, target.Rank, out long cost, out error) && Set(ref edit.Before, cost)
                        : NodeSheetEdits.TrySetCost(text, target.NodeId, target.Rank, (long)request.Value, out result, out long before, out error)
                          && Set(ref edit.Before, before) && Keep(plan, edit.CsvPath, result);
                }
                else
                {
                    ok = readOnly
                        ? NodeSheetEdits.TryGetEffect(text, target.NodeId, target.Rank, target.StatId, out double value, out error) && Set(ref edit.Before, value)
                        : NodeSheetEdits.TrySetEffect(text, target.NodeId, target.Rank, target.StatId, request.Value, out result, out double before, out error)
                          && Set(ref edit.Before, before) && Keep(plan, edit.CsvPath, result);
                }

                if (!ok)
                    return null;
            }
            else
            {
                UnityEngine.Object asset = AssetOf(setup, target, out error);

                if (asset == null)
                    return null;

                if (!plan.Assets.TryGetValue(asset, out SerializedObject serialized))
                {
                    serialized = new SerializedObject(asset);
                    plan.Assets.Add(asset, serialized);
                }

                SerializedProperty property = PropertyOf(serialized, target, out error);

                if (property == null)
                    return null;

                if (!Read(property, target.Kind, out edit.Before, out error))
                    return null;

                edit.Serialized = serialized;
                edit.Property = property;

                if (!readOnly)
                    Write(property, target.Kind, request.Value);
            }

            if (request.Expected.HasValue && !Same(edit.Before, request.Expected.Value))
            {
                error = $"그 뒤에 값이 바뀌었다(기록의 이후 값 {request.Expected.Value}, 지금 {edit.Before}). 손으로 확인한다.";
                return null;
            }

            error = null;
            return edit;
        }

        private static bool Set(ref double into, double value)
        {
            into = value;
            return true;
        }

        private static bool Keep(Plan plan, string path, string text)
        {
            plan.CsvTexts[path] = text;
            return true;
        }

        private static UnityEngine.Object AssetOf(GameContentSetup setup, ProfileTarget target, out string error)
        {
            error = null;
            UnityEngine.Object asset;

            switch (target.Source)
            {
                case TargetSource.BattleRules:
                    asset = setup.BattleRules;
                    break;
                case TargetSource.SkillSetup:
                    asset = setup.Skills;
                    break;
                case TargetSource.EnemySupplySetup:
                    asset = setup.Supply;
                    break;
                case TargetSource.HqGrowthSetup:
                    asset = setup.Growth;
                    break;
                case TargetSource.EnemyKind:
                    asset = null;

                    if (setup.Enemies != null)
                    {
                        foreach (EnemyKind kind in setup.Enemies.Kinds())
                        {
                            if (kind != null && ProfileTargets.KeyOf(kind.Type.ToString()) == target.EnemyKey)
                                asset = kind;
                        }
                    }

                    if (asset == null)
                        error = $"적 종류 목록에 '{target.EnemyKey}' 에셋이 없다.";

                    return asset;
                default:
                    asset = null;
                    break;
            }

            if (asset == null)
                error = $"게임 콘텐츠 세트에 {target.Source} 에셋이 연결되어 있지 않다.";

            return asset;
        }

        private static SerializedProperty PropertyOf(SerializedObject serialized, ProfileTarget target, out string error)
        {
            error = null;

            if (target.PropertyPath != null)
            {
                SerializedProperty property = serialized.FindProperty(target.PropertyPath);

                if (property == null)
                    error = $"에셋에 칸이 없다: {target.PropertyPath}.";

                return property;
            }

            SerializedProperty array = serialized.FindProperty(target.ArrayProperty);

            if (array == null || !array.isArray)
            {
                error = $"에셋에 목록 칸이 없다: {target.ArrayProperty}.";
                return null;
            }

            for (int i = 0; i < array.arraySize; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                SerializedProperty key = element.FindPropertyRelative(target.KeyField);

                if (key == null || KeyOf(key) != target.Key)
                    continue;

                SerializedProperty field = element.FindPropertyRelative(target.ElementField);

                if (field == null)
                    error = $"에셋에 칸이 없다: {target.ArrayProperty}[{target.Key}].{target.ElementField}.";

                return field;
            }

            error = target.ArrayProperty == "startSupply"
                ? $"시작 공급에 '{target.Key}'이 없다. 승격은 있는 항목만 고친다(에셋에서 직접 더한다)."
                : $"에셋의 {target.ArrayProperty}에 '{target.Key}'이 없다.";
            return null;
        }

        // 키 칸(성질 종류 열거형, 공급 종류 에셋)의 경로 키.
        private static string KeyOf(SerializedProperty key)
        {
            switch (key.propertyType)
            {
                case SerializedPropertyType.Enum:
                    return key.enumValueIndex >= 0 && key.enumValueIndex < key.enumNames.Length
                        ? ProfileTargets.KeyOf(key.enumNames[key.enumValueIndex])
                        : null;
                case SerializedPropertyType.ObjectReference:
                    return key.objectReferenceValue is EnemyKind kind ? ProfileTargets.KeyOf(kind.Type.ToString()) : null;
                default:
                    return null;
            }
        }

        private static bool Read(SerializedProperty property, BalanceProfilePatcher.SlotKind kind, out double value, out string error)
        {
            error = null;
            value = 0;

            switch (kind)
            {
                case BalanceProfilePatcher.SlotKind.Real when property.propertyType == SerializedPropertyType.Float:
                    value = BalanceProfilePatcher.Decimal(property.floatValue);
                    return true;
                case BalanceProfilePatcher.SlotKind.Int when property.propertyType == SerializedPropertyType.Integer:
                    value = property.intValue;
                    return true;
                case BalanceProfilePatcher.SlotKind.Long when property.propertyType == SerializedPropertyType.Integer:
                    value = property.longValue;
                    return true;
                default:
                    error = $"칸의 종류가 다르다: {property.propertyPath}는 {property.propertyType}, 경로는 {kind}.";
                    return false;
            }
        }

        private static void Write(SerializedProperty property, BalanceProfilePatcher.SlotKind kind, double value)
        {
            switch (kind)
            {
                case BalanceProfilePatcher.SlotKind.Real:
                    property.floatValue = (float)value;
                    break;
                case BalanceProfilePatcher.SlotKind.Int:
                    property.intValue = (int)value;
                    break;
                default:
                    property.longValue = (long)value;
                    break;
            }
        }

        private static bool Same(double a, double b) => Math.Abs(a - b) <= 1e-6 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));

        private static ChangeRecord NewRecord(string kind, string author, List<Edit> edits, string before, string after)
        {
            var record = new ChangeRecord
            {
                Id = ChangeRecord.NewId(DateTime.UtcNow, new System.Random()),
                AtUtc = DateTime.UtcNow,
                Kind = kind,
                Author = author,
                FingerprintBefore = before,
                FingerprintAfter = after,
            };

            var noteIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (Edit edit in edits)
            {
                record.Patches.Add(new ChangePatch
                {
                    Path = edit.Request.Path,
                    Target = edit.Target.ToString(),
                    Before = edit.Before,
                    After = edit.Request.Value,
                    Reason = edit.Request.Reason,
                    NoteIds = new List<string>(edit.Request.NoteIds),
                });

                foreach (string id in edit.Request.NoteIds)
                    noteIds.Add(id);
            }

            // 근거 메모들의 세팅 키.
            foreach (JsonObject note in PlaytestNotes.ReadRaw())
            {
                string key = note.Text("setupKey");

                if (key != null && noteIds.Contains(note.Text("id")) && !record.SetupKeys.Contains(key))
                    record.SetupKeys.Add(key);
            }

            return record;
        }

        private static void Save(ChangeRecord record, List<string> errors)
        {
            if (PlaytestChanges.TryAppend(record, out string error))
            {
                Debug.Log($"[AI 조정] {record.Kind} {record.Id}: 값 {record.Patches.Count}개 · 지문 {record.FingerprintBefore} → {record.FingerprintAfter}");
                return;
            }

            // 값은 이미 바뀌었다. 기록을 잃지 않게 콘솔에 남긴다.
            errors.Add($"변경 기록을 쓰지 못했다: {error}");
            Debug.LogError($"[AI 조정] 변경 기록을 쓰지 못했다({error}). 기록:\n{PlaytestJson.Write(record.ToJson())}");
        }

        // 초안을 PlaytestData/drafts/<name>.json으로 옮기고 에셋을 지운다. 고른 프로필이 초안이면 원본으로 돌린다.
        private static string Archive(string name)
        {
            string draft = AiContextService.DraftPath;

            if (!File.Exists(draft))
                return null;

            try
            {
                Directory.CreateDirectory(PlaytestChanges.DraftsFolder);
                File.Copy(draft, Path.Combine(PlaytestChanges.DraftsFolder, name + ".json"), true);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[AI 조정] 초안을 보관하지 못했다: {exception.Message}");
                return null;
            }

            if (PlayerPrefs.GetString(PlaytestSession.ProfileKey, "") == BalanceProfile.DraftName)
                PlaytestSession.SelectProfile(string.Empty);

            AssetDatabase.DeleteAsset(draft);
            return $"{PlaytestNotes.DataFolderName}/drafts/{name}.json";
        }

        private static string Fingerprint(GameContentSetup setup)
        {
            var errors = new List<ContentDiagnostic>();
            NodeContentData nodes = setup.Nodes.Content.Read(errors);
            return ContentFingerprint.Of(setup.ToData(), nodes);
        }

        private static bool HasBom(string path)
        {
            byte[] head = new byte[3];

            using (FileStream stream = File.OpenRead(path))
            {
                int read = stream.Read(head, 0, 3);
                return read == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
            }
        }

        private static T FindAsset<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
