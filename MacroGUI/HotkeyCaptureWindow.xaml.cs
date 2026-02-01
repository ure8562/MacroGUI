using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MacroGUI
{
    /// <summary>
    /// HotkeyCaptureWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class HotkeyCaptureWindow : Window
    {
        public HotkeyCaptureWindow()
        {
            InitializeComponent();
        }

        public List<string> CapturedKeys { get; private set; } = new List<string>();

        public HotkeyCaptureWindow(string currentText)
        {
            InitializeComponent();
            if (!string.IsNullOrWhiteSpace(currentText) && currentText != "-")
                TbPreview.Text = currentText;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Alt 조합일 때 KeyDown만으로는 깔끔히 안 잡히는 경우가 있어 PreviewKeyDown에서 처리
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                return;
            }

            Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

            // modifier만 누른 경우는 무시
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift)
            {
                return;
            }

            ModifierKeys mods = Keyboard.Modifiers;

            // Win키는 허용 안 함 (WPF에서 보통 안 들어오지만 안전하게 차단)
            if ((mods & ModifierKeys.Windows) != 0)
                return;

            List<string> list = new List<string>();

            if ((mods & ModifierKeys.Control) != 0) list.Add("CTRL");
            if ((mods & ModifierKeys.Alt) != 0) list.Add("ALT");
            if ((mods & ModifierKeys.Shift) != 0) list.Add("SHIFT");

            string main = NormalizeKey(key);
            if (string.IsNullOrWhiteSpace(main))
                return;

            list.Add(main);

            CapturedKeys = list;
            TbPreview.Text = string.Join(" + ", CapturedKeys);

            DialogResult = true;
            Close();

            e.Handled = true;
        }

        private static string NormalizeKey(Key key)
        {
            // A~Z, 0~9
            if (key >= Key.A && key <= Key.Z) return key.ToString().ToUpperInvariant();
            if (key >= Key.D0 && key <= Key.D9) return ((int)(key - Key.D0)).ToString();
            if (key >= Key.NumPad0 && key <= Key.NumPad9) return "NUM" + ((int)(key - Key.NumPad0));

            // F1~F24
            if (key >= Key.F1 && key <= Key.F24) return key.ToString().ToUpperInvariant();

            // arrows & common keys
            if (key == Key.Up) return "UP";
            if (key == Key.Down) return "DOWN";
            if (key == Key.Left) return "LEFT";
            if (key == Key.Right) return "RIGHT";

            if (key == Key.Space) return "SPACE";
            if (key == Key.Enter) return "ENTER";
            if (key == Key.Tab) return "TAB";
            if (key == Key.Back) return "BACKSPACE";
            if (key == Key.Delete) return "DELETE";
            if (key == Key.Insert) return "INSERT";
            if (key == Key.Home) return "HOME";
            if (key == Key.End) return "END";
            if (key == Key.PageUp) return "PAGEUP";
            if (key == Key.PageDown) return "PAGEDOWN";

            return key.ToString().ToUpperInvariant();
        }
    }
}
