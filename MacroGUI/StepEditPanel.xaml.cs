using MacroGUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MacroGUI
{
    /// <summary>
    /// StepEditPanel.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class StepEditPanel : UserControl
    {
        private static readonly Regex DigitsRegex = new Regex("^[0-9]+$");
        private bool _isCapturingKey;
        private Button? _captureButton;
        private TextBlock? _captureHint;
        private TextBox? _keyDisplayTextBox;

        public StepEditPanel()
        {
            InitializeComponent();
        }
        public object? SelectedStep
        {
            get { return GetValue(SelectedStepProperty); }
            set { SetValue(SelectedStepProperty, value); }
        }

        public static readonly DependencyProperty SelectedStepProperty =
            DependencyProperty.Register(
                nameof(SelectedStep),
                typeof(object),
                typeof(StepEditPanel),
                new PropertyMetadata(null));

        private void DigitsOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !DigitsRegex.IsMatch(e.Text);
        }

        private void DigitsOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            string text = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
            if (!DigitsRegex.IsMatch(text))
                e.CancelCommand();
        }

        private void CaptureKeyButton_Click(object sender, RoutedEventArgs e)
        {
            _isCapturingKey = true;

            _captureButton = sender as Button;

            if (_captureButton != null)
            {
                FrameworkElement? parent = _captureButton.Parent as FrameworkElement;
                _captureHint = parent?.FindName("CaptureHintText") as TextBlock;

                _keyDisplayTextBox = parent?.FindName("KeyDisplayTextBox") as TextBox; // ✅ 추가

                _captureButton.Content = "키 입력 중... (ESC 취소)";
                if (_captureHint != null)
                    _captureHint.Text = "원하는 키를 한 번 누르세요.";
            }

            Focus();
        }

        private void StepEditPanel_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isCapturingKey)
                return;

            // Alt 조합에서 e.Key가 System으로 들어오는 경우 처리
            Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

            if (key == Key.Escape)
            {
                StopCapture("취소됨");
                e.Handled = true;
                return;
            }

            string keyText = NormalizeKey(key);
            if (string.IsNullOrWhiteSpace(keyText))
            {
                StopCapture("지원하지 않는 키");
                e.Handled = true;
                return;
            }

            // SelectedStep.Key에 주입 (타입 직접 참조 대신 Reflection)
            if (SelectedStep is MacroStepVM step)
            {
                step.Key = keyText;   // ✅ setter 직접 호출
                                      // ✅ 왼쪽까지 재평가 트리거
            }

            // ✅ 추가: 표시 즉시 갱신 강제
            System.Windows.Data.BindingExpression? be =
                _keyDisplayTextBox?.GetBindingExpression(TextBox.TextProperty);
            be?.UpdateTarget();

            StopCapture($"입력됨: {keyText}");
            e.Handled = true;
        }

        private void StopCapture(string message)
        {
            _isCapturingKey = false;

            if (_captureButton != null)
                _captureButton.Content = "키 입력 받기";

            if (_captureHint != null)
                _captureHint.Text = message;

            _captureButton = null;
            _captureHint = null;
        }



        private static string NormalizeKey(Key key)
        {
            // 알파벳
            if (key >= Key.A && key <= Key.Z)
                return key.ToString().ToUpperInvariant();

            // 숫자 (상단)
            if (key >= Key.D0 && key <= Key.D9)
                return ((int)(key - Key.D0)).ToString();

            // 방향키 / 기본키
            if (key == Key.Up) return "UP";
            if (key == Key.Down) return "DOWN";
            if (key == Key.Left) return "LEFT";
            if (key == Key.Right) return "RIGHT";

            if (key == Key.Enter) return "ENTER";
            if (key == Key.Space) return "SPACE";
            if (key == Key.Tab) return "TAB";
            if (key == Key.Back) return "BACKSPACE";
            if (key == Key.Delete) return "DELETE";
            if (key == Key.Home) return "HOME";
            if (key == Key.End) return "END";
            if (key == Key.PageUp) return "PAGEUP";
            if (key == Key.PageDown) return "PAGEDOWN";
            if (key == Key.Insert) return "INSERT";

            // Ctrl/Shift/Alt (좌/우 구분)
            if (key == Key.LeftCtrl) return "LCTRL";
            if (key == Key.RightCtrl) return "RCTRL";
            if (key == Key.LeftShift) return "LSHIFT";
            if (key == Key.RightShift) return "RSHIFT";
            if (key == Key.LeftAlt) return "LALT";
            if (key == Key.RightAlt) return "RALT";

            // Win 키
            if (key == Key.LWin) return "LWIN";
            if (key == Key.RWin) return "RWIN";

            // 기능키
            if (key >= Key.F1 && key <= Key.F24)
                return key.ToString().ToUpperInvariant();

            // NumPad (원하면 포맷 바꿔도 됨)
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return "NUM" + ((int)(key - Key.NumPad0)).ToString();

            if (key == Key.Add) return "NUMPLUS";
            if (key == Key.Subtract) return "NUMMINUS";
            if (key == Key.Multiply) return "NUMMUL";
            if (key == Key.Divide) return "NUMDIV";
            if (key == Key.Decimal) return "NUMDOT";

            // 그 외는 필요해지면 케이스 추가
            return string.Empty;
        }

    }
}
