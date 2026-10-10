#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BlackHole.Unity
{
    // 원본 수치 변경 기록 한 줄(schema 1). 초안 승격(promote)과 되돌리기(revert, M4), 시트에서 끌어오기(pull)와 시트에 반영(sheet, M5)마다 하나다.
    // 형식은 Docs/BalanceLoop/M4-ai-tuning.md. 사람과 AI가 같은 파일을 읽는다.
    internal sealed class ChangeRecord
    {
        public const int Schema = 1;
        public const string Promote = "promote";
        public const string Revert = "revert";
        // 기획 시트에서 노드 CSV를 끌어왔다(author: sheet). 노드 값이 바뀐 칸만 patches에 있고, 나머지는 summary에 적는다.
        public const string Pull = "pull";
        // 승격·되돌리기한 노드 값을 기획 시트에 썼다. syncOf가 그 기록들이다.
        public const string Sheet = "sheet";

        public string Id;
        public DateTime AtUtc;
        // promote | revert | pull | sheet
        public string Kind;
        // 값을 정한 쪽: ai(AI 초안) | human | sheet(기획 시트)
        public string Author;
        // 반영 버튼을 누른 쪽: human, 또는 시트 자동 끌어오기면 auto. AI는 초안까지만 쓴다.
        public string AppliedBy = "human";
        // 승격한 프로필 이름과 설명. 되돌리기면 되돌린 변경의 것.
        public string Profile;
        public string ProfileNote;
        // 되돌리기면 되돌린 변경 ID.
        public string RevertOf;
        public List<ChangePatch> Patches = new List<ChangePatch>();
        // 원본(프로필 없음) 수치 지문의 앞뒤.
        public string FingerprintBefore;
        public string FingerprintAfter;
        // 근거 메모들의 세팅 키.
        public List<string> SetupKeys = new List<string>();
        // 보관한 초안 파일(레포 기준 경로). 없으면 null.
        public string Archive;
        // 이 기록이 이미 기획 시트와 같은가. pull·sheet 기록은 true다. promote·revert는 false로 쓰고, 뒤의 pull·sheet 기록의 syncOf에 들면 반영된 것이다.
        public bool SheetSynced;
        // pull·sheet: 이 기록으로 시트와 맞춰진 앞 기록 ID.
        public List<string> SyncOf = new List<string>();
        // 사람이 읽는 요약(끌어오기에서 늘거나 준 행, 값 말고 바뀐 칸 등). 없으면 null.
        public string Summary;

        public static string NewId(DateTime atUtc, System.Random random) =>
            $"c-{atUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{random.Next(0x10000):x4}";

        // 이미 되돌린 변경 ID.
        public static HashSet<string> RevertedIds(IEnumerable<ChangeRecord> records)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ChangeRecord record in records)
            {
                if (record.Kind == ChangeRecord.Revert && !string.IsNullOrEmpty(record.RevertOf))
                    ids.Add(record.RevertOf);
            }

            return ids;
        }

        // 시트와 맞춰진 기록 ID: sheetSynced가 true이거나, pull·sheet 기록의 syncOf에 든 것.
        public static HashSet<string> SyncedIds(IEnumerable<ChangeRecord> records)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ChangeRecord record in records)
            {
                if (record.SheetSynced && !string.IsNullOrEmpty(record.Id))
                    ids.Add(record.Id);

                foreach (string id in record.SyncOf)
                    ids.Add(id);
            }

            return ids;
        }

        public JsonObject ToJson()
        {
            var patches = new List<object>(Patches.Count);
            foreach (ChangePatch patch in Patches)
                patches.Add(patch.ToJson());

            return new JsonObject
            {
                { "schema", Schema },
                { "id", Id },
                { "atUtc", AtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) },
                { "kind", Kind },
                { "author", Author },
                { "appliedBy", AppliedBy },
                { "profile", Profile },
                { "profileNote", ProfileNote },
                { "revertOf", RevertOf },
                { "patches", patches },
                { "fingerprintBefore", FingerprintBefore },
                { "fingerprintAfter", FingerprintAfter },
                { "setupKeys", new List<object>(SetupKeys) },
                { "archive", Archive },
                { "sheetSynced", SheetSynced },
                { "syncOf", new List<object>(SyncOf) },
                { "summary", Summary },
            };
        }

        public static ChangeRecord FromJson(JsonObject obj)
        {
            var record = new ChangeRecord
            {
                Id = obj.Text("id"),
                Kind = obj.Text("kind"),
                Author = obj.Text("author"),
                AppliedBy = obj.Text("appliedBy"),
                Profile = obj.Text("profile"),
                ProfileNote = obj.Text("profileNote"),
                RevertOf = obj.Text("revertOf"),
                FingerprintBefore = obj.Text("fingerprintBefore"),
                FingerprintAfter = obj.Text("fingerprintAfter"),
                Archive = obj.Text("archive"),
                SheetSynced = obj["sheetSynced"] is bool synced && synced,
                Summary = obj.Text("summary"),
            };

            if (DateTime.TryParse(obj.Text("atUtc"), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime at))
                record.AtUtc = at;

            foreach (object item in obj.Array("patches") ?? new List<object>())
            {
                if (item is JsonObject patch)
                    record.Patches.Add(ChangePatch.FromJson(patch));
            }

            foreach (object item in obj.Array("setupKeys") ?? new List<object>())
            {
                if (item is string key)
                    record.SetupKeys.Add(key);
            }

            foreach (object item in obj.Array("syncOf") ?? new List<object>())
            {
                if (item is string id)
                    record.SyncOf.Add(id);
            }

            return record;
        }
    }

    // 바꾼 값 하나: 경로, 원본 칸, 이전·이후 값, 이유, 근거 메모.
    internal sealed class ChangePatch
    {
        public string Path;
        public string Target;
        public double Before;
        public double After;
        public string Reason;
        public List<string> NoteIds = new List<string>();

        public JsonObject ToJson() => new JsonObject
        {
            { "path", Path },
            { "target", Target },
            { "before", Before },
            { "after", After },
            { "reason", Reason },
            { "noteIds", new List<object>(NoteIds) },
        };

        public static ChangePatch FromJson(JsonObject obj)
        {
            var patch = new ChangePatch
            {
                Path = obj.Text("path"),
                Target = obj.Text("target"),
                Before = obj.Number("before") ?? 0,
                After = obj.Number("after") ?? 0,
                Reason = obj.Text("reason"),
            };

            foreach (object item in obj.Array("noteIds") ?? new List<object>())
            {
                if (item is string id)
                    patch.NoteIds.Add(id);
            }

            return patch;
        }
    }
}
#endif
