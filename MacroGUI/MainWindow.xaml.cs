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
                StatusTextBlock.Text = connected ? "Connected" : "Disconnected";
                StatusDot.Fill = connected ? Brushes.LimeGreen : Brushes.IndianRed;
            });

            await _vm.OnConnectionChangedAsync(connected);
        }

    }
}