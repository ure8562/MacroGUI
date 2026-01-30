using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MacroGUI.Services
{
    /// <summary>
    /// "연결되면 1회 초기 동기화" + "업데이트 버튼 동기화" 담당.
    /// 실제 Pi 명령/JSON은 FetchSnapshotAsync()만 채우면 됨.
    /// </summary>
    public sealed class PiSyncService
    {
        private readonly SshCommandService _ssh;
        private bool _initialized;

        public string LastMessage { get; private set; } = "";
        public PiSyncService(SshCommandService ssh)
        {
            _ssh = ssh;
        }

        public async Task<bool> InitOnceAsync(CancellationToken token)
        {
            if (_initialized)
                return false;

            await FetchSnapshotAsync(token);
            _initialized = true;
            return true;
        }

        public Task RefreshAsync(CancellationToken token)
        {
            return FetchSnapshotAsync(token);
        }

        public async Task FetchSnapshotAsync(CancellationToken ct)
        {
            var result = await _ssh.RunAsync("echo ok", 1500, ct);

            if (result.Success)
                LastMessage = $"PI OK: {result.StdOut.Trim()}";
            else
                LastMessage = $"PI ERR({result.ExitCode}): {result.StdErr}";
        }

        public void ResetInitFlag()
        {
            // 연결 끊겼다 다시 붙을 때, 다시 InitOnce 하려면 필요
            _initialized = false;
        }
    }
}
