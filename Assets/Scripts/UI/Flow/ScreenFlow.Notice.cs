using BlackHole.Net;
using UnityEngine;

namespace BlackHole.Unity
{
    // 서버 거부 알림(W3-5, 계약 5.9). 마지막 동기화에 거부가 있고 아직 보여 주지 않았으면, 타이틀·업그레이드 화면에서 한 번 글자로 알린다.
    // - 화면에 들어갈 때(ShowTitle, ShowUpgrade): 시작 동기화나 판 중에 받아 둔 거부를 보인다.
    // - 업그레이드 화면에 있는 동안 거부가 오면(GameBootstrap이 SyncSender.RejectionReceived를 연결): 서버 값으로 덮어쓴 진행도를
    //   노드 트리와 Gold에 다시 그리고 바로 보인다. 타이틀이면 바로 보인다. 전투·결산 중이면 두었다가 다음에 두 화면에 들어갈 때 보인다.
    // 글자를 보이기 시작할 때 "보여 줬다"로 기록한다(눌러서 닫는 창이 아니므로).
    internal sealed partial class ScreenFlow
    {
        private RejectionNotice _rejectionNotice;

        // GameBootstrap이 SyncSender.RejectionReceived에 연결한다. 메인 스레드에서 불린다.
        public void HandleSyncRejection()
        {
            if (_ui.CurrentRoot is UpgradeScreen)
                RefreshUpgrade();

            ShowRejectionNoticeIfDue();
        }

        // 타이틀·업그레이드 화면이 열려 있고 보여 줄 거부가 있으면 보인다.
        private void ShowRejectionNoticeIfDue()
        {
            if (!(_ui.CurrentRoot is TitleScreen) && !(_ui.CurrentRoot is UpgradeScreen))
                return;

            RejectedLogDto rejection = _progressStore.LastRejection;

            if (rejection == null || _progressStore.LastRejectionShown)
                return;

            string kind = _localizer.Get(RejectionKinds.TextKeyOf(rejection.type));
            EnsureRejectionNotice().Show(_localizer.Format("sync.rejection", kind, rejection.code));
            _progressStore.MarkLastRejectionShown();
        }

        private RejectionNotice EnsureRejectionNotice()
        {
            if (_rejectionNotice != null)
                return _rejectionNotice;

            var style = new UIStyle(_settings.TextScale, _fonts, _settings.LanguageLocale.ToId(), null, null, null);
            Transform canvas = _transition.transform.parent;

            // 엔딩 화면과 같이 화면 전환 덮개 바로 아래에 둔다.
            _rejectionNotice = RejectionNotice.Create(
                canvas,
                _transition.transform.GetSiblingIndex(),
                style.FontFor(UITextRole.Body),
                style.Scale);

            return _rejectionNotice;
        }

        private void DisposeRejectionNotice()
        {
            if (_rejectionNotice != null)
                Object.Destroy(_rejectionNotice.gameObject);

            _rejectionNotice = null;
        }
    }
}
