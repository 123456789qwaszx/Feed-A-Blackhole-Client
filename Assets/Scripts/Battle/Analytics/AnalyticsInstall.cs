using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace BlackHole.Unity
{
    // 이 설치의 ID와 판 번호. 통계 폴더의 install.json에 둔다 — 앱을 지우면 함께 지워져 새 설치가 된다.
    // 진행 저장과 따로 둔다: 새 게임을 시작해도 같은 설치이고, 통계가 진행 저장 형식에 끼어들지 않는다.
    // 파일이 없거나 읽지 못하면 새 ID로 처음부터 센다(같은 기기라도 분석에서는 다른 설치로 보인다).
    internal sealed class AnalyticsInstall
    {
        private const string FileName = "install.json";
        private static readonly UTF8Encoding Utf8NoBom = new(false);

        private readonly string _path;
        private readonly Data _data;

        private AnalyticsInstall(string path, Data data)
        {
            _path = path;
            _data = data;
        }

        public string InstallId => _data.installId;

        public static AnalyticsInstall Load(string directory)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, FileName);

            if (TryRead(path, out Data saved))
                return new AnalyticsInstall(path, saved);

            Data created = new() { installId = Guid.NewGuid().ToString(), battleIndex = 0 };
            AnalyticsInstall install = new(path, created);
            install.Write();
            return install;
        }

        // 판을 시작할 때 부른다. 다음 판 번호(1부터)를 정하고 바로 적는다 — 판이 끝나기 전에 앱이 꺼져도 번호가 겹치지 않는다.
        public int NextBattleIndex()
        {
            _data.battleIndex++;
            Write();
            return _data.battleIndex;
        }

        // 임시 파일에 다 쓴 뒤 바꿔치기한다. 쓰는 도중 앱이 꺼져도 앞서 적은 파일이 남는다.
        private void Write()
        {
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(_data), Utf8NoBom);

            if (File.Exists(_path))
                File.Replace(tmp, _path, null);
            else
                File.Move(tmp, _path);
        }

        private static bool TryRead(string path, out Data data)
        {
            data = null;

            if (!File.Exists(path))
                return false;

            try
            {
                data = JsonUtility.FromJson<Data>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (ArgumentException)
            {
                data = null;
            }

            if (data != null && Guid.TryParse(data.installId, out _) && data.battleIndex >= 0)
                return true;

            Debug.LogWarning($"[통계] 설치 정보를 읽지 못해 새 설치로 센다: {path}");
            return false;
        }

        [Serializable]
        private sealed class Data
        {
            public string installId;
            public int battleIndex;
        }
    }
}
