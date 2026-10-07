using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
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

        [Header("Presentations")]
        [SerializeField] private ScreenPresentations _presentations;

        [Header("UI Context")]
        [SerializeField] private string _themeId = "Light";
        [SerializeField] private string _localeId = "ko-KR";

        [Header("Runtime")]
        [SerializeField] private UIDisplayRefreshDriver _displayRefreshDriver;

        [Header("Input")]
        [SerializeField] private KeyInput _keyInput;

        private static readonly Type[] RequiredViews =
        {
            typeof(TitleScreen),
            typeof(UpgradeScreen),
            typeof(BattleScreen),
            typeof(SettlementScreen),
            typeof(NodeTreeView),
            typeof(ModeSelectPanel),
            typeof(SettingsPanel),
            typeof(PausePanel),
            typeof(ConfirmPanel),
        };

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

            Camera battleCamera = Camera.main;
            if (battleCamera != null && !battleCamera.TryGetComponent(out _cameraFit))
                _cameraFit = battleCamera.gameObject.AddComponent<BattleCameraFit>();
        }

        private void BootstrapBattle()
        {
            _progress = new ProgressState();
            _battle = new BattleSystem(
                _loaded.Content,
                _progress,
                _enemyView,
                _breakerView,
                _deathEffectView,
                _hqView,
                _cameraFit);

            _aim = new AimInput(_battle);
        }

        private void BootstrapProgress()
        {
            _progressStore =
                ProgressStore.Load(
                    Application.persistentDataPath,
                    _loaded.NodeTree.Content,
                    _loaded.Content.Growth);
        }

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

            if (_displayRefreshDriver == null)
                _displayRefreshDriver = gameObject.AddComponent<UIDisplayRefreshDriver>();

            _displayRefreshDriver.Initialize(_ui);

            Canvas canvas = _rootLayer.GetComponentInParent<Canvas>();
            _transition = ScreenTransition.Create(canvas != null
                ? canvas.rootCanvas.transform
                : _rootLayer.parent, _screenTransitionLook);
        }

        private void BootstrapScreenFlow()
        {
            _screens = new ScreenFlow(
                _ui,
                _presentations,
                _battle,
                _progress,
                _progressStore,
                _loaded.NodeTree,
                _loaded.NodeItems,
                _loaded.Content.Growth,
                _settings,
                _transition);
        }

        private void BootstrapHost()
        {
            _host = new GameHost(
                _ui,
                _battle,
                _aim,
                _screens,
                _enemyLooks,
                _enemyView,
                _breakerView,
                _deathEffectView,
                _hqView,
                _cameraShake);
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
            _host.Start();
            SoundManager.Instance.Bind(_settings);
        }

        private void Update() => _host.Tick(Time.deltaTime);

        private void OnApplicationPause(bool paused)
        {
            if (paused && enabled)
                _progressStore.Save(_progress);
        }

        private void OnDestroy()
        {
            _host?.Dispose();

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

            if (_breakerLook == null
                || _breakerLook.Material == null
                || _breakerLook.OrbMaterial == null
                || _breakerLook.CometAuraMaterial == null)
            {
                Debug.LogError(
                    "[외형] GameBootstrap에 Breaker 외형(BreakerLook)을," +
                    " Breaker 외형에 링·버프 구체·혜성 배경 원 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            if (_explosionLook == null
                || _explosionLook.Material == null)
            {
                Debug.LogError("[외형] GameBootstrap에 폭발 외형(ExplosionLook)을," +
                               " 폭발 외형에 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            if (_cometLook == null || _cometLook.Material == null)
            {
                Debug.LogError("[외형] GameBootstrap에 혜성 외형(CometLook)을, " +
                               "혜성 외형에 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            if (_lightningLook == null || _lightningLook.Material == null)
            {
                Debug.LogError("[외형] GameBootstrap에 번개 외형(LightningLook)을," +
                               " 번개 외형에 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            return configured;
        }

        private bool HasConfiguredUI()
        {
            bool configured = true;

            if (_screenTransitionLook == null
                || _screenTransitionLook.Material == null)
            {
                Debug.LogError("[UI] GameBootstrap에 화면 전환 외형(ScreenTransitionLook)을," +
                               " 화면 전환 외형에 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            if (_rootLayer == null
                || _panelLayer == null)
            {
                Debug.LogError("[UI] GameBootstrap에 Root Layer와 Panel Layer를 연결해야 한다.", this);
                configured = false;
            }

            var missingViews = new List<string>();

            foreach (Type type in RequiredViews)
            {
                if (_views == null
                    || !Array.Exists(_views, view => view != null && view.GetType() == type))
                    missingViews.Add(type.Name);
            }

            if (missingViews.Count > 0)
            {
                Debug.LogError(
                    $"[UI] GameBootstrap의 Registered Views에 빠진 화면이 있다: {string.Join(", ", missingViews)}.", this);
                configured = false;
            }

            if (_presentations == null)
            {
                Debug.LogError(
                    "[UI] GameBootstrap에 화면 Presentation 묶음(ScreenPresentations)을 연결해야 한다.", this);
                configured = false;
            }
            else
            {
                IReadOnlyList<string> missing = _presentations.MissingReferences();

                if (missing.Count > 0)
                {
                    Debug.LogError(
                        $"[UI] 화면 Presentation 묶음에 연결하지 않은 칸이 있다: {string.Join(", ", missing)}.", _presentations);
                    configured = false;
                }
            }

            return configured;
        }

        private bool TryBootstrapContent()
        {
            _loaded = GameContentLoader.Load(_content);
            return _loaded != null;
        }
    }
}
