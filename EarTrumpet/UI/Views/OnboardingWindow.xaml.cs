using EarTrumpet.UI.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace EarTrumpet.UI.Views
{
    public partial class OnboardingWindow : Window
    {
        public OnboardingWindow()
        {
            InitializeComponent();
            VersionText.Text = $"v{App.PackageVersion}";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void Next_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm)
                vm.NextCommand.Execute(null);
            e.Handled = true;
        }

        private void Back_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm)
                vm.BackCommand.Execute(null);
            e.Handled = true;
        }

        private void Skip_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm)
                vm.SkipCommand.Execute(null);
            e.Handled = true;
        }

        private void DeviceCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm && sender is FrameworkElement fe && fe.Tag is AudioDeviceChoice choice)
            {
                // Deselect all, select clicked
                foreach (var dev in vm.AudioDevices)
                    dev.IsDefault = false;
                choice.IsDefault = true;
                vm.SelectedDevice = choice;

                // Force UI refresh (ItemsControl doesn't auto-refresh IsDefault changes)
                var items = vm.AudioDevices;
                var temp = new System.Collections.Generic.List<AudioDeviceChoice>(items);
                items.Clear();
                foreach (var d in temp) items.Add(d);
                vm.SelectedDevice = choice;
            }
            e.Handled = true;
        }

        private void Theme0_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm) vm.SelectedThemeIndex = 0;
            e.Handled = true;
        }

        private void Theme1_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm) vm.SelectedThemeIndex = 1;
            e.Handled = true;
        }
    }
}
