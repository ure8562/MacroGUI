using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MacroGUI.ViewModels;

namespace MacroGUI.Services
{
    /// <summary>
    /// "연결되면 1회 초기 동기화" + "업데이트 버튼 동기화" 담당.
    /// 실제 Pi 명령/JSON은 FetchSnapshotAsync()만 채우면 됨.
    /// </summary>
    public sealed class PiSyncService
    {
        private const string MacrosDir = "/opt/bong_macro";
        private const string MacrosPath = "/opt/bong_macro/macros.json";
        private const string MacrosTmpPath = "/opt/bong_macro/macros.json.tmp";

        private readonly SshCommandService _ssh;
        private bool _initialized;

        public string LastMessage { get; private set; } = "";
        public ObservableCollection<MacroVM> Macros { get; } = new ObservableCollection<MacroVM>();

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
        {// 파일 읽기
            SshCommandResult result = await _ssh.RunAsync($"cat {MacrosPath}", 3000, ct);

            if (!result.Success)
            {
                LastMessage = $"PI ERR({result.ExitCode}): {result.StdErr}";
                return;
            }

            // JSON -> VM
            List<MacroVM> list;
            try
            {
                list = MacroJsonCodec.DeserializeMacros(result.StdOut);
            }
            catch (Exception ex)
            {
                LastMessage = $"JSON PARSE ERR: {ex.Message}";
                return;
            }

            // 컬렉션 갱신 (WPF 컨텍스트에서 호출된다는 전제)
            Macros.Clear();
            foreach (MacroVM m in list)
                Macros.Add(m);

            LastMessage = $"Loaded: {Macros.Count} macros";
        }

        public async Task SaveMacrosAsync(CancellationToken ct)
        {
            string json;
            try
            {
                json = MacroJsonCodec.SerializeMacrosJson(Macros);
            }
            catch (Exception ex)
            {
                LastMessage = $"SERIALIZE ERR: {ex.Message}";
                return;
            }

            // 안전 저장: tmp에 쓰고 mv로 교체
            string remoteCmd = $"mkdir -p {MacrosDir} && cat > {MacrosTmpPath} && mv {MacrosTmpPath} {MacrosPath}";

            // json 크기 여유로 timeout 잡기 (기본 5초)
            int timeoutMs = 5000;

            SshCommandResult result = await _ssh.RunWithInputAsync(remoteCmd, json, timeoutMs, ct);

            if (!result.Success)
            {
                LastMessage = $"SAVE ERR({result.ExitCode}): {result.StdErr}";
                return;
            }

            LastMessage = $"Saved: {Macros.Count} macros";
        }


        public void ResetInitFlag()
        {
            // 연결 끊겼다 다시 붙을 때, 다시 InitOnce 하려면 필요
            _initialized = false;
        }
    }
}
