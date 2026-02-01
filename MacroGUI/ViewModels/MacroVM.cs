using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MacroGUI.ViewModels
{
    public sealed class MacroVM : INotifyPropertyChanged
    {
        private string _name;
        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value ?? string.Empty;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }

        private string _memo = string.Empty;
        public string Memo
        {
            get => _memo;
            set
            {
                if (_memo != value)
                {
                    _memo = value ?? string.Empty;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Memo)));
                }
            }
        }

        // ✅ trigger.keys 저장용
        public ObservableCollection<string> TriggerKeys { get; } = new ObservableCollection<string>();

        // ✅ UI 표시용: "ALT + A" / "-" 
        public string TriggerText
        {
            get
            {
                if (TriggerKeys.Count <= 0)
                    return "-";

                return string.Join(" + ", TriggerKeys);
            }
        }

        public ObservableCollection<MacroStepVM> Steps { get; } = new ObservableCollection<MacroStepVM>();

        public MacroVM(string name)
        {
            _name = name ?? string.Empty;
            TriggerKeys.CollectionChanged += (s, e) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TriggerText)));
        }
        public event PropertyChangedEventHandler? PropertyChanged;

    }
}
