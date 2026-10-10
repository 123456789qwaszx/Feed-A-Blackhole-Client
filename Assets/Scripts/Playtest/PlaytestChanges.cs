#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BlackHole.Unity
{
    // 변경 기록 파일(한 줄에 JSON 하나). 에디터는 <레포>/PlaytestData/changes.ndjson(git 무시). 덧붙이기만 한다.
    // 보관한 초안은 PlaytestData/drafts/에 둔다.
    internal static class PlaytestChanges
    {
        private const string FileName = "changes.ndjson";

        public static string FilePath => Path.Combine(PlaytestNotes.DataFolder, FileName);
        public static string DraftsFolder => Path.Combine(PlaytestNotes.DataFolder, "drafts");

        public static (DateTime Write, long Length) Stamp()
        {
            try
            {
                var file = new FileInfo(FilePath);
                return file.Exists ? (file.LastWriteTimeUtc, file.Length) : default;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return default;
            }
        }

        public static bool TryAppend(ChangeRecord record, out string error)
        {
            try
            {
                Directory.CreateDirectory(PlaytestNotes.DataFolder);
                File.AppendAllText(FilePath, PlaytestJson.Write(record.ToJson()) + "\n");
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = exception.Message;
                return false;
            }
        }

        // 원문 JSON(파일 순서).
        public static List<JsonObject> ReadRaw()
        {
            try
            {
                return File.Exists(FilePath)
                    ? PlaytestJson.ParseLines(File.ReadAllLines(FilePath), ChangeRecord.Schema, out _)
                    : new List<JsonObject>();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[변경 기록] {FilePath}를 읽지 못했다: {exception.Message}");
                return new List<JsonObject>();
            }
        }

        public static List<ChangeRecord> ReadAll()
        {
            var records = new List<ChangeRecord>();
            foreach (JsonObject obj in ReadRaw())
                records.Add(ChangeRecord.FromJson(obj));

            return records;
        }
    }
}
#endif
