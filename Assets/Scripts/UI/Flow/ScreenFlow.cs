using System;
using System.Collections.Generic;

namespace BlackHole.Unity
{
    // 화면 연결과 전환. 화면 버튼의 사건을 받아 다음 화면을 연다.
    // 전투는 아직 없다 — 타이틀 → 업그레이드 → 전투 → 결산 → 업그레이드를 버튼으로만 오가고, 화면에 게임 값을 넘기지 않는다.
    internal sealed partial class ScreenFlow : IDisposable
    {
        private readonly UIManager _ui;
        private readonly UIPresentationSpec _titlePresentation;
        private readonly UIPresentationSpec _upgradePresentation;
        private readonly UIPresentationSpec _battlePresentation;
        private readonly UIPresentationSpec _settlementPresentation;
        private readonly Dictionary<UIBase, List<Action>> _cleanupByScreen = new Dictionary<UIBase, List<Action>>();

        public ScreenFlow(UIManager ui, UIPresentationSpec titlePresentation, UIPresentationSpec upgradePresentation,
            UIPresentationSpec battlePresentation, UIPresentationSpec settlementPresentation)
        {
            _ui = ui;
            _titlePresentation = titlePresentation;
            _upgradePresentation = upgradePresentation;
            _battlePresentation = battlePresentation;
            _settlementPresentation = settlementPresentation;
        }

        private void BindView<T>(T screen, Action<T> apply) where T : UIBase
        {
            Unbind(screen);
            apply(screen);
        }

        private void AddBinding<T>(T screen, Action<T> attach, Action<T> detach) where T : UIBase
        {
            attach(screen);
            AddCleanup(screen, () => detach(screen));
        }

        private void AddCleanup(UIBase screen, Action cleanup)
        {
            if (!_cleanupByScreen.TryGetValue(screen, out List<Action> cleanups))
            {
                cleanups = new List<Action>();
                _cleanupByScreen[screen] = cleanups;
            }

            cleanups.Add(cleanup);
        }

        private void Unbind(UIBase screen)
        {
            if (!_cleanupByScreen.TryGetValue(screen, out List<Action> cleanups))
                return;

            _cleanupByScreen.Remove(screen);
            RunCleanups(cleanups);
        }

        private static void RunCleanups(List<Action> cleanups)
        {
            for (int i = cleanups.Count - 1; i >= 0; i--)
                cleanups[i]?.Invoke();
        }

        public void Dispose()
        {
            foreach (List<Action> cleanups in _cleanupByScreen.Values)
                RunCleanups(cleanups);

            _cleanupByScreen.Clear();
        }
    }
}
