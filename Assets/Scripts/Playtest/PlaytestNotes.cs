#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace BlackHole.Unity
{
    // 플레이 메모: 한 줄에 JSON 하나(ndjson)로 기기에 쌓는다(PlaytestFiles.Notes).
    // 판 ID로 전투 요약과 잇는다 — 숫자(전투 요약)와 느낌(메모)을 같이 AI에게 보여 준다.
    internal static class PlaytestNotes
    {
        [Serializable]
        private sealed class Line
        {
            public string atUtc;
            public string battleId;
            public string buildVersion;
            public string contentVersion;
            // 판이 없으면 -1.
            public float elapsed;
            public int level;
            public int growthStage;
            public string text;
        }

        // 저장하면 true. 실패하면 error에 이유가 있다.
        public static bool TryAppend(
            string text,
            string battleId,
            string contentVersion,
            float elapsed,
            int level,
            int growthStage,
            out string error)
        {
            var line = new Line
            {
                atUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                battleId = battleId ?? "",
                buildVersion = Application.version,
                contentVersion = contentVersion ?? "",
                elapsed = elapsed,
                level = level,
                growthStage = growthStage,
                text = text,
            };

            try
            {
                Directory.CreateDirectory(PlaytestFiles.Root);
                File.AppendAllText(PlaytestFiles.Notes, JsonUtility.ToJson(line) + "\n");
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
#endif
