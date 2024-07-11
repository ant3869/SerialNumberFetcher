using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SerialNumberRetrieval
{
    public class SerialNumberFetcher
    {
        private readonly int _maxConcurrentTasks;
        private readonly int _commandTimeout;
        private readonly int _maxRetries;

        public SerialNumberFetcher(int maxConcurrentTasks = 10, int commandTimeout = 30000, int maxRetries = 3)
        {
            _maxConcurrentTasks = maxConcurrentTasks;
            _commandTimeout = commandTimeout;
            _maxRetries = maxRetries;
        }

        public async Task FetchSerialNumbersAsync<T>(IEnumerable<T> computers, 
            Func<T, string> getNameFunc, 
            Action<T, string> setSerialNumberFunc)
        {
            var throttler = new SemaphoreSlim(_maxConcurrentTasks);

            var tasks = computers.Select(async computer =>
            {
                await throttler.WaitAsync();
                try
                {
                    await FetchSerialNumberWithRetryAsync(computer, getNameFunc, setSerialNumberFunc);
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task FetchSerialNumberWithRetryAsync<T>(T computer, 
            Func<T, string> getNameFunc, 
            Action<T, string> setSerialNumberFunc)
        {
            string computerName = getNameFunc(computer);

            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    string serialNumber = await GetSerialNumberAsync(computerName);
                    setSerialNumberFunc(computer, serialNumber);
                    return;
                }
                catch (Exception ex) when (attempt < _maxRetries)
                {
                    Logger.Instance.Log($"Attempt {attempt} failed to get serial number for {computerName}: {ex.Message}", LogLevel.Warning);
                    await Task.Delay(1000 * attempt); // Exponential backoff
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"All attempts failed to get serial number for {computerName}: {ex.Message}", LogLevel.Error);
                    setSerialNumberFunc(computer, "Error");
                }
            }
        }

        private async Task<string> GetSerialNumberAsync(string computerName)
        {
            string command = $"psexec \\\\{computerName} cmd /c \"wmic bios get serialnumber\"";
            string output = await ExecuteCommandAsync(command);
            return ParseSerialNumber(output);
        }

        private async Task<string> ExecuteCommandAsync(string command)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(_commandTimeout)) == Task.Delay(_commandTimeout))
            {
                process.Kill();
                throw new TimeoutException($"Command execution timed out after {_commandTimeout}ms");
            }

            if (process.ExitCode != 0)
            {
                throw new Exception($"Command failed with exit code {process.ExitCode}. Error: {errorBuilder}");
            }

            return outputBuilder.ToString();
        }

        private string ParseSerialNumber(string output)
        {
            var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 1 ? lines[1].Trim() : "Unknown";
        }
    }

    // Assuming you have these defined elsewhere
    public static class Logger
    {
        public static Logger Instance { get; } = new Logger();
        public void Log(string message, LogLevel level) { /* Implementation */ }
    }

    public enum LogLevel
    {
        Information,
        Warning,
        Error
    }
}
