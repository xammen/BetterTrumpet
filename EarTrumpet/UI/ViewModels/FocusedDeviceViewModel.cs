using EarTrumpet.Extensibility.Hosting;
using EarTrumpet.UI.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EarTrumpet.UI.ViewModels
{
    class FocusedDeviceViewModel : IFocusedViewModel
    {
        public event Action RequestClose;

        public string DisplayName { get; }
        public ObservableCollection<ToolbarItemViewModel> Toolbar { get; }

        public ObservableCollection<object> Addons { get; }

        public bool IsApplicable => (Addons != null && Addons.Count > 0) || _hasCommands;

        private readonly bool _hasCommands;

        public FocusedDeviceViewModel(DeviceCollectionViewModel mainViewModel, DeviceViewModel device)
        {
            DisplayName = device.DisplayName;
            Toolbar = new ObservableCollection<ToolbarItemViewModel>();
            Toolbar.Add(new ToolbarItemViewModel
            {
                GlyphFontSize = 10,
                DisplayName = Properties.Resources.CloseButtonAccessibleText,
                Glyph = "\uE8BB",
                Command = new RelayCommand(() => RequestClose.Invoke())
            });

            var menuItems = new List<ContextMenuItem>();

            if (device.HasHiddenApps)
            {
                var hiddenEntries = mainViewModel.GetHiddenAppsForDevice(device.Id);
                var restoreChildren = hiddenEntries.Select(entry => new ContextMenuItem
                {
                    DisplayName = mainViewModel.GetHiddenAppLabel(entry),
                    Command = new RelayCommand(() =>
                    {
                        mainViewModel.UnhideAppOnDevice(device.Id, entry.AppId, entry.ExeName);
                        RequestClose.Invoke();
                    }),
                }).ToList();

                restoreChildren.Add(new ContextMenuSeparator());
                restoreChildren.Add(new ContextMenuItem
                {
                    DisplayName = Properties.Resources.RestoreHiddenAppsForDeviceAll,
                    Command = new RelayCommand(() =>
                    {
                        mainViewModel.UnhideAllAppsForDevice(device.Id);
                        RequestClose.Invoke();
                    }),
                });

                menuItems.Add(new ContextMenuItem
                {
                    DisplayName = Properties.Resources.RestoreHiddenAppsForDevice,
                    Children = restoreChildren,
                });
            }

            var contentItems = AddonManager.Host.DeviceContentItems;
            if (contentItems != null)
            {
                Addons = new ObservableCollection<object>(contentItems.Select(a => a.GetContentForDevice(device.Id, () => RequestClose.Invoke())).Where(a => a != null).ToArray());

                var addonMenuItems = contentItems.SelectMany(a => a.GetContextMenuItemsForDevice(device.Id)).Where(m => m != null).ToList();
                if (addonMenuItems.Any())
                {
                    menuItems.AddRange(addonMenuItems);
                }
            }

            if (menuItems.Any())
            {
                Toolbar.Insert(0, new ToolbarItemViewModel
                {
                    GlyphFontSize = 16,
                    DisplayName = Properties.Resources.MoreCommandsAccessibleText,
                    Glyph = "\uE10C",
                    Menu = new ObservableCollection<ContextMenuItem>(menuItems)
                });
            }

            _hasCommands = menuItems.Count > 0;
        }

        public void Closing() { }
    }
}
