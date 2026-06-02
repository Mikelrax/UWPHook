using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace UWPHook
{
    /// <summary>
    /// Functions related to Windows PowerShell
    /// </summary>
    static class ScriptManager
    {
        // Always invoke Windows PowerShell 5.1 (powershell.exe) explicitly.
        // The Appx module that backs Get-AppxPackage is not supported on
        // PowerShell 7 (pwsh.exe), so we must not let the host default to it
        // when it is installed on the machine.
        private const string PowerShellExecutable = "powershell.exe";

        /// <summary>
        /// Runs the given PowerShell script text through Windows PowerShell 5.1
        /// and returns its formatted output as a single string.
        /// </summary>
        public static string RunScript(string scriptText)
        {
            // The embedded scripts are passed to powershell.exe -File. We append
            // | Out-String so the output matches what the previous Runspace-based
            // implementation produced via pipeline.Commands.Add("Out-String").
            string tempScript = Path.Combine(
                Path.GetTempPath(),
                $"UWPHook_{Guid.NewGuid():N}.ps1");

            try
            {
                // Write with a UTF-8 BOM so PowerShell 5.1 parses the file as
                // UTF-8 even when the host code page is not UTF-8. The trailing
                // | Out-String must live on the same line as the script's last
                // expression: a script that ends with ';' would otherwise leave
                // an empty pipe element on the next line and PowerShell 5.1
                // raises EmptyPipeElement.
                string tail = scriptText.TrimEnd(';', ' ', '\t', '\r', '\n');
                File.WriteAllText(tempScript, tail + " | Out-String", new UTF8Encoding(true));

                var startInfo = new ProcessStartInfo
                {
                    FileName = PowerShellExecutable,
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{tempScript}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                using (var process = Process.Start(startInfo))
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
                    {
                        throw new InvalidOperationException(
                            $"PowerShell exited with code {process.ExitCode}: {stderr}");
                    }

                    return stdout;
                }
            }
            finally
            {
                if (File.Exists(tempScript))
                {
                    try { File.Delete(tempScript); }
                    catch { /* best-effort cleanup */ }
                }
            }
        }
    }
}
