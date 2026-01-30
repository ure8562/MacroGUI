using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MacroGUI.ViewModels
{
    public sealed class MacroVM
    {
        public string Name { get; }

        public ObservableCollection<MacroStepVM> Steps { get; } = new ObservableCollection<MacroStepVM>();

        public MacroVM(string name)
        {
            Name = name;
        }

        public static MacroVM CreateDefault1()
        {
            MacroVM m = new MacroVM("매크로1");
            m.Steps.Add(new MacroStepVM("Delay", "-", 300));
            m.Steps.Add(new MacroStepVM("KeyDown", "LEFT", 0));
            m.Steps.Add(new MacroStepVM("Tap", "C", 50));
            m.Steps.Add(new MacroStepVM("KeyUp", "LEFT", 0));
            return m;
        }

        public static MacroVM CreateDefault2()
        {
            MacroVM m = new MacroVM("매크로2");
            m.Steps.Add(new MacroStepVM("Tap", "A", 50));
            m.Steps.Add(new MacroStepVM("Delay", "-", 200));
            m.Steps.Add(new MacroStepVM("Tap", "S", 50));
            return m;
        }

        public static MacroVM CreateDefault3()
        {
            MacroVM m = new MacroVM("매크로3");
            m.Steps.Add(new MacroStepVM("Tap", "D", 50));
            m.Steps.Add(new MacroStepVM("Delay", "-", 150));
            m.Steps.Add(new MacroStepVM("Tap", "F", 50));
            return m;
        }
    }
}
