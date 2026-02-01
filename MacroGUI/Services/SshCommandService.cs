using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MacroGUI.Services
{
    public sealed class SshCommandService
    {
        public string Host { get; }
        public string User { get; }

        public SshCommandService(string host, string user)
        {
            Host = host;
            User = user;
        }

        public async Task<SshCommandResult> RunAsync(string remoteCommand, int timeoutMs, CancellationToken token)
        {
            // Windows에 OpenSSH(ssh.exe) 설치되어 있어야 함.
            string args =
                "-o BatchMode=yes " +
                "-o ConnectTimeout=1 " +
                "-o StrictHostKeyChecking=accept-new " +
                $"{User}@{Host} \"{remoteCommand}\"";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "ssh",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using Process p = new Process { StartInfo = psi };

                if (!p.Start())
                    return SshCommandResult.Fail(-1, "", "ssh start failed");

                Task<string> outTask = p.StandardOutput.ReadToEndAsync();
                Task<string> errTask = p.StandardError.ReadToEndAsync();

                Task waitTask = p.WaitForExitAsync(token);
                Task timeoutTask = Task.Delay(timeoutMs, token);

                Task done = await Task.WhenAny(waitTask, timeoutTask);
                if (done != waitTask)
                {
                    try { p.Kill(true); } catch { }
                    return SshCommandResult.Fail(-2, "", "timeout");
                }

                string stdout = await outTask;
                string stderr = await errTask;

                if (p.ExitCode == 0)
                    return SshCommandResult.Ok(stdout, stderr);

                return SshCommandResult.Fail(p.ExitCode, stdout, stderr);
            }
            catch (OperationCanceledException)
            {
                return SshCommandResult.Fail(-3, "", "canceled");
            }
            catch (Exception ex)
            {
                return SshCommandResult.Fail(-9, "", ex.Message);
            }
        }

        public async Task<SshCommandResult> RunWithInputAsync(string remoteCommand, string stdinText, int timeoutMs, CancellationToken token)
        {
            string safeCmd = EscapeForDoubleQuotes(remoteCommand);

            string args =
                "-o BatchMode=yes " +
                "-o ConnectTimeout=1 " +
                "-o StrictHostKeyChecking=accept-new " +
                $"{User}@{Host} \"{safeCmd}\"";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "ssh",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                StandardInputEncoding = Encoding.UTF8
            };

            try
            {
                using Process p = new Process { StartInfo = psi };

                if (!p.Start())
                    return SshCommandResult.Fail(-1, "", "ssh start failed");

                // stdin 전송
                if (!string.IsNullOrEmpty(stdinText))
                {
                    await p.StandardInput.WriteAsync(stdinText);
                }
                p.StandardInput.Close();

                Task<string> outTask = p.StandardOutput.ReadToEndAsync();
                Task<string> errTask = p.StandardError.ReadToEndAsync();

                Task waitTask = p.WaitForExitAsync(token);
                Task timeoutTask = Task.Delay(timeoutMs, token);

                Task done = await Task.WhenAny(waitTask, timeoutTask);
                if (done != waitTask)
                {
                    try { p.Kill(true); } catch { }
                    return SshCommandResult.Fail(-2, "", "timeout");
                }

                string stdout = await outTask;
                string stderr = await errTask;

                if (p.ExitCode == 0)
                    return SshCommandResult.Ok(stdout, stderr);

                return SshCommandResult.Fail(p.ExitCode, stdout, stderr);
            }
            catch (OperationCanceledException)
            {
                return SshCommandResult.Fail(-3, "", "canceled");
            }
            catch (Exception ex)
            {
                return SshCommandResult.Fail(-9, "", ex.Message);
            }
        }

        private static string EscapeForDoubleQuotes(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    public sealed class SshCommandResult
    {
        public bool Success { get; }
        public int ExitCode { get; }
        public string StdOut { get; }
        public string StdErr { get; }

        private SshCommandResult(bool success, int exitCode, string stdout, string stderr)
        {
            Success = success;
            ExitCode = exitCode;
            StdOut = stdout ?? "";
            StdErr = stderr ?? "";
        }

        public static SshCommandResult Ok(string stdout, string stderr)
        {
            return new SshCommandResult(true, 0, stdout, stderr);
        }

        public static SshCommandResult Fail(int exitCode, string stdout, string stderr)
        {
            return new SshCommandResult(false, exitCode, stdout, stderr);
        }
    }
}
