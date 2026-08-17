using EarTrumpet.UI.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace EarTrumpet.UI.ViewModels
{
    public interface IAppItemViewModel : IAppIconSource, INotifyPropertyChanged
    {
        string Id { get; }
        bool IsMuted { get; set; }
        int Volume { get; set; }
        Color Background { get; }
        ObservableCollection<IAppItemViewModel> ChildApps { get; }
        string DisplayName { get; }
        string ExeName { get; }
        string AppId { get; }
        char IconText { get; }
        bool AnimateOnLoad { get; }
        bool IsHiding { get; set; }
        bool IsExpanded { get; }
        bool IsMovable { get; }
        /// <summary>
        /// True while a Lock volume rule holds this app's volume. The slider binds its
        /// enabled state to this: fighting a locked slider mid-drag looks like a bug.
        /// </summary>
        bool IsVolumeLocked { get; }
        float PeakValue1 { get; }
        float PeakValue2 { get; }
        string PersistedOutputDevice { get; }
        int ProcessId { get; }
        bool DoesGroupWith(IAppItemViewModel app);
        bool MoveToDevice(string id, bool hide);
        void UpdatePeakValueForeground();
        void UpdatePeakValueBackground();
        IDeviceViewModel Parent { get; }
    }
}
