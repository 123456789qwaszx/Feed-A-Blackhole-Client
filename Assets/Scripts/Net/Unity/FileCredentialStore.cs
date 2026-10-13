using System;
using System.IO;
using UnityEngine;

namespace BlackHole.Net.Unity
{
    /// <summary>
    /// account.json에 게스트 계정 정보를 저장한다.
    /// 손상된 파일은 백업으로 옮기며, 저장은 임시 파일 작성 후 교체
    /// </summary>
    public sealed class FileCredentialStore : ICredentialStore
    {
        private readonly string _accountPath;
        private readonly string _backupPath;
        private readonly string _tempPath;

        public FileCredentialStore(string saveDirectory)
        {
            if (string.IsNullOrWhiteSpace(saveDirectory))
                throw new ArgumentException("저장 폴더 경로가 필요합니다.", nameof(saveDirectory));

            _accountPath = Path.Combine(saveDirectory, "account.json");
            _backupPath = _accountPath + ".bak";
            _tempPath = _accountPath + ".tmp";
        }

        public GuestCredentials Load()
        {
            if (!File.Exists(_accountPath)) return null;

            try
            {
                string json = File.ReadAllText(_accountPath);
                var data = JsonUtility.FromJson<CredentialData>(json);

                if (data == null ||
                    string.IsNullOrEmpty(data.guestId) ||
                    string.IsNullOrEmpty(data.guestSecret))
                {
                    BackupCorruptedFile();
                    return null;
                }

                return new GuestCredentials(data.guestId, data.guestSecret, data.refreshToken);
            }
            catch (Exception e)
            {
                // 계약: 읽지 못하는 파일은 백업으로 옮기고 계정 정보 없음으로 처리한다.
                BackupCorruptedFile();
                return null;
            }
        }

        public void Save(GuestCredentials credentials)
        {
            if (credentials == null) throw new ArgumentNullException(nameof(credentials));

            string directory = Path.GetDirectoryName(_accountPath);
            Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(new CredentialData
            {
                guestId = credentials.GuestId,
                guestSecret = credentials.GuestSecret,
                refreshToken = credentials.RefreshToken,
            });

            // 같은 디렉터리에 임시 파일을 작성해 같은 볼륨 안에서 교체한다.
            File.WriteAllText(_tempPath, json);

            try
            {
                if (File.Exists(_accountPath))
                {
                    // File.Replace는 대상 파일이 존재할 때 원자적 교체를 지원한다.
                    File.Replace(_tempPath, _accountPath, null);
                }
                else
                {
                    // 최초 저장: 대상이 없을 때 같은 디렉터리에서 이동한다.
                    File.Move(_tempPath, _accountPath);
                }
            }
            finally
            {
                // 교체 실패 시 남은 임시 파일을 정리한다.
                if (File.Exists(_tempPath))
                    File.Delete(_tempPath);
            }
        }

        private void BackupCorruptedFile()
        {
            if (!File.Exists(_accountPath)) return;

            try
            {
                string backupPath = _backupPath;
                if (File.Exists(backupPath))
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                    backupPath = $"{_accountPath}.{timestamp}.bak";
                }

                File.Move(_accountPath, backupPath);
            }
            catch (IOException)
            {
                // 백업 이동 자체가 실패해도 Load 계약상 null을 반환
            }
            catch (UnauthorizedAccessException)
            {
                // 파일 권한 문제로 백업할 수 없어도 계정 정보를 로드하지 않는다
            }
        }

        [Serializable]
        private sealed class CredentialData
        {
            public string guestId;
            public string guestSecret;
            public string refreshToken;
        }
    }
}
