using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MacroGUI.ViewModels
{
    public sealed class MacroStepVM
    {
        public string Type { get; }
        public string Key { get; }
        public int DurationMs { get; }

        public MacroStepVM(string type, string key, int durationMs)
        {
            Type = type;
            Key = key;
            DurationMs = durationMs;
        }

        public override string ToString()
        {
            if (Type == "Delay")
                return $"Delay {DurationMs}ms";
            if (Type == "Tap")
                return $"Tap {Key} ({DurationMs}ms)";
            if (Type == "KeyDown" || Type == "KeyUp")
                return $"{Type} {Key}";
            return $"{Type} {Key} {DurationMs}ms";
        }
    }
}
