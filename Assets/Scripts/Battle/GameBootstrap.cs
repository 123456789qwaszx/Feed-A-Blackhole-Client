using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 씬의 직렬화 설정으로 게임을 조립하는 Unity 진입점.
    // - Awake: 콘텐츠 로드(GameContentLoader), 적 화면·Breaker 화면·사망 효과 화면·블랙홀 화면, 진행 상태와 진행 저장, 전투 시스템, 조준 입력,
    //   UI(UIManager와 타이틀·업그레이드·전투·결산 화면), 화면 흐름, GameHost 조립.
    // - Start/Update: 조립한 GameHost에 Unity 수명을 전달한다.
    //
    // 화면은 씬의 UI Canvas에 놓인 화면 프리팹(TitleScreen·UpgradeScreen·BattleScreen·SettlementScreen)을 Root Layer와 Views로,
    // 패널 프리팹(ModeSelectPanel·SettingsPanel·PausePanel·ConfirmPanel)을 Panel Layer와 Views로 받는다.
    // 누락된 연결은 조립 전에 오류로 알린다. Presentation을 비워 두면 아무것도 바꾸지 않는 빈 Presentation을 쓴다.
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private GameContentSetup _content;

        [Header("Looks")]
        [SerializeField] private BreakerLook _breakerLook;
        [SerializeField] private ExplosionLook _explosionLook;
        [SerializeField] private LightningLook _lightningLook;
        [SerializeField] private CometLook _cometLook;
        [SerializeField] private GameObject _blackHole;

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
        [SerializeField] private UIPresentationSpec _confirmPresentation;

        [Header("UI Context")]
        [SerializeField] private string _themeId = "Light";
        [SerializeField] private string _localeId = "ko-KR";

        [Header("Runtime")]
        [SerializeField] private UIDisplayRefreshDriver _displayRefreshDriver;

        [Header("Input")]
        [SerializeField] private KeyInput _keyInput;

        private readonly List<UIPresentationSpec> _emptyPresentations = new List<UIPresentationSpec>();
        private LoadedContent _loaded;
        private EnemyLooks _enemyLooks;
        private EnemyView _enemyView;
        private BreakerView _breakerView;
        private DeathEffectView _deathEffectView;
        private HqView _hqView;
        private BattleSystem _battle;
        private ProgressState _progress;
        private ProgressStore _progressStore;
        private AimInput _aim;
        private GameSettings _settings;
        private UIManager _ui;
        private ScreenTransition _transition;
        private ScreenFlow _screens;
        private GameHost _host;
        private CameraShake _cameraShake;
        private BattleCameraFit _cameraFit;

        private void Awake()
        {
            if (!TryBootstrapContent()
                || !HasConfiguredLooks()
                || !HasConfiguredUI())
            {
                enabled = false;
                return;
            }

            _cameraShake = Camera.main.GetComponent<CameraShake>();

            BootstrapBattleViews();
            BootstrapBattle();
            BootstrapProgress();
            BootstrapSettings();
            BootstrapUI();
            BootstrapScreenFlow();
            BootstrapHost();
            BootstrapKeyInput();
        }

        private void BootstrapBattleViews()
        {
            _enemyLooks = new EnemyLooks(_content.Enemies.Kinds());
            _enemyView = new EnemyView(transform, _enemyLooks, _breakerLook, _cometLook);
            _breakerView = new BreakerView(transform, _breakerLook);
            _deathEffectView = new DeathEffectView(transform, _lightningLook, _explosionLook);
            _hqView = new HqView(transform, _blackHole, Camera.main);

            // 전투 카메라를 화면비와 판의 전장 배율에 맞춘다(좁은 화면에서도 16:9의 가로 폭을 보여 준다). 씬에 없으면 여기서 붙인다.
            Camera battleCamera = Camera.main;
            if (battleCamera != null && !battleCamera.TryGetComponent(out _cameraFit))
                _cameraFit = battleCamera.gameObject.AddComponent<BattleCameraFit>();
        }

        private void BootstrapBattle()
        {
            // 화면이 보는 진행 상태. 전투 사이에 이어지고, 진행 저장으로 앱을 다시 켜도 이어진다.
            _progress = new ProgressState();
            _battle = new BattleSystem(_loaded.Content, _progress, _enemyView, _breakerView, _deathEffectView, _hqView, _cameraFit);
            // 포인터 위치를 Breaker의 조준점으로 넣는다.
            _aim = new AimInput(_battle);
        }

        // 진행 저장을 한 번 불러 검사한다. 진행 상태에 넣는 것은 모드 선택에서 계속을 고를 때다.
        private void BootstrapProgress() =>
            _progressStore = ProgressStore.Load(Application.persistentDataPath, _loaded.NodeTree.Content, _loaded.Content.Growth);

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
                OrEmpty(_confirmPresentation, "Confirm"),
                _battle, _progress, _progressStore, _loaded.NodeTree, _loaded.NodeItems, _loaded.Content.Growth,
                _settings, _transition);
        }

        private void BootstrapHost()
        {
            _host = new GameHost(_ui, _battle, _aim, _screens,
                _enemyLooks, _enemyView, _breakerView, _deathEffectView, _hqView, _cameraShake);
        }

        private void BootstrapKeyInput()
        {
            if (_keyInput == null) return;

            _keyInput.ContinuePressed += _screens.HandleKeyActionSpace;
            _keyInput.UpgradePressed += _screens.HandleKeyActionShift;
            _keyInput.PausePressed += _screens.HandleKeyActionEsc;
        }

        private void Start()
        {
            _host?.Start();
            SoundManager.Instance.Bind(_settings);
        }

        private void Update() => _host?.Tick(Time.deltaTime);

        // 모바일은 앱 종료 이벤트가 불리지 않는 경우가 많아, 앱이 내려갈 때 저장해 둔다. 조립에 실패했으면 저장할 것이 없다.
        private void OnApplicationPause(bool paused)
        {
            if (paused && enabled)
                _progressStore.Save(_progress);
        }

        private void OnDestroy()
        {
            _host?.Dispose();

            foreach (UIPresentationSpec presentation in _emptyPresentations)
                Destroy(presentation);

            if(_keyInput != null && _screens != null)
            {
                _keyInput.ContinuePressed -= _screens.HandleKeyActionSpace;
                _keyInput.UpgradePressed -= _screens.HandleKeyActionShift;
                _keyInput.PausePressed -= _screens.HandleKeyActionEsc;
            }
        }

        private bool HasConfiguredLooks()
        {
            bool configured = true;

            if (_breakerLook == null || _breakerLook.Material == null || _breakerLook.OrbMaterial == null
                || _breakerLook.CometAuraMaterial == null)
            {
                Debug.LogError("[외형] GameBootstrap에 Breaker 외형(BreakerLook)을, Breaker 외형에 링·버프 구체·혜성 배경 원 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            if (_explosionLook == null || _explosionLook.Material == null)
            {
                Debug.LogError("[외형] GameBootstrap에 폭발 외형(ExplosionLook)을, 폭발 외형에 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            if (_cometLook == null || _cometLook.Material == null)
            {
                Debug.LogError("[외형] GameBootstrap에 혜성 외형(CometLook)을, 혜성 외형에 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            if (_lightningLook == null || _lightningLook.Material == null)
            {
                Debug.LogError("[외형] GameBootstrap에 번개 외형(LightningLook)을, 번개 외형에 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            return configured;
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
                bool hasConfirm = false;

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
                    hasConfirm |= view is ConfirmPanel;
                }

                if (hasTitle && hasModeSelect && hasSettings && hasPause && hasUpgrade && hasBattle && hasSettlement && hasNodeTree && hasConfirm)
                    return true;
            }

            Debug.LogError(
                "[UI] GameBootstrap에 Root Layer, Panel Layer와 TitleScreen·UpgradeScreen·BattleScreen·SettlementScreen, " +
                "업그레이드 화면 안의 트리 보기 페이지(NodeTreeView), Panel Layer 아래의 모드 선택 패널(ModeSelectPanel)·설정 패널(SettingsPanel)·일시 정지 패널(PausePanel)·확인 창(ConfirmPanel)을 Registered Views로 연결해야 한다.",
                this);
            return false;
        }

        // 오류가 있는 콘텐츠로는 시작하지 않는다. 빠진 연결과 진단은 GameContentLoader가 남긴다.
        private bool TryBootstrapContent()
        {
            _loaded = GameContentLoader.Load(_content);
            return _loaded != null;
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
