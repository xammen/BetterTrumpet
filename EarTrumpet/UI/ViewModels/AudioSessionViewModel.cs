using EarTrumpet.DataModel;
using EarTrumpet.DataModel.Audio;
using EarTrumpet.Extensions;
using EarTrumpet.UI.Helpers;
using System.Windows.Input;

namespace EarTrumpet.UI.ViewModels
{
    public class AudioSessionViewModel : BindableBase
    {
        private readonly IStreamWithVolumeControl _stream;
        private bool _isAbsMuted;

        public AudioSessionViewModel(IStreamWithVolumeControl stream)
        {
            _stream = stream;
            _stream.PropertyChanged += Stream_PropertyChanged;

            _isAbsMuted = false;

            ToggleMute = new RelayCommand(() => IsMuted = !IsMuted);
        }

        ~AudioSessionViewModel()
        {
            _stream.PropertyChanged -= Stream_PropertyChanged;
        }

        private void Stream_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            RaisePropertyChanged(e.PropertyName);
        }

        public string Id => _stream.Id;
        public virtual string DisplayName => Id;
        protected virtual bool IsDevice => false;
        public ICommand ToggleMute { get; } 
        public bool IsMuted
        {
            get => _stream.IsMuted;
            set
            {
                if (_stream.IsMuted != value)
                {
                    App.UndoService.RecordMuteChange(Id, DisplayName, IsDevice, _stream.IsMuted, value);
                    _stream.IsMuted = value;
                }
            }
        }

        public bool IsAbsMuted
        {
            get => _isAbsMuted;
            set => _isAbsMuted = value;
        }

        public int Volume
        {
            get => _stream.Volume.ToVolumeInt();
            set
            {
                var oldVol = _stream.Volume.ToVolumeInt();
                if (oldVol != value)
                {
                    App.UndoService.RecordVolumeChange(Id, DisplayName, IsDevice, oldVol, value);
                    _stream.Volume = value / 100f;
                }
            }
        }
        public virtual float PeakValue1 => _stream.PeakValue1;
        public virtual float PeakValue2 => _stream.PeakValue2;

        public virtual void UpdatePeakValueForeground()
        {
            RaisePropertyChanged(nameof(PeakValue1));
            RaisePropertyChanged(nameof(PeakValue2));
        }
    }
}
