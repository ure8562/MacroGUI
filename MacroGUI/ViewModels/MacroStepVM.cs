using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MacroGUI.ViewModels
{
    public sealed class MacroStepVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _type;
        private string _key;
        private int _durationMs;
        private int _minMs;
        private int _maxMs;
        private bool _isDelayRangeValid = true;
        private string _delayRangeError = string.Empty;
        private void RevalidateDelayRange()
        {
            // 규칙:
            // - 둘 중 하나가 0 이하이면 "미입력"으로 간주하고 허용
            // - 둘 다 0보다 크면 Min <= Max 이어야 함

            if (_minMs <= 0 || _maxMs <= 0)
            {
                IsDelayRangeValid = true;
                DelayRangeError = string.Empty;
                return;
            }

            if (_minMs <= _maxMs)
            {
                IsDelayRangeValid = true;
                DelayRangeError = string.Empty;
                return;
            }

            IsDelayRangeValid = false;
            DelayRangeError = "MinMs는 MaxMs 이하이어야 합니다.";
        }
        public bool IsDelayRangeValid
        {
            get { return _isDelayRangeValid; }
            private set
            {
                if (_isDelayRangeValid != value)
                {
                    _isDelayRangeValid = value;
                    OnPropertyChanged(nameof(IsDelayRangeValid));
                }
            }
        }

        public string DelayRangeError
        {
            get { return _delayRangeError; }
            private set
            {
                if (_delayRangeError != value)
                {
                    _delayRangeError = value;
                    OnPropertyChanged(nameof(DelayRangeError));
                }
            }
        }
        public string Type
        {
            get { return _type; }
        }

        public string Key
        {
            get { return _key; }
            set
            {
                if (!string.Equals(_key, value, StringComparison.Ordinal))
                {
                    _key = value;
                    OnPropertyChanged(nameof(Key));
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        public int DurationMs
        {
            get { return _durationMs; }
            set
            {
                if (_durationMs != value)
                {
                    _durationMs = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        public int MinMs
        {
            get { return _minMs; }
            set
            {
                if (_minMs != value)
                {
                    _minMs = value;
                    OnPropertyChanged(nameof(MinMs));
                    OnPropertyChanged(nameof(DisplayText));
                    RevalidateDelayRange();
                }
            }
        }

        public int MaxMs
        {
            get { return _maxMs; }
            set
            {
                if (_maxMs != value)
                {
                    _maxMs = value;
                    OnPropertyChanged(nameof(MaxMs));
                    OnPropertyChanged(nameof(DisplayText));
                    RevalidateDelayRange();
                }
            }
        }

        /// <summary>ListBox 표시용 문자열</summary>
        public string DisplayText
        {
            get { return ToString(); }
        }


        public MacroStepVM(string type, string key, int durationMs)
        {
            _type = type;
            _key = key;
            _durationMs = durationMs;

            _minMs = 0;
            _maxMs = 0;
        }
        public MacroStepVM(string type, string key, int durationMs, int minMs, int maxMs)
        {
            _type = type;
            _key = key;
            _durationMs = durationMs;

            _minMs = minMs;
            _maxMs = maxMs;
        }

        public override string ToString()
        {
            if (string.Equals(Type, "Delay", StringComparison.OrdinalIgnoreCase))
            {
                if (MinMs > 0 || MaxMs > 0)
                    return $"Delay {MinMs}~{MaxMs}ms";

                return $"Delay {DurationMs}ms";
            }

            if (string.Equals(Type, "Tap", StringComparison.OrdinalIgnoreCase))
                return $"Tap {Key}";

            if (string.Equals(Type, "KeyDown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Type, "KeyUp", StringComparison.OrdinalIgnoreCase))
                return $"{Type} {Key}";

            return $"{Type} {Key}";
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
