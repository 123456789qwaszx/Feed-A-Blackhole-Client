using UnityEngine;

namespace BlackHole.Unity
{
    // 통계 서버 설정 에셋. GameBootstrap에 연결한다. 연결하지 않았거나 꺼 두면 통계를 만들지도 보내지도 않는다.
    // 주소 예:
    // - 에디터: http://localhost:8080
    // - USB로 연결한 Android 기기: 같은 주소에 adb reverse tcp:8080 tcp:8080(기기의 8080을 PC로 잇는다).
    // Android 9부터 http(평문)는 막혀 있다. 기기에서 http로 보내려면 Player Settings의 Allow downloads over HTTP를 켠다.
    [CreateAssetMenu(fileName = "AnalyticsSettings", menuName = "BlackHole/Analytics Settings")]
    public sealed class AnalyticsSettings : ScriptableObject
    {
        [Tooltip("끄면 통계를 만들지도 보내지도 않는다.")]
        [SerializeField] private bool _enabled = true;
        [Tooltip("통계 서버 주소(경로 없이). 예: http://localhost:8080")]
        [SerializeField] private string _baseUrl = "http://localhost:8080";
        [Tooltip("요청 하나를 기다리는 초. 넘으면 닿지 않은 것으로 보고 큐에 남겨 다음 기회에 보낸다.")]
        [Min(1)]
        [SerializeField] private int _timeoutSeconds = 10;

        // 켜져 있고 주소가 있다.
        public bool Enabled => _enabled && !string.IsNullOrWhiteSpace(_baseUrl);
        public string BaseUrl => _baseUrl;
        public int TimeoutSeconds => _timeoutSeconds;
    }
}
