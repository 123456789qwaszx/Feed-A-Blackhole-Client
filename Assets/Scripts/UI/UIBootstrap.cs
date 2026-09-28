using System.Collections.Generic;
using UnityEngine;

namespace BlackHole.Unity
{
    // UIScene의 진입점. 씬의 UI Canvas에 놓인 화면 프리팹으로 UIManager와 화면 흐름을 조립하고 타이틀 화면을 연다.
    // 화면(TitleScreen·UpgradeScreen·BattleScreen·SettlementScreen)을 Root Layer 아래에 두고 Views에 연결해야 한다.
    // 누락된 연결은 조립 전에 오류로 알린다. Presentation을 비워 두면 아무것도 바꾸지 않는 빈 Presentation을 쓴다.
    public sealed class UIBootstrap : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private RectTransform _rootLayer;
        [SerializeField] private RectTransform _panelLayer;
        [SerializeField] private UIBase[] _views;

        [Header("Presentations (비우면 빈 Presentation)")]
        [SerializeField] private UIPresentationSpec _titlePresentation;
        [SerializeField] private UIPresentationSpec _upgradePresentation;
        [SerializeField] private UIPresentationSpec _battlePresentation;
        [SerializeField] private UIPresentationSpec _settlementPresentation;

        [Header("Context")]
        [SerializeField] private string _themeId = "Light";
        [SerializeField] private string _localeId = "ko-KR";

        [SerializeField] private UIDisplayRefreshDriver _displayRefreshDriver;

        private readonly List<UIPresentationSpec> _emptyPresentations = new List<UIPresentationSpec>();
        private UIManager _ui;
        private ScreenFlow _screens;

        private void Awake()
        {
            if (!HasConfiguredUI())
            {
                enabled = false;
                return;
            }

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

            _screens = new ScreenFlow(
                _ui,
                OrEmpty(_titlePresentation, "Title"),
                OrEmpty(_upgradePresentation, "Upgrade"),
                OrEmpty(_battlePresentation, "Battle"),
                OrEmpty(_settlementPresentation, "Settlement"));
        }

        private void Start() => _screens?.GoToTitle();

        private void OnDestroy()
        {
            _screens?.Dispose();

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
                "[UI] UIBootstrap에 Root Layer, Panel Layer와 TitleScreen·UpgradeScreen·BattleScreen·SettlementScreen을 " +
                "Views로 연결해야 한다.",
                this);
            return false;
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
