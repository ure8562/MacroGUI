using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MacroGUI.Services
{
    /// <summary>
    /// SSH 연결 상태를 주기적으로 체크하는 서비스.
    /// - 1초마다 ssh 원샷 실행으로 Connected/Disconnected 판정
    /// - 상태가 바뀔 때만 이벤트 발생
    /// </summary>
    public sealed class SshConnectionService : IDisposable
    {
        public event Action<bool>? ConnectionChanged;

        public string Host { get; }
        public string User { get; }

        public bool IsConnected { get; private set; }
        public bool IsKeyboardConnected { get; private set; }
        public event Action<bool>? KeyboardConnectionChanged;

        private readonly TimeSpan _interval;
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        public SshConnectionService(string host, string user, TimeSpan interval)
        {
            Host = host;
            User = user;
            _interval = interval;
        }


        public void Start()
        {
            if (_loopTask != null)
                return;

            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => LoopAsync(_cts.Token));
        }
        public void Stop()
        {
            if (_cts == null)
                return;

            try
            {
                _cts.Cancel();
            }
            catch
            {
                // ignore
            }
        }

        public void Dispose()
        {
            Stop();
            try { _loopTask?.Wait(500); } catch { }
            try { _cts?.Dispose(); } catch { }
        }

        private async Task LoopAsync(CancellationToken token)
        {
            PeriodicTimer timer = new PeriodicTimer(_interval);

            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    bool ok = await CheckOnceAsync(token);

                    if (ok != IsConnected)
                    {
                        IsConnected = ok;
                        ConnectionChanged?.Invoke(ok);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 종료
            }
            finally
            {
                timer.Dispose();
            }
        }

        private async Task<bool> CheckOnceAsync(CancellationToken token)
        {
            string remoteCmd =
                "printf 'status\\n' > /tmp/proxykbd_cmd; cat /tmp/proxykbd_status";

            string args =
                $"-o BatchMode=yes " +
                $"-o ConnectTimeout=1 " +
                $"-o StrictHostKeyChecking=accept-new " +
                $"{User}@{Host} \"{remoteCmd}\"";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "ssh",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                using Process p = new Process { StartInfo = psi };
                if (!p.Start())
                    return false;

                Task<string> readStdout = p.StandardOutput.ReadToEndAsync();
                Task<string> readStderr = p.StandardError.ReadToEndAsync();

                Task waitTask = p.WaitForExitAsync(token);
                Task timeoutTask = Task.Delay(1500, token);

                Task finished = await Task.WhenAny(waitTask, timeoutTask);
                if (finished != waitTask)
                {
                    try { p.Kill(true); } catch { }
                    return false;
                }

                if (p.ExitCode != 0)
                    return false;

                string stdout = await readStdout;
                // string stderr = await readStderr; // 필요하면 로그용

                bool kbd =
                    stdout.IndexOf("connected", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    stdout.IndexOf("disconnected", StringComparison.OrdinalIgnoreCase) < 0;

                if (kbd != IsKeyboardConnected)
                {
                    IsKeyboardConnected = kbd;
                    KeyboardConnectionChanged?.Invoke(kbd);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
