using System.Collections.Generic;
using BlackHole.Core;
using BlackHole.Sample;
using UnityEngine;

namespace BlackHole.Unity
{
    // 씬의 직렬화 설정으로 게임을 조립하는 Unity 진입점.
    // - Awake: 콘텐츠 로드·검증, 적 화면·스킬 화면·사망 효과 화면·블랙홀 화면, 전투 시스템, 오케스트레이터, 조준 입력,
    //   UI(UIManager와 타이틀·업그레이드·전투·결산 화면), 화면 흐름, GameHost 조립.
    // - Start/Update: 조립한 GameHost에 Unity 수명을 전달한다.
    //
    // 콘텐츠: 판 설정은 SampleContent(C#), 스킬은 스킬 설정 에셋, 적 종류는 적 종류 목록 에셋,
    // 출현 배치와 전투 시작 공급은 적 공급 설정 에셋, 블랙홀 성장의 Level 표는 블랙홀 성장 설정 에셋이 채운다.
    // 화면은 씬의 UI Canvas에 놓인 화면 프리팹(TitleScreen·UpgradeScreen·BattleScreen·SettlementScreen)을 Root Layer와 Views로 받는다.
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

        [Header("UI Layers")]
        [SerializeField] private RectTransform _rootLayer;
        [SerializeField] private RectTransform _panelLayer;

        [Header("Registered Views")]
        [SerializeField] private UIBase[] _views;

        [Header("Presentations (비우면 빈 Presentation)")]
        [SerializeField] private UIPresentationSpec _titlePresentation;
        [SerializeField] private UIPresentationSpec _upgradePresentation;
        [SerializeField] private UIPresentationSpec _battlePresentation;
        [SerializeField] private UIPresentationSpec _settlementPresentation;

        [Header("UI Context")]
        [SerializeField] private string _themeId = "Light";
        [SerializeField] private string _localeId = "ko-KR";

        [Header("Runtime")]
        [SerializeField] private UIDisplayRefreshDriver _displayRefreshDriver;

        private readonly List<UIPresentationSpec> _emptyPresentations = new List<UIPresentationSpec>();
        private GameContent _content;
        private EnemyLooks _enemyLooks;
        private EnemyView _enemyView;
        private SkillView _skillView;
        private DeathEffectView _deathEffectView;
        private HqView _hqView;
        private BattleSystem _battle;
        private BattleOrchestrator _orchestrator;
        private PlayerState _viewer;
        private AimInput _aim;
        private UIManager _ui;
        private ScreenFlow _screens;
        private GameHost _host;

        private void Awake()
        {
            if (!TryLoadContent(out _content) || !HasConfiguredUI())
            {
                enabled = false;
                return;
            }

            BootstrapBattleViews();
            BootstrapBattle();
            BootstrapUI();
            BootstrapScreenFlow();
            BootstrapHost();
        }

        private void BootstrapBattleViews()
        {
            _enemyLooks = new EnemyLooks(_enemyCatalog.Kinds());
            _enemyView = new EnemyView(transform, _enemyLooks);
            _skillView = new SkillView(transform);
            _deathEffectView = new DeathEffectView(transform);
            _hqView = new HqView(transform);
        }

        private void BootstrapBattle()
        {
            _battle = new BattleSystem(_content, _enemyView, _skillView, _deathEffectView, _hqView);
            _orchestrator = new BattleOrchestrator(_battle, Host);
            // 화면이 보는 진행 상태: 방장의 것.
            _viewer = _orchestrator.Progress;
            // 마우스가 조준하는 참가자: 방장.
            _aim = new AimInput(_battle, _viewer.Id);
        }

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

            if (_displayRefreshDriver != null)
                _displayRefreshDriver.Initialize(_ui);
        }

        private void BootstrapScreenFlow()
        {
            _screens = new ScreenFlow(
                _ui,
                OrEmpty(_titlePresentation, "Title"),
                OrEmpty(_upgradePresentation, "Upgrade"),
                OrEmpty(_battlePresentation, "Battle"),
                OrEmpty(_settlementPresentation, "Settlement"),
                _battle, _orchestrator, _viewer, _content.Growth);
        }

        private void BootstrapHost()
        {
            _host = new GameHost(_ui, _battle, _aim, _screens,
                _enemyLooks, _enemyView, _skillView, _deathEffectView, _hqView);
        }

        private void Start() => _host?.Start();

        private void Update() => _host?.Tick(Time.deltaTime);

        private void OnDestroy()
        {
            _host?.Dispose();

            foreach (UIPresentationSpec presentation in _emptyPresentations)
                Destroy(presentation);
        }

        private bool HasConfiguredUI()
        {
            if (_rootLayer != null && _panelLayer != null && _views != null)
            {
                bool hasTitle = false;
                bool hasUpgrade = false;
                bool hasBattle = false;
                bool hasSettlement = false;

                foreach (UIBase view in _views)
                {
                    hasTitle |= view is TitleScreen;
                    hasUpgrade |= view is UpgradeScreen;
                    hasBattle |= view is BattleScreen;
                    hasSettlement |= view is SettlementScreen;
                }

                if (hasTitle && hasUpgrade && hasBattle && hasSettlement)
                    return true;
            }

            Debug.LogError(
                "[UI] GameBootstrap에 Root Layer, Panel Layer와 TitleScreen·UpgradeScreen·BattleScreen·SettlementScreen을 " +
                "Registered Views로 연결해야 한다.",
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

            ContentData data = SampleContent.Create();
            _skillSetup.WriteTo(data);
            _enemyCatalog.WriteTo(data.Enemies);
            _enemySupply.WriteTo(data.Enemies);
            data.Growth = _hqGrowth.ToData();
            ContentLoadResult result = ContentLoader.Load(data);

            foreach (ContentDiagnostic diagnostic in result.Diagnostics)
                Debug.LogError("[콘텐츠] " + diagnostic, this);

            content = result.Content;
            return result.Succeeded;
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
