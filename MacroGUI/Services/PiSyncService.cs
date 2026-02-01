using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
        private const string ActivePath = "/opt/bong_macro/macros.json";
        private const string PresetGlob = "/opt/bong_macro/macros*.json";

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

        public async Task RefreshAsync(string remotePath, CancellationToken ct)
        {
            // 기존 RefreshAsync 내부에서 쓰던 cat 대상만 remotePath로 바꿔서 실행
            SshCommandResult result = await _ssh.RunAsync($"cat {remotePath}", 3000, ct);

            if (!result.Success)
            {
                LastMessage = $"PI ERR({result.ExitCode}): {result.StdErr}";
                return;
            }

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

            // ✅ 성공했을 때만 반영(실패 시 기존 유지)
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
        public async Task<List<string>> ListPresetFilesAsync(CancellationToken ct)
        {
            // 2>/dev/null: 매칭 없을 때 ls 에러 숨김, || true: exitcode 0 유지
            string cmd = $"ls -1 {PresetGlob} 2>/dev/null || true";
            SshCommandResult result = await _ssh.RunAsync(cmd, 3000, ct);

            if (!result.Success)
            {
                LastMessage = $"LIST ERR({result.ExitCode}): {result.StdErr}";
                return new List<string>();
            }

            List<string> list = new List<string>();
            string[] lines = result.StdOut.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (string full in lines)
            {
                string t = full.Trim();
                if (t.Length <= 0)
                    continue;

                // basename만 보이게(예: macros_office.json)
                int idx = t.LastIndexOf('/');
                list.Add(idx >= 0 ? t.Substring(idx + 1) : t);
            }

            LastMessage = $"Preset list: {list.Count}";
            return list;
        }

        public async Task LoadPresetAsync(string presetFileName, CancellationToken ct)
        {
            string path = $"{MacrosDir}/{presetFileName}";

            SshCommandResult result = await _ssh.RunAsync($"cat \"{path}\"", 3000, ct);
            if (!result.Success)
            {
                LastMessage = $"LOAD ERR({result.ExitCode}): {result.StdErr}";
                return;
            }

            List<MacroVM> list;
            try
            {
                list = MacroJsonCodec.DeserializeMacros(result.StdOut);
            }
            catch (Exception ex)
            {
                LastMessage = $"LOAD PARSE ERR: {ex.Message}";
                return;
            }

            // UI 컬렉션 갱신 (WPF UI 스레드에서 호출된다는 전제)
            Macros.Clear();
            foreach (MacroVM m in list)
                Macros.Add(m);

            LastMessage = $"Loaded preset: {presetFileName} ({Macros.Count} macros)";
        }

        public async Task ApplyPresetAsync(string selectedPresetFile, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(selectedPresetFile))
            {
                LastMessage = "apply failed: empty preset";
                return;
            }

            string? name = ExtractPresetName(selectedPresetFile); // macros_ / .json 제거
            if (string.IsNullOrWhiteSpace(name))
            {
                LastMessage = $"apply failed: invalid preset file: {selectedPresetFile}";
                return;
            }

            // FIFO에 명령 쓰기 (따옴표/escape 최소)
            SshCommandResult r = await _ssh.RunWithInputAsync(
                "cat > /tmp/proxykbd_cmd",
                $"load {name}\n",
                3000,
                ct);

            if (!r.Success)
            {
                LastMessage = $"PI ERR({r.ExitCode}): {r.StdErr}";
                return;
            }

            LastMessage = $"Applied: {name}";
        }

        public async Task SavePresetAsync(string presetFileName, bool applyAlso, CancellationToken ct)
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

            string dst = $"{MacrosDir}/{presetFileName}";
            string tmp = $"{dst}.tmp";

            // tmp에 쓰고 mv로 교체
            string remoteCmd = $"mkdir -p \"{MacrosDir}\" && cat > \"{tmp}\" && mv \"{tmp}\" \"{dst}\"";
            SshCommandResult wr = await _ssh.RunWithInputAsync(remoteCmd, json, 6000, ct);

            if (!wr.Success)
            {
                LastMessage = $"SAVE ERR({wr.ExitCode}): {wr.StdErr}";
                return;
            }

            if (applyAlso)
            {
                SshCommandResult cp = await _ssh.RunAsync($"cp \"{dst}\" \"{ActivePath}\"", 3000, ct);
                if (!cp.Success)
                {
                    LastMessage = $"SAVED BUT APPLY ERR({cp.ExitCode}): {cp.StdErr}";
                    return;
                }

                LastMessage = $"Saved preset + applied: {presetFileName}";
                return;
            }

            LastMessage = $"Saved preset: {presetFileName}";
        }

        public void ResetInitFlag()
        {
            // 연결 끊겼다 다시 붙을 때, 다시 InitOnce 하려면 필요
            _initialized = false;
        }

        private static string? ExtractPresetName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            string s = Path.GetFileName(fileName);

            // ✅ default 처리
            if (string.Equals(s, "macros.json", StringComparison.OrdinalIgnoreCase))
                return "default";

            if (s.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 5);

            if (s.StartsWith("macros_", StringComparison.OrdinalIgnoreCase))
                s = s.Substring("macros_".Length);

            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        public async Task DeletePresetAsync(string presetFileName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(presetFileName))
            {
                LastMessage = "DELETE ERR: empty preset name";
                return;
            }

            // 🔒 보호 프리셋
            if (IsProtectedPreset(presetFileName))
            {
                LastMessage = $"DELETE BLOCKED: {presetFileName}";
                return;
            }

            string path = $"{MacrosDir}/{presetFileName}";

            SshCommandResult r = await _ssh.RunAsync(
                $"rm -f \"{path}\"",
                3000,
                ct
            );

            if (!r.Success)
            {
                LastMessage = $"DELETE ERR({r.ExitCode}): {r.StdErr}";
                return;
            }

            LastMessage = $"Deleted preset: {presetFileName}";
        }

        private static bool IsProtectedPreset(string fileName)
        {
            return fileName.Equals("macros_active.json", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals("macros.json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
