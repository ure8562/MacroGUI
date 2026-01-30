using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using MacroGUI.Services;

namespace MacroGUI.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly PiSyncService _sync;
        private bool _isConnected;
        private bool _wasConnected;

        public bool IsConnected
        {
            get { return _isConnected; }
            private set
            {
                if (_isConnected == value) return;
                _isConnected = value;
                OnPropertyChanged();
            }
        }

        private string _lastSyncMessage = "-";
        public string LastSyncMessage
        {
            get => _lastSyncMessage;
            set
            {
                _lastSyncMessage = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<MacroVM> Macros { get; } = new ObservableCollection<MacroVM>();

        private MacroVM? _selectedMacro;
        public MacroVM? SelectedMacro
        {
            get { return _selectedMacro; }
            set
            {
                if (ReferenceEquals(_selectedMacro, value))
                    return;

                _selectedMacro = value;
                OnPropertyChanged();
            }
        }
        public MainViewModel(PiSyncService sync)
        {
            _sync = sync;

            // default 3개
            MacroVM m1 = MacroVM.CreateDefault1();
            MacroVM m2 = MacroVM.CreateDefault2();
            MacroVM m3 = MacroVM.CreateDefault3();

            Macros.Add(m1);
            Macros.Add(m2);
            Macros.Add(m3);

            // default 선택 = 1번
            SelectedMacro = m1;
        }

        /// <summary>
        /// 연결 서비스에서 상태가 바뀔 때 호출해줌
        /// </summary>
        public async Task OnConnectionChangedAsync(bool connected)
        {
            IsConnected = connected;

            // Disconnected -> Connected 순간에만 1회 Init
            if (!_wasConnected && connected)
            {
                await _sync.InitOnceAsync(CancellationToken.None);
                LastSyncMessage = _sync.LastMessage;
            }

            // Connected -> Disconnected 되면 다음 재연결 때 InitOnce 다시 하도록 reset
            if (_wasConnected && !connected)
            {
                _sync.ResetInitFlag();
            }

            _wasConnected = connected;
        }

        public Task RefreshAsync()
        {
            // 업데이트 버튼 눌렀을 때 호출
            return _sync.RefreshAsync(CancellationToken.None);
            LastSyncMessage = _sync.LastMessage;
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
