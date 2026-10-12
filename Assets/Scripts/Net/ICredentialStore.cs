namespace BlackHole.Net
{
    // 계정 정보를 읽고 쓴다. 실제 구현은 BlackHole.Net.Unity(진행 저장 폴더의 account.json, 계약 2.5).
    public interface ICredentialStore
    {
        // 없거나 읽지 못하면 null. 읽지 못한 파일은 구현이 백업으로 옮긴다.
        GuestCredentials Load();

        // 원자적으로 쓴다(임시 파일에 쓴 뒤 바꿔치기). 실패하면 예외.
        void Save(GuestCredentials credentials);
    }
}
