using System;
using System.Collections.Generic;
using BlackHole.Core;
using BlackHole.Sample;
using UnityEngine;

namespace BlackHole.Unity
{
    // 씬의 직렬화 설정으로 게임을 조립하는 Unity 진입점.
    // - Awake: 콘텐츠·노드 트리 로드·검증, 적 화면·스킬 화면·사망 효과 화면·블랙홀 화면, 진행 상태, 전투 시스템, 조준 입력,
    //   UI(UIManager와 타이틀·업그레이드·전투·결산 화면), 화면 흐름, GameHost 조립.
    // - Start/Update: 조립한 GameHost에 Unity 수명을 전달한다.
    //
    // 콘텐츠: 판 설정은 SampleContent(C#), 스킬은 스킬 설정 에셋, 적 종류는 적 종류 목록 에셋,
    // 출현 배치와 전투 시작 공급은 적 공급 설정 에셋, 블랙홀 성장의 Level 표는 블랙홀 성장 설정 에셋이 채운다.
    // 화면은 씬의 UI Canvas에 놓인 화면 프리팹(TitleScreen·UpgradeScreen·BattleScreen·SettlementScreen)을 Root Layer와 Views로,
    // 패널 프리팹(ModeSelectPanel·SettingsPanel·PausePanel)을 Panel Layer와 Views로 받는다.
    // 누락된 연결은 조립 전에 오류로 알린다. Presentation을 비워 두면 아무것도 바꾸지 않는 빈 Presentation을 쓴다.
    public sealed class GameBootstrap : MonoBehaviour
    {
        // 방장(로컬 Player). 진행 상태의 주인이고, 지금 판 안의 참가자도 방장 한 명이다.
        private static readonly PlayerId Host = new PlayerId(1);

        [Header("Content")]
        [SerializeField] private EnemyCatalog _enemyCatalog;
        [SerializeField] private EnemySupplySetup _enemySupply;
        [SerializeField] private HqGrowthSetup _hqGrowth;
        [SerializeField] private SkillSetup _skillSetup;
        [SerializeField] private NodeCatalog _nodeCatalog;

        [Header("Looks")]
        [SerializeField] private BreakerLook _breakerLook;

        [Header("UI Layers")]
        [SerializeField] private RectTransform _rootLayer;
        [SerializeField] private RectTransform _panelLayer;

        [Header("Screen Transition")]
        [SerializeField] private ScreenTransitionLook _screenTransitionLook;

        [Header("Registered Views")]
        [SerializeField] private UIBase[] _views;

        [Header("Presentations (비우면 빈 Presentation)")]
        [SerializeField] private UIPresentationSpec _titlePresentation;
        [SerializeField] private UIPresentationSpec _modeSelectPresentation;
        [SerializeField] private UIPresentationSpec _settingsPresentation;
        [SerializeField] private UIPresentationSpec _pausePresentation;
        [SerializeField] private UIPresentationSpec _upgradePresentation;
        [SerializeField] private UIPresentationSpec _battlePresentation;
        [SerializeField] private UIPresentationSpec _settlementPresentation;
        [SerializeField] private UIPresentationSpec _nodeTreePresentation;

        [Header("UI Context")]
        [SerializeField] private string _themeId = "Light";
        [SerializeField] private string _localeId = "ko-KR";

        [Header("Runtime")]
        [SerializeField] private UIDisplayRefreshDriver _displayRefreshDriver;

        private readonly List<UIPresentationSpec> _emptyPresentations = new List<UIPresentationSpec>();
        private GameContent _content;
        private NodeTreeData _layout;
        private NodeTree _nodeTree;
        private EnemyLooks _enemyLooks;
        private EnemyView _enemyView;
        private BreakerView _breakerView;
        private SkillView _skillView;
        private DeathEffectView _deathEffectView;
        private HqView _hqView;
        private BattleSystem _battle;
        private PlayerState _viewer;
        private AimInput _aim;
        private GameSettings _settings;
        private UIManager _ui;
        private ScreenTransition _transition;
        private ScreenFlow _screens;
        private GameHost _host;
        private CameraShake _cameraShake;

        private void Awake()
        {
            if (!TryLoadContent(out _content)
                || !TryLoadNodeTree(out _layout, out _nodeTree)
                || !NodesFitContent(_content, _nodeTree)
                || !HasConfiguredLooks()
                || !HasConfiguredUI())
            {
                enabled = false;
                return;
            }

            _cameraShake = Camera.main.GetComponent<CameraShake>();

            BootstrapBattleViews();
            BootstrapBattle();
            BootstrapSettings();
            BootstrapUI();
            BootstrapScreenFlow();
            BootstrapHost();

        }

        private void BootstrapBattleViews()
        {
            _enemyLooks = new EnemyLooks(_enemyCatalog.Kinds());
            _enemyView = new EnemyView(transform, _enemyLooks);
            _breakerView = new BreakerView(transform, _breakerLook);
            _skillView = new SkillView(transform);
            _deathEffectView = new DeathEffectView(transform);
            _hqView = new HqView(transform);

            // 전투 카메라를 화면비에 맞춘다(좁은 화면에서도 16:9의 가로 폭을 보여 준다). 씬에 없으면 여기서 붙인다.
            Camera battleCamera = Camera.main;
            if (battleCamera != null && !battleCamera.TryGetComponent(out BattleCameraFit _))
                battleCamera.gameObject.AddComponent<BattleCameraFit>();
        }

        private void BootstrapBattle()
        {
            // 화면이 보는 진행 상태: 방장의 것. 전투 사이에 이어진다(저장은 없다).
            _viewer = new PlayerState(Host);
            _battle = new BattleSystem(_content, _viewer, _enemyView, _breakerView, _skillView, _deathEffectView, _hqView);
            // 마우스가 조준하는 참가자: 방장.
            _aim = new AimInput(_battle, _viewer.Id);
        }

        // 저장된 플레이어 설정을 읽는다(없으면 기본값).
        private void BootstrapSettings() => _settings = GameSettings.Load();

        private void BootstrapUI()
        {
            _ui = new UIManager(
                _rootLayer,
                _panelLayer,
                new UIResolver(new UIContext(_themeId, _localeId)),
                new UIPresentationApplier());

            foreach (UIBase view in _views)
            {
                if (view == null)
                    continue;

                view.gameObject.SetActive(false);
                _ui.Register(view);
            }

            // 해상도·Safe Area가 바뀌면(회전, 창 크기) 보이는 화면에 다시 맞춘다. 씬에 없으면 여기서 붙인다.
            if (_displayRefreshDriver == null)
                _displayRefreshDriver = gameObject.AddComponent<UIDisplayRefreshDriver>();

            _displayRefreshDriver.Initialize(_ui);

            // 화면 전환 덮개는 맨 위 캔버스의 마지막 자식이라 모든 화면·패널 위에 그려진다.
            Canvas canvas = _rootLayer.GetComponentInParent<Canvas>();
            _transition = ScreenTransition.Create(canvas != null ? canvas.rootCanvas.transform : _rootLayer.parent, _screenTransitionLook);
        }

        private void BootstrapScreenFlow()
        {
            _screens = new ScreenFlow(
                _ui,
                OrEmpty(_titlePresentation, "Title"),
                OrEmpty(_modeSelectPresentation, "ModeSelect"),
                OrEmpty(_settingsPresentation, "Settings"),
                OrEmpty(_pausePresentation, "Pause"),
                OrEmpty(_upgradePresentation, "Upgrade"),
                OrEmpty(_battlePresentation, "Battle"),
                OrEmpty(_settlementPresentation, "Settlement"),
                OrEmpty(_nodeTreePresentation, "NodeTree"),
                _battle, _viewer, _nodeTree, BuildNodeItems(_nodeTree, _layout), _content.Growth, _settings, _transition);
        }

        private void BootstrapHost()
        {
            _host = new GameHost(_ui, _battle, _aim, _screens,
                _enemyLooks, _enemyView, _breakerView, _skillView, _deathEffectView, _hqView, _cameraShake);
        }

        private void Start()
        {
            _host?.Start();
            SoundManager.Instance.Bind(_settings);
        }

        private void Update() => _host?.Tick(Time.deltaTime);

        private void OnDestroy()
        {
            _host?.Dispose();

            foreach (UIPresentationSpec presentation in _emptyPresentations)
                Destroy(presentation);
        }

        private bool HasConfiguredLooks()
        {
            if (_breakerLook != null && _breakerLook.Material != null && _breakerLook.OrbMaterial != null
                && _breakerLook.CometAuraMaterial != null)
                return true;

            Debug.LogError("[외형] GameBootstrap에 Breaker 외형(BreakerLook)을, Breaker 외형에 링·버프 구체·혜성 배경 원 머티리얼을 연결해야 한다.", this);
            return false;
        }

        private bool HasConfiguredUI()
        {
            if (_screenTransitionLook == null || _screenTransitionLook.Material == null)
            {
                Debug.LogError("[UI] GameBootstrap에 화면 전환 외형(ScreenTransitionLook)을, 화면 전환 외형에 머티리얼을 연결해야 한다.", this);
                return false;
            }

            if (_rootLayer != null && _panelLayer != null && _views != null)
            {
                bool hasTitle = false;
                bool hasModeSelect = false;
                bool hasSettings = false;
                bool hasPause = false;
                bool hasUpgrade = false;
                bool hasBattle = false;
                bool hasSettlement = false;
                bool hasNodeTree = false;

                foreach (UIBase view in _views)
                {
                    hasTitle |= view is TitleScreen;
                    hasModeSelect |= view is ModeSelectPanel;
                    hasSettings |= view is SettingsPanel;
                    hasPause |= view is PausePanel;
                    hasUpgrade |= view is UpgradeScreen;
                    hasBattle |= view is BattleScreen;
                    hasSettlement |= view is SettlementScreen;
                    hasNodeTree |= view is NodeTreeView;
                }

                if (hasTitle && hasModeSelect && hasSettings && hasPause && hasUpgrade && hasBattle && hasSettlement && hasNodeTree)
                    return true;
            }

            Debug.LogError(
                "[UI] GameBootstrap에 Root Layer, Panel Layer와 TitleScreen·UpgradeScreen·BattleScreen·SettlementScreen, " +
                "업그레이드 화면 안의 트리 보기 페이지(NodeTreeView), Panel Layer 아래의 모드 선택 패널(ModeSelectPanel)·설정 패널(SettingsPanel)·일시 정지 패널(PausePanel)을 Registered Views로 연결해야 한다.",
                this);
            return false;
        }

        // 오류가 있는 콘텐츠로는 시작하지 않는다. 모든 진단을 위치와 함께 남긴다.
        private bool TryLoadContent(out GameContent content)
        {
            content = null;

            if (_enemyCatalog == null || _enemySupply == null || _hqGrowth == null || _skillSetup == null)
            {
                Debug.LogError(
                    "[콘텐츠] GameBootstrap에 적 종류 목록(EnemyCatalog), 적 공급 설정(EnemySupplySetup), 블랙홀 성장 설정(HqGrowthSetup), 스킬 설정(SkillSetup)을 연결해야 한다.",
                    this);
                return false;
            }

            ContentData data = ContentDataFrom(_skillSetup, _enemyCatalog, _enemySupply, _hqGrowth);
            ContentLoadResult result = ContentLoader.Load(data);

            foreach (ContentDiagnostic diagnostic in result.Diagnostics)
                Debug.LogError("[콘텐츠] " + diagnostic, this);

            content = result.Content;
            return result.Succeeded;
        }

        // 콘텐츠 에셋으로 Core 저작 형식을 채운다. 데이터 시트 가져오기도 같은 형식으로 게임과 같은 검사를 한다.
        internal static ContentData ContentDataFrom(SkillSetup skills, EnemyCatalog enemies, EnemySupplySetup supply, HqGrowthSetup growth)
        {
            ContentData data = SampleContent.Create();
            skills.WriteTo(data);
            enemies.WriteTo(data.Enemies);
            supply.WriteTo(data.Enemies);
            data.Growth = growth.ToData();
            return data;
        }

        // 오류가 있는 노드 트리로도 시작하지 않는다. 업그레이드 화면은 트리(규칙)와 함께 저작 데이터(격자 칸)도 받는다.
        private bool TryLoadNodeTree(out NodeTreeData layout, out NodeTree tree)
        {
            layout = null;
            tree = null;

            if (_nodeCatalog == null)
            {
                Debug.LogError("[노드 트리] GameBootstrap에 노드 목록(NodeCatalog)을 연결해야 한다.", this);
                return false;
            }

            layout = _nodeCatalog.ToData();
            NodeTreeLoadResult result = NodeTreeLoader.Load(layout);

            foreach (ContentDiagnostic diagnostic in result.Diagnostics)
                Debug.LogError("[노드 트리] " + diagnostic, this);

            tree = result.Tree;
            return result.Succeeded;
        }

        // 노드를 모두 산 경우에도 판을 조립할 수 있어야 한다(질량 단계 범위, 황금이 되는 종류, 전체 개체 수 상한).
        // 두 데이터는 따로 불러오므로 여기서 함께 본다. 오류가 있으면 언젠가 전투 시작이 실패하므로 시작하지 않는다.
        private bool NodesFitContent(GameContent content, NodeTree tree)
        {
            IReadOnlyList<ContentDiagnostic> diagnostics = UpgradeContentCheck.Check(content, tree);

            foreach (ContentDiagnostic diagnostic in diagnostics)
                Debug.LogError("[노드 트리 × 콘텐츠] " + diagnostic, this);

            return diagnostics.Count == 0;
        }

        // 업그레이드 화면에 그릴 노드. 격자 칸은 화면 배치용이라 규칙 트리가 아니라 같은 저작 데이터에서 읽는다.
        // 로더가 같은 데이터로 트리를 만들었으니 트리의 모든 노드에 칸이 있다.
        private static IReadOnlyList<NodeTreeView.NodeItem> BuildNodeItems(NodeTree tree, NodeTreeData layout)
        {
            var cells = new Dictionary<string, (int X, int Y)>(StringComparer.Ordinal);
            foreach (NodeData node in layout.Nodes)
            {
                if (node?.Id != null && !cells.ContainsKey(node.Id))
                    cells.Add(node.Id, (node.X, node.Y));
            }

            var nodes = new List<NodeTreeView.NodeItem>(tree.Nodes.Count);
            foreach (NodeDefinition node in tree.Nodes)
            {
                (int x, int y) = cells.TryGetValue(node.Id, out (int X, int Y) cell) ? cell : (0, 0);
                string stat = node.Upgrades.Count > 0 ? node.Upgrades[0].Stat : null;   // 노드 그림을 고르는 스탯
                nodes.Add(new NodeTreeView.NodeItem(node.Id, x, y, node.Price, stat));
            }

            return nodes;
        }

        private UIPresentationSpec OrEmpty(UIPresentationSpec presentation, string id)
        {
            if (presentation != null)
                return presentation;

            var empty = ScriptableObject.CreateInstance<UIPresentationSpec>();
            empty.name = id;
            empty.presentationId = id;
            _emptyPresentations.Add(empty);
            return empty;
        }
    }
}
