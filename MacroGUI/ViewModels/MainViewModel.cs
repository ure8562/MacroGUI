using MacroGUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MacroGUI.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        

        #endregion

        #region Services and Status
        private readonly PiSyncService _sync;
        private bool _isConnected;
        private bool _wasConnected;
        private bool _presetRefreshedOnConnect;

        public bool IsConnected
        {
            get { return _isConnected; }
            private set
            {
                if (_isConnected == value) return;

                bool wasConnected = _isConnected;
                _isConnected = value;
                OnPropertyChanged();

                // 🔹 미연결 → 연결 : 프리셋 목록 1회 갱신
                if (!wasConnected && _isConnected)
                {
                    if (!_presetRefreshedOnConnect)
                    {
                        _presetRefreshedOnConnect = true;
                        _ = RefreshPresetListAsync(); // 1회 호출
                    }
                }
                // 🔹 연결 끊김 → 다음 연결 시 다시 1회 허용
                else if (wasConnected && !_isConnected)
                {
                    _presetRefreshedOnConnect = false;
                }
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
        private string _newStepType = "Delay";
        public string NewStepType
        {
            get { return _newStepType; }
            set
            {
                if (_newStepType != value)
                {
                    _newStepType = value;
                    OnPropertyChanged(nameof(NewStepType));
                }
            }
        }

        public ObservableCollection<string> StepTypeItems { get; }
            = new ObservableCollection<string> { "Tap", "Delay" };
        #endregion

        #region Preset Properties

        private string _selectedPresetFile = "";
        public string SelectedPresetFile
        {
            get { return _selectedPresetFile; }
            set
            {
                if (string.Equals(_selectedPresetFile, value, StringComparison.Ordinal))
                    return;

                _selectedPresetFile = value;
                OnPropertyChanged();
                _ = OnSelectedPresetFileChangedAsync(value);
            }
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

        private bool _isMacroMoveUpEnabled;
        public bool IsMacroMoveUpEnabled
        {
            get { return _isMacroMoveUpEnabled; }
            private set
            {
                if (_isMacroMoveUpEnabled == value)
                    return;
                _isMacroMoveUpEnabled = value;
                OnPropertyChanged(nameof(IsMacroMoveUpEnabled));
            }
        }

        private bool _isMacroMoveDownEnabled;
        public bool IsMacroMoveDownEnabled
        {
            get { return _isMacroMoveDownEnabled; }
            private set
            {
                if (_isMacroMoveDownEnabled == value)
                    return;
                _isMacroMoveDownEnabled = value;
                OnPropertyChanged(nameof(IsMacroMoveDownEnabled));
            }
        }

        #endregion

        #region Macro Properties
        public ObservableCollection<MacroVM> Macros { get; } = new ObservableCollection<MacroVM>();

        private MacroVM? _selectedMacro;
        public MacroVM? SelectedMacro
        {
            get { return _selectedMacro; }
            set
            {
                if (ReferenceEquals(_selectedMacro, value))
                    return;

                // 1️⃣ 이전 매크로 구독 해제
                if (_selectedMacro != null)
                    UnsubscribeMacro(_selectedMacro);

                _selectedMacro = value;

                // 2️⃣ 새 매크로 구독
                if (_selectedMacro != null)
                    SubscribeMacro(_selectedMacro);

                // 3️⃣ 알림
                OnPropertyChanged();
                NotifyPresetButtons();
                StepAddCommand?.CanExecute(null);
                StepDeleteCommand?.CanExecute(null);
                RaiseStepButtonsCanExecute();
                UpdateMacroMoveButtons();
            }
        }

        private MacroStepVM? _selectedStep;

        public MacroStepVM? SelectedStep
        {
            get { return _selectedStep; }
            set
            {
                if (!ReferenceEquals(_selectedStep, value))
                {
                    _selectedStep = value;
                    OnPropertyChanged();
                    StepAddCommand?.CanExecute(null);
                    StepDeleteCommand?.CanExecute(null);
                    RaiseStepButtonsCanExecute();
                    // ✅ 이것만 추가
                    (StepMoveUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (StepMoveDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(IsStepMoveUpEnabled));
                    OnPropertyChanged(nameof(IsStepMoveDownEnabled));
                }
            }
        }

        #endregion

        #region Commands
        public ICommand RefreshCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand PresetRefreshCommand { get; }
        public ICommand PresetLoadCommand { get; }
        public ICommand PresetApplyCommand { get; }
        public ICommand PresetSaveCommand { get; }
        public ICommand AddMacroCommand { get; }
        public ICommand StepAddCommand { get; }
        public ICommand StepDeleteCommand { get; }
        public ICommand MacroRenameCommand { get; }
        public ICommand MacroDeleteCommand { get; }
        public ICommand MacroHotkeyCommand { get; }
        public ICommand PresetDeleteCommand { get; }
        public ICommand PresetOverwriteCommand { get; }
        public ICommand MacroMoveUpCommand { get; }
        public ICommand MacroMoveDownCommand { get; }
        public ICommand StepMoveUpCommand { get; }
        public ICommand StepMoveDownCommand { get; }
        #endregion



        public MainViewModel(PiSyncService sync)
        {
            _sync = sync;

            RefreshCommand = new AsyncCommand(OnRefreshAsync);
            SaveCommand = new AsyncCommand(OnSaveAsync);
            PresetRefreshCommand = new AsyncCommand(RefreshPresetListAsync);
            PresetLoadCommand = new AsyncCommand(LoadPresetAsync);
            PresetApplyCommand = new AsyncCommand(ApplyPresetAsync);
            PresetSaveCommand = new AsyncCommand(SavePresetAsync);
            AddMacroCommand = new AsyncCommand(AddMacroAsync);
            StepAddCommand = new RelayCommand(AddStep, CanAddStep);
            StepDeleteCommand = new RelayCommand(DeleteStep, CanDeleteStep);
            MacroRenameCommand = new RelayCommand(RenameMacro, CanMacroAction);
            MacroDeleteCommand = new RelayCommand(DeleteMacro, CanMacroAction);
            MacroHotkeyCommand = new RelayCommand(ChangeMacroHotkey, CanMacroAction);
            PresetDeleteCommand = new AsyncCommand(DeletePresetAsync);
            PresetOverwriteCommand = new AsyncCommand(OverwritePresetAsync);
            MacroMoveUpCommand = new RelayCommand(MoveMacroUp, CanMoveMacroUp);
            MacroMoveDownCommand = new RelayCommand(MoveMacroDown, CanMoveMacroDown); 
            StepMoveUpCommand = new RelayCommand(MoveStepUp);
            StepMoveDownCommand = new RelayCommand(MoveStepDown);

            // Macros 변동 시 버튼 상태도 갱신
            Macros.CollectionChanged += (s, e) => UpdateMacroMoveButtons();
        }

        #region Connection and Sync
        /// <summary>
        /// 연결 서비스에서 상태가 바뀔 때 호출해줌
        /// </summary>
        public async Task OnConnectionChangedAsync(bool connected)
        {
            IsConnected = connected;

            if (!_wasConnected && connected)
            {
                await _sync.InitOnceAsync(CancellationToken.None);

                // 초기 로드 결과를 UI에 반영 (로드 실패 시 기본 유지)
                await ApplySyncToUiAsync(keepDefaultsOnError: true);
            }

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
        private async Task ApplySyncToUiAsync(bool keepDefaultsOnError)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // 에러면 UI 덮어쓰기 하지 않음 (기본 3개 유지 옵션)
                if (keepDefaultsOnError)
                {
                    string msg = _sync.LastMessage ?? string.Empty;
                    if (msg.StartsWith("PI ERR") || msg.StartsWith("JSON PARSE ERR"))
                    {
                        LastSyncMessage = _sync.LastMessage;
                        OnPropertyChanged(nameof(LastSyncMessage));
                        return;
                    }
                }

                string? prevName = SelectedMacro?.Name;

                Macros.Clear();
                foreach (MacroVM m in _sync.Macros)
                    Macros.Add(m);

                if (prevName != null)
                    SelectedMacro = Macros.FirstOrDefault(x => x.Name == prevName) ?? Macros.FirstOrDefault();
                else
                    SelectedMacro = Macros.FirstOrDefault();

                LastSyncMessage = _sync.LastMessage;
                OnPropertyChanged(nameof(LastSyncMessage));
            });
        }



        #endregion

        #region Button Events
        private async Task OnRefreshAsync()
        {

            await _sync.RefreshAsync(CancellationToken.None);

            // 로드 결과를 UI에 반영 (로드 실패 시 기본 유지)
            await ApplySyncToUiAsync(keepDefaultsOnError: true);

            // ✅ 콤보를 macros_active.json으로 강제 선택
            const string active = "macros_active.json";
            if (PresetFiles.Contains(active))
            {
                SelectedPresetFile = active;
                OnPropertyChanged(nameof(SelectedPresetFile));
            }
        }

        private async Task OnSaveAsync()
        {
            await _sync.SaveMacrosAsync(CancellationToken.None);
            OnPropertyChanged(nameof(LastSyncMessage));
        }
        private async Task RefreshPresetListAsync()
        {
            string keep = SelectedPresetFile ?? string.Empty;

            List<string> list = await _sync.ListPresetFilesAsync(CancellationToken.None);

            App.Current.Dispatcher.Invoke(() =>
            {
                PresetFiles.Clear();
                foreach (string f in list)
                    PresetFiles.Add(f);

                // ✅ 기존 선택이 목록에 있으면 유지
                if (!string.IsNullOrWhiteSpace(keep) && PresetFiles.Contains(keep))
                {
                    SelectedPresetFile = keep;
                }
                // ✅ 없으면 첫 항목
                else if (PresetFiles.Count > 0)
                {
                    SelectedPresetFile = PresetFiles[0];
                }

                LastSyncMessage = _sync.LastMessage;
                OnPropertyChanged(nameof(LastSyncMessage));
                OnPropertyChanged(nameof(SelectedPresetFile));
            });
        }

        private async Task LoadPresetAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedPresetFile))
                return;

            await _sync.LoadPresetAsync(SelectedPresetFile, CancellationToken.None);

            LastSyncMessage = _sync.LastMessage;
            OnPropertyChanged(nameof(LastSyncMessage));
        }

        private void SyncMacrosToService()
        {
            _sync.Macros.Clear();
            foreach (MacroVM m in Macros)
                _sync.Macros.Add(m);
        }

        private async Task SavePresetAsync()
        {
            string name = (PresetNewName ?? "").Trim();

            if (name.Length == 0)
                name = "macros_new.json";

            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                name = name + ".json";

            SyncMacrosToService();

            PresetNewName = name;
            OnPropertyChanged(nameof(PresetNewName));


            // 1) 저장만
            await _sync.SavePresetAsync(name, false, CancellationToken.None);

            // 2) 프리셋 목록 갱신 + 방금 저장한 파일 선택
            await RefreshPresetListAsync();
            SelectedPresetFile = name;
            OnPropertyChanged(nameof(SelectedPresetFile));

            // 3) 필요 시 적용(Apply 버튼 흐름)
            if (ApplyAlso)
            {
                await ApplyPresetAsync();
            }
            await RefreshPresetListAsync();

            // 4) 메시지 갱신(저장/적용 결과가 최종으로 남게)
            LastSyncMessage = _sync.LastMessage;
            OnPropertyChanged(nameof(LastSyncMessage));
        }

        private async Task ApplyPresetAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedPresetFile))
                return;

            // 1) Pi에 적용(load)
            await _sync.ApplyPresetAsync(SelectedPresetFile, CancellationToken.None);

            // 2) 적용 결과가 성공이면 active를 다시 읽어서 UI 확정 (권장)
            //    실패면 keepDefaultsOnError=true라서 UI는 유지됨
            await _sync.RefreshAsync(CancellationToken.None);
            await ApplySyncToUiAsync(keepDefaultsOnError: true);

            LastSyncMessage = _sync.LastMessage;
            OnPropertyChanged(nameof(LastSyncMessage));
        }
        private Task AddMacroAsync()
        {
            //_sync.Macros 사용?
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

        #endregion



        private async Task OnSelectedPresetFileChangedAsync(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            string remotePath = $"/opt/bong_macro/{fileName}";

            // ✅ 기존에 동작하는 경로 재사용
            await _sync.RefreshAsync(remotePath, CancellationToken.None);

            await ApplySyncToUiAsync(keepDefaultsOnError: true);
        }

        public bool IsPresetActionsEnabled
        {
            get { return IsConnected && IsSelectedMacroValid(); }
        }

        private bool IsSelectedMacroValid()
        {
            if (SelectedMacro == null)
                return false;

            foreach (MacroStepVM step in SelectedMacro.Steps)
            {
                // Delay 스텝만 검증
                if (step.Type == "Delay" && !step.IsDelayRangeValid)
                    return false;
            }

            return true;
        }

        public bool IsPresetSaveEnabled
        {
            get { return IsSelectedMacroValid(); }
        }

        public bool IsPresetApplyEnabled
        {
            get { return IsConnected && IsSelectedMacroValid(); }
        }

        private void Step_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MacroStepVM.IsDelayRangeValid) ||
                e.PropertyName == nameof(MacroStepVM.MinMs) ||
                e.PropertyName == nameof(MacroStepVM.MaxMs))
            {
                NotifyPresetButtons();
            }
        }

        private void NotifyPresetButtons()
        {
            OnPropertyChanged(nameof(IsPresetSaveEnabled));
            OnPropertyChanged(nameof(IsPresetApplyEnabled));
        }

        private void SubscribeMacro(MacroVM macro)
        {
            macro.Steps.CollectionChanged += Steps_CollectionChanged;

            foreach (MacroStepVM step in macro.Steps)
                step.PropertyChanged += Step_PropertyChanged;
        }

        private void UnsubscribeMacro(MacroVM macro)
        {
            macro.Steps.CollectionChanged -= Steps_CollectionChanged;

            foreach (MacroStepVM step in macro.Steps)
                step.PropertyChanged -= Step_PropertyChanged;
        }
        private void Steps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (MacroStepVM step in e.OldItems)
                    step.PropertyChanged -= Step_PropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (MacroStepVM step in e.NewItems)
                    step.PropertyChanged += Step_PropertyChanged;
            }

            NotifyPresetButtons();
        }


        private bool CanAddStep()
        {
            return SelectedMacro != null;
        }

        private bool CanDeleteStep()
        {
            return SelectedMacro != null && SelectedStep != null;
        }

        private void AddStep()
        {
            if (SelectedMacro == null)
                return;

            var steps = SelectedMacro.Steps;

            int insertIndex = steps.Count;
            if (SelectedStep != null)
            {
                int idx = steps.IndexOf(SelectedStep);
                if (idx >= 0)
                    insertIndex = idx + 1;
            }

            // 기본: Delay step 삽입
            MacroStepVM newStep;


            if (NewStepType == "Tap")
            {
                newStep = new MacroStepVM("Tap", "A", 0); // 기본 Key는 임시값. 빈 값 허용이면 ""로
            }
            else // Delay
            {
                newStep = new MacroStepVM("Delay", string.Empty, 0, 0, 0);
            }

            steps.Insert(insertIndex, newStep);
            SelectedStep = newStep;

            NotifyPresetButtons();
            ((RelayCommand)StepDeleteCommand).RaiseCanExecuteChanged();
        }

        private void DeleteStep()
        {
            if (SelectedMacro == null || SelectedStep == null)
                return;

            var steps = SelectedMacro.Steps;

            int index = steps.IndexOf(SelectedStep);
            if (index < 0)
                return;

            steps.RemoveAt(index);

            if (steps.Count == 0)
                SelectedStep = null;
            else if (index < steps.Count)
                SelectedStep = steps[index];
            else
                SelectedStep = steps[steps.Count - 1];

            NotifyPresetButtons();
            ((RelayCommand)StepDeleteCommand).RaiseCanExecuteChanged();
        }
        private void RaiseStepButtonsCanExecute()
        {
            (StepAddCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (StepDeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private bool CanMacroAction(object? param)
        {
            return param is MacroVM;
        }

        private void DeleteMacro(object? param)
        {
            MacroVM macro = param as MacroVM;
            if (macro == null)
                return;

            int idx = Macros.IndexOf(macro);
            if (idx < 0)
                return;

            Macros.RemoveAt(idx);

            if (Macros.Count == 0)
                SelectedMacro = null;
            else if (idx < Macros.Count)
                SelectedMacro = Macros[idx];
            else
                SelectedMacro = Macros[Macros.Count - 1];

            NotifyPresetButtons();
        }

        private void RenameMacro(object? param)
        {
            MacroVM macro = param as MacroVM;
            if (macro == null)
                return;

            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "새 프리셋 이름을 입력하세요.",
                "이름 변경",
                macro.Name);

            string newName = (input ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(newName))
                return;

            macro.Name = newName;
            NotifyPresetButtons();
        }

        private void ChangeMacroHotkey(object? param)
        {
            MacroVM macro = param as MacroVM;
            if (macro == null)
                return;

            HotkeyCaptureWindow w = new HotkeyCaptureWindow(macro.TriggerText);
            w.Owner = Application.Current.MainWindow;

            bool? ok = w.ShowDialog();
            if (ok != true)
                return;

            List<string> newKeys = w.CapturedKeys;
            string newSig = BuildHotkeySignature(newKeys);

            if (string.IsNullOrWhiteSpace(newSig))
                return;

            // Macros 컬렉션에서 충돌 검사
            foreach (MacroVM m in Macros)
            {
                if (m == null)
                    continue;

                if (object.ReferenceEquals(m, macro))
                    continue;

                string sig = BuildHotkeySignature(m.TriggerKeys);
                if (string.Equals(sig, newSig, StringComparison.OrdinalIgnoreCase))
                {
                    System.Windows.MessageBox.Show(
                        "이미 다른 매크로가 같은 핫키를 사용 중입니다.\n\n" +
                        "충돌 매크로: " + m.Name + "\n" +
                        "핫키: " + string.Join(" + ", newKeys),
                        "핫키 충돌",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);

                    return;
                }
            }

            macro.TriggerKeys.Clear();
            foreach (string k in w.CapturedKeys)
                macro.TriggerKeys.Add(k);

            NotifyPresetButtons();
        }
        private static string BuildHotkeySignature(IEnumerable<string> keys)
        {
            if (keys == null)
                return string.Empty;

            List<string> list = new List<string>();

            foreach (string k in keys)
            {
                string t = (k ?? string.Empty).Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(t))
                    continue;

                list.Add(t);
            }

            // 순서 통일(CTRL/ALT/SHIFT 먼저)
            List<string> ordered = new List<string>();
            if (list.Contains("CTRL")) ordered.Add("CTRL");
            if (list.Contains("ALT")) ordered.Add("ALT");
            if (list.Contains("SHIFT")) ordered.Add("SHIFT");

            foreach (string t in list)
            {
                if (t == "CTRL" || t == "ALT" || t == "SHIFT")
                    continue;

                ordered.Add(t);
            }

            return string.Join("+", ordered);
        }

        private bool CanDeletePreset()
        {
            if (string.IsNullOrWhiteSpace(SelectedPresetFile))
                return false;

            if (IsProtectedPreset(SelectedPresetFile))
                return false;

            return true;
        }

        private static bool IsProtectedPreset(string fileName)
        {
            return fileName.Equals("macros_active.json", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals("macros.json", StringComparison.OrdinalIgnoreCase);
        }
        private async Task DeletePresetAsync()
        {
            string file = SelectedPresetFile ?? string.Empty;
            if (!CanDeletePreset())
                return;

            if (MessageBox.Show(
                    $"{file} 을(를) 삭제하시겠습니까?",
                    "프리셋 삭제",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            await _sync.DeletePresetAsync(file, CancellationToken.None);

            LastSyncMessage = _sync.LastMessage;
            OnPropertyChanged(nameof(LastSyncMessage));

            await RefreshPresetListAsync();
        }

        private async Task OverwritePresetAsync()
        {
            string name = (SelectedPresetFile ?? "").Trim();
            if (name.Length == 0)
                return;

            // ⛔ active 파일 보호
            if (string.Equals(name, "macros_active.json", StringComparison.OrdinalIgnoreCase))
            {
                LastSyncMessage = "덮어쓰기 금지된 프리셋입니다.";
                OnPropertyChanged(nameof(LastSyncMessage));
                return;
            }

            // 안전: 확장자 보정(콤보가 항상 .json이면 없어도 됨)
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                name = name + ".json";

            // ✅ 저장 직전 동기화(너가 1번 구조 변경 안 한다고 했으니 필수)
            SyncMacrosToService();

            await _sync.SavePresetAsync(name, false, CancellationToken.None);

            LastSyncMessage = _sync.LastMessage;
            OnPropertyChanged(nameof(LastSyncMessage));

            // 목록 갱신 + 선택 유지
            await RefreshPresetListAsync();
            SelectedPresetFile = name;
            OnPropertyChanged(nameof(SelectedPresetFile));
        }

        private bool CanMoveMacroUp(object? param)
        {
            if (SelectedMacro == null)
                return false;

            int idx = Macros.IndexOf(SelectedMacro);
            return idx > 0;
        }

        private bool CanMoveMacroDown(object? param)
        {
            if (SelectedMacro == null)
                return false;

            int idx = Macros.IndexOf(SelectedMacro);
            return idx >= 0 && idx < Macros.Count - 1;
        }

        private void MoveMacroUp(object? param)
        {
            MacroVM? m = SelectedMacro;
            if (m == null)
                return;

            int idx = Macros.IndexOf(m);
            if (idx <= 0)
                return;

            Macros.Move(idx, idx - 1);
            SelectedMacro = m; // 선택 유지

            UpdateMacroMoveButtons();
        }

        private void MoveMacroDown(object? param)
        {
            MacroVM? m = SelectedMacro;
            if (m == null)
                return;

            int idx = Macros.IndexOf(m);
            if (idx < 0 || idx >= Macros.Count - 1)
                return;

            Macros.Move(idx, idx + 1);
            SelectedMacro = m; // 선택 유지

            UpdateMacroMoveButtons();
        }

        private void UpdateMacroMoveButtons()
        {
            IsMacroMoveUpEnabled = CanMoveMacroUp(null);
            IsMacroMoveDownEnabled = CanMoveMacroDown(null);

            (MacroMoveUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MacroMoveDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        private void MoveStepUp()
        {
            if (SelectedMacro == null || SelectedStep == null)
                return;

            var steps = SelectedMacro.Steps;
            int idx = steps.IndexOf(SelectedStep);
            if (idx <= 0)
                return;

            steps.Move(idx, idx - 1);
        }

        private void MoveStepDown()
        {
            if (SelectedMacro == null || SelectedStep == null)
                return;

            var steps = SelectedMacro.Steps;
            int idx = steps.IndexOf(SelectedStep);
            if (idx < 0 || idx >= steps.Count - 1)
                return;

            steps.Move(idx, idx + 1);
        }

        public bool IsStepMoveUpEnabled =>
        SelectedMacro != null &&
        SelectedStep != null &&
        SelectedMacro.Steps.IndexOf(SelectedStep) > 0;

        public bool IsStepMoveDownEnabled =>
            SelectedMacro != null &&
            SelectedStep != null &&
            SelectedMacro.Steps.IndexOf(SelectedStep) < SelectedMacro.Steps.Count - 1;

    }
}
