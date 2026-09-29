using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 화면 연결과 전환. 버튼과 시간 종료에서 전투 수명을 요청하고, 성공 결과로 다음 화면을 연다.
    // 타이틀(모드 선택 패널) → 업그레이드(노드 트리 페이지) → 전투 → 결산 → 업그레이드. 설정 패널은 타이틀에서 연다.
    internal sealed partial class ScreenFlow : IDisposable
    {
        private readonly UIManager _ui;
        private readonly UIPresentationSpec _titlePresentation;
        private readonly UIPresentationSpec _modeSelectPresentation;
        private readonly UIPresentationSpec _settingsPresentation;
        private readonly UIPresentationSpec _upgradePresentation;
        private readonly UIPresentationSpec _battlePresentation;
        private readonly UIPresentationSpec _settlementPresentation;
        private readonly UIPresentationSpec _nodeTreePresentation;
        private readonly BattleSystem _battle;
        private readonly PlayerState _player;
        // 노드 트리. 업그레이드 화면의 노드 상태·구매와, 전투를 시작할 때 방장의 산 노드를 업그레이드 표로 바꾸는 데 쓴다.
        private readonly NodeTree _tree;
        // 업그레이드 화면에 그릴 노드(칸·가격). 조립 때 저작 데이터의 격자 칸으로 만들어 받는다.
        private readonly IReadOnlyList<NodeTreeView.NodeItem> _nodes;
        // 블랙홀 성장(성장도별 Level 표·이정표). 업그레이드 화면의 목표 Level과 결산 화면의 이정표 진행도를 계산할 때 쓴다.
        private readonly HqGrowthDefinition _growth;
        // 플레이어 설정. 설정 패널이 보여 주고 고친다.
        private readonly GameSettings _settings;
        private readonly Dictionary<UIBase, List<Action>> _cleanupByScreen = new Dictionary<UIBase, List<Action>>();

        public ScreenFlow(UIManager ui, UIPresentationSpec titlePresentation, UIPresentationSpec modeSelectPresentation,
            UIPresentationSpec settingsPresentation, UIPresentationSpec upgradePresentation,
            UIPresentationSpec battlePresentation, UIPresentationSpec settlementPresentation, UIPresentationSpec nodeTreePresentation,
            BattleSystem battle, PlayerState player, NodeTree tree, IReadOnlyList<NodeTreeView.NodeItem> nodes, HqGrowthDefinition growth,
            GameSettings settings)
        {
            _ui = ui;
            _titlePresentation = titlePresentation;
            _modeSelectPresentation = modeSelectPresentation;
            _settingsPresentation = settingsPresentation;
            _upgradePresentation = upgradePresentation;
            _battlePresentation = battlePresentation;
            _settlementPresentation = settlementPresentation;
            _nodeTreePresentation = nodeTreePresentation;
            _battle = battle;
            _player = player;
            _tree = tree;
            _nodes = nodes;
            _growth = growth;
            _settings = settings;
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
