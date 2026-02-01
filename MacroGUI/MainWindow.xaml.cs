using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MacroGUI.Services;
using MacroGUI.ViewModels;

namespace MacroGUI
{
    public partial class MainWindow : Window
    {
        private readonly SshConnectionService _ssh;
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();

            SshCommandService cmd = new SshCommandService("192.168.0.22", "bong");
            PiSyncService sync = new PiSyncService(cmd);

            _vm = new MainViewModel(sync);
            DataContext = _vm;

            _ssh = new SshConnectionService("192.168.0.22", "bong", TimeSpan.FromSeconds(1));
            _ssh.ConnectionChanged += OnConnectionChanged;
            _ssh.KeyboardConnectionChanged += OnKeyboardConnectionChanged;
            _ssh.Start();
        }
        protected override void OnClosed(EventArgs e)
        {
            _ssh.Dispose();
            base.OnClosed(e);
        }

        private async void OnConnectionChanged(bool connected)
        {
            Dispatcher.Invoke(() =>
            {
                // SSH 상태
                SshStatusTextBlock.Text = connected ? "SSH: Connected" : "SSH: Disconnected";
                SshDot.Fill = connected ? Brushes.LimeGreen : Brushes.IndianRed;

                // ✅ SSH 끊기면 키보드도 강제 Disconnected
                if (!connected)
                {
                    KbdStatusTextBlock.Text = "KBD: Disconnected";
                    KbdDot.Fill = Brushes.IndianRed;
                }
            });

            await _vm.OnConnectionChangedAsync(connected);
        }
        private void OnKeyboardConnectionChanged(bool connected)
        {
            Dispatcher.Invoke(() =>
            {
                KbdStatusTextBlock.Text = connected ? "KBD: Connected" : "KBD: Disconnected";
                KbdDot.Fill = connected ? Brushes.LimeGreen : Brushes.IndianRed;
            });
        }
        
        private void ListBoxItem_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            System.Windows.Controls.ListBoxItem item = sender as System.Windows.Controls.ListBoxItem;
            if (item == null)
                return;

            item.IsSelected = true;
            item.Focus();

            e.Handled = false; // ContextMenu 뜨는 거 방해하지 않게
        }
    }
}