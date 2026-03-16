namespace EarTrumpet.UI.ViewModels
{
    public class SettingsPageHeaderViewModel : BindableBase
    {
        public string Title => _settingsPageViewModel.Title;
        public string Subtitle => _settingsPageViewModel.Subtitle;

        private SettingsPageViewModel _settingsPageViewModel;

        public SettingsPageHeaderViewModel(SettingsPageViewModel settingsPageViewModel)
        {
            _settingsPageViewModel = settingsPageViewModel;
        }
    }
}
