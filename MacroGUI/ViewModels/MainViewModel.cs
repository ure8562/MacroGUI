using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using MacroGUI.Services;

namespace MacroGUI.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string _selectedPresetFile = "";
        public string SelectedPresetFile
        {
            get { return _selectedPresetFile; }
            set { _selectedPresetFile = value; OnPropertyChanged(); }
        }

        private string _presetNewName = "macros_new.json";
        public string PresetNewName
        {
            get { return _presetNewName; }
            set { _presetNewName = value; OnPropertyChanged(); }
        }
        public ObservableCollection<string> PresetFiles { get; } = new ObservableCollection<string>();

        private bool _applyAlso = true;
        public bool ApplyAlso
        {
            get { return _applyAlso; }
            set { _applyAlso = value; OnPropertyChanged(); }
        }



        public ICommand RefreshCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand PresetRefreshCommand { get; }
        public ICommand PresetLoadCommand { get; }
        public ICommand PresetApplyCommand { get; }
        public ICommand PresetSaveCommand { get; }
        public ICommand AddMacroCommand { get; }


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

            RefreshCommand = new AsyncCommand(OnRefreshAsync);
            SaveCommand = new AsyncCommand(OnSaveAsync);
            PresetRefreshCommand = new AsyncCommand(RefreshPresetListAsync);
            PresetLoadCommand = new AsyncCommand(LoadPresetAsync);
            PresetApplyCommand = new AsyncCommand(ApplyPresetAsync);
            PresetSaveCommand = new AsyncCommand(SavePresetAsync);
            AddMacroCommand = new AsyncCommand(AddMacroAsync);

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

        public async Task RefreshAsync()
        {
            // 업데이트 버튼 눌렀을 때 호출
            await _sync.RefreshAsync(CancellationToken.None);
            LastSyncMessage = _sync.LastMessage;
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private async Task OnRefreshAsync()
        {
            await _sync.RefreshAsync(CancellationToken.None);
            OnPropertyChanged(nameof(LastSyncMessage));
        }

        private async Task OnSaveAsync()
        {
            await _sync.SaveMacrosAsync(CancellationToken.None);
            OnPropertyChanged(nameof(LastSyncMessage));
        }
        private async Task RefreshPresetListAsync()
        {
            List<string> list = await _sync.ListPresetFilesAsync(CancellationToken.None);

            PresetFiles.Clear();
            foreach (string f in list)
                PresetFiles.Add(f);

            if (PresetFiles.Count > 0 && string.IsNullOrWhiteSpace(SelectedPresetFile))
                SelectedPresetFile = PresetFiles[0];

            LastSyncMessage = _sync.LastMessage;
            OnPropertyChanged(nameof(LastSyncMessage));
        }

        private async Task LoadPresetAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedPresetFile))
                return;

            await _sync.LoadPresetAsync(SelectedPresetFile, CancellationToken.None);

            LastSyncMessage = _sync.LastMessage;
            OnPropertyChanged(nameof(LastSyncMessage));
        }

        private async Task ApplyPresetAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedPresetFile))
                return;

            await _sync.ApplyPresetAsync(SelectedPresetFile, CancellationToken.None);

            LastSyncMessage = _sync.LastMessage;
            OnPropertyChanged(nameof(LastSyncMessage));
        }

        private async Task SavePresetAsync()
        {
            string name = (PresetNewName ?? "").Trim();

            if (name.Length == 0)
                name = "macros_new.json";

            if (!name.StartsWith("macros_", StringComparison.OrdinalIgnoreCase))
                name = "macros_" + name;

            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                name = name + ".json";

            PresetNewName = name;
            OnPropertyChanged(nameof(PresetNewName));

            await _sync.SavePresetAsync(name, ApplyAlso, CancellationToken.None);

            LastSyncMessage = _sync.LastMessage;
            OnPropertyChanged(nameof(LastSyncMessage));

            await RefreshPresetListAsync();
        }
        private Task AddMacroAsync()
        {
            int n = Macros.Count + 1;
            MacroVM m = new MacroVM($"NewMacro{n}");

            // 기본 Step 하나 넣어두면 UI 확인이 쉬움(원하면 제거 가능)
            m.Steps.Add(new MacroStepVM("Delay", "-", 200));

            Macros.Add(m);
            SelectedMacro = m;

            LastSyncMessage = "Macro added (not saved yet)";
            OnPropertyChanged(nameof(LastSyncMessage));

            return Task.CompletedTask;
        }
    }
}
