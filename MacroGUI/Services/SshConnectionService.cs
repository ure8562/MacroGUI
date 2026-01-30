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
            // BatchMode=yes : 비밀번호/프롬프트 없이 실패 처리
            // ConnectTimeout=1 : 1초 내 연결 안되면 실패
            // StrictHostKeyChecking=accept-new : 최초 연결 시 known_hosts 자동 등록
            string args =
                $"-o BatchMode=yes " +
                $"-o ConnectTimeout=1 " +
                $"-o StrictHostKeyChecking=accept-new " +
                $"{User}@{Host} \"echo ok\"";

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

                Task waitTask = p.WaitForExitAsync(token);
                Task timeoutTask = Task.Delay(1500, token);

                Task finished = await Task.WhenAny(waitTask, timeoutTask);
                if (finished != waitTask)
                {
                    try { p.Kill(true); } catch { }
                    return false;
                }

                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
