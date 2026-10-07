using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow : IDisposable
    {
        private readonly UIManager _ui;
        private readonly ScreenPresentations _presentations;
        private readonly BattleSystem _battle;
        private readonly ProgressState _progress;
        private readonly ProgressStore _progressStore;
        private readonly NodeTree _tree;
        private readonly IReadOnlyList<NodeTreeView.NodeItem> _nodes;// 업그레이드 화면에 그릴 노드(칸, 가격).
        private readonly HqGrowthDefinition _growth;// 블랙홀 성장(성장도별 Level 표, 이정표).
        private readonly GameSettings _settings;// 플레이어 설정.
        private readonly ScreenTransition _transition;
        private readonly Dictionary<UIBase, List<Action>> _cleanupByScreen = new();

        public ScreenFlow(
            UIManager ui,
            ScreenPresentations presentations,
            BattleSystem battle,
            ProgressState progress,
            ProgressStore progressStore,
            NodeTree tree,
            IReadOnlyList<NodeTreeView.NodeItem> nodes,
            HqGrowthDefinition growth,
            GameSettings settings,
            ScreenTransition transition)
        {
            _ui = ui;
            _presentations = presentations;
            _battle = battle;
            _progress = progress;
            _progressStore = progressStore;
            _tree = tree;
            _nodes = nodes;
            _growth = growth;
            _settings = settings;
            _transition = transition;
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
