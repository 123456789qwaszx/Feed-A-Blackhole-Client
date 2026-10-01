using System.Collections.Generic;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        private void OpenSettings()
        {
            _ui.PushPanel<SettingsPanel>(
                _settingsPresentation,
                afterPresented: panel =>
                {
                    BindView(panel, ApplyBindings);
                    panel.Build(SettingItems());
                },
                afterClosed: Unbind);

            _settingsOpen = true;
        }

        private void ApplyBindings(SettingsPanel panel)
        {
            AddBinding(panel,
                p => p.Toggled += HandleSettingToggled,
                p => p.Toggled -= HandleSettingToggled);

            AddBinding(panel,
                p => p.SliderChanged += HandleSettingSliderChanged,
                p => p.SliderChanged -= HandleSettingSliderChanged);

            AddBinding(panel,
                p => p.OptionChanged += HandleSettingOptionChanged,
                p => p.OptionChanged -= HandleSettingOptionChanged);

            AddBinding(panel,
                p => p.BackClicked += HandleSettingsBackClicked,
                p => p.BackClicked -= HandleSettingsBackClicked);
        }

        // 바뀐 값은 곧바로 설정에 넣는다(저장·반영은 GameSettings가 한다).
        private void HandleSettingToggled(string id, bool on) => _settings.SetOn(id, on);
        private void HandleSettingSliderChanged(string id, float level) => _settings.SetLevel(id, level);
        private void HandleSettingOptionChanged(string id, int index) => _settings.SetIndex(id, index);

        // 닫을 때 디스크에 쓴다.
        private void HandleSettingsBackClicked()
        {
            _settings.Save();
            _ui.PopPanel(Unbind);
            _settingsOpen = false;
        }

        // 설정 정의와 지금 값을 설정 창의 행으로 바꾼다.
        private List<SettingsPanel.SettingItem> SettingItems()
        {
            var items = new List<SettingsPanel.SettingItem>(GameSettings.Definitions.Count);

            foreach (GameSettings.Definition definition in GameSettings.Definitions)
            {
                items.Add(new SettingsPanel.SettingItem(
                    definition.Id, definition.Label, definition.Kind, _settings.ValueOf(definition.Id), definition.Options));
            }

            return items;
        }
    }
}
