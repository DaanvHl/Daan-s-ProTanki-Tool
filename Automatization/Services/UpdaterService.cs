using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Automatization.Services
{
    public class UpdaterService
    {
        private const string Owner = "Gabriel250903";
        private const string Repo = "Automatization";

        private readonly HttpClient _httpClient;

        public UpdaterService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "C# App");
        }

        public Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version ?? new Version("0.0.0");
        }

        public async Task<(Version?, string?)> GetLatestVersionAsync()
        {
            string url = $"https://api.github.com/repos/{Owner}/{Repo}/releases";
            string response = await _httpClient.GetStringAsync(url);
            if (string.IsNullOrEmpty(response))
            {
                return (null, null);
            }

            using JsonDocument jsonDoc = JsonDocument.Parse(response);
            var releases = jsonDoc.RootElement.EnumerateArray()
                .Select(r => new
                {
                    TagName = r.GetProperty("tag_name").GetString(),
                    ReleaseNotes = r.GetProperty("body").GetString()
                })
                .Where(r => r.TagName != null)
                .Select(r => new
                {
                    Version = new Version(r.TagName!.TrimStart('v', '.')),
                    r.ReleaseNotes
                })
                .OrderByDescending(r => r.Version)
                .FirstOrDefault();

            return (releases?.Version, releases?.ReleaseNotes);
        }

        public async Task<(bool, string?)> DownloadAndInstallUpdateAsync(Action<long, long> progressChanged)
        {
            LogService.LogInfo("DownloadAndInstallUpdateAsync started.");

            string url = $"https://api.github.com/repos/{Owner}/{Repo}/releases";
            string response = await _httpClient.GetStringAsync(url);

            using (JsonDocument jsonDoc = JsonDocument.Parse(response))
            {
                var latestRelease = jsonDoc.RootElement.EnumerateArray()
                    .Select(r => new
                    {
                        Json = r,
                        Version = new Version(r.GetProperty("tag_name").GetString()!.TrimStart('v', '.'))
                    })
                    .OrderByDescending(r => r.Version)
                    .FirstOrDefault();

                if (latestRelease != null)
                {
                    JsonElement assets = latestRelease.Json.GetProperty("assets");

                    foreach (JsonElement asset in assets.EnumerateArray())
                    {
                        if (asset.GetProperty("name").GetString() == "Automatization.Installer.msi")
                        {
                            string? downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            if (downloadUrl == null)
                            {
                                LogService.LogError("Download URL not found.");
                                return (false, null);
                            }

                            string targetDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TankAutomation");
                            if (!Directory.Exists(targetDirectory))
                            {
                                _ = Directory.CreateDirectory(targetDirectory);
                            }

                            string tempPath = Path.Combine(targetDirectory, "Automatization.Installer.msi");
                            LogService.LogInfo($"Downloading installer from {downloadUrl} to {tempPath}");

                            using (Stream downloadStream = await _httpClient.GetStreamAsync(downloadUrl))

                            using (FileStream fileStream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                long totalBytes = asset.GetProperty("size").GetInt64();
                                byte[] buffer = new byte[8192];
                                int bytesRead = 0;
                                long totalBytesRead = 0L;
                                while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    totalBytesRead += bytesRead;
                                    progressChanged(totalBytesRead, totalBytes);
                                }
                            }

                            LogService.LogInfo("Download finished.");

                            bool uninstalled = await UninstallCurrentVersionAsync();
                            if (uninstalled)
                            {
                                LogService.LogInfo("Previous version uninstalled. Starting new installer.");
                                _ = Process.Start("msiexec.exe", $"/i \"{tempPath}\" /quiet");
                                return (true, tempPath);
                            }
                            else
                            {
                                LogService.LogInfo("Previous version not found. Manual installation required.");
                                return (false, tempPath);
                            }
                        }
                    }
                }
            }

            LogService.LogInfo("No new version found.");
            return (false, null);
        }

        private async Task<bool> UninstallCurrentVersionAsync()
        {
            LogService.LogInfo("UninstallCurrentVersionAsync started.");
            string productDisplayName = "Automatization";
            string? uninstallString = GetUninstallString(productDisplayName);

            if (!string.IsNullOrEmpty(uninstallString))
            {
                try
                {
                    ProcessStartInfo processStartInfo = new();
                    if (uninstallString.Contains("msiexec.exe", StringComparison.CurrentCultureIgnoreCase))
                    {
                        string arguments = uninstallString[uninstallString.IndexOf("/i", StringComparison.CurrentCultureIgnoreCase)..].Replace("/i", "/x").Replace("/I", "/X");
                        processStartInfo.FileName = "msiexec.exe";
                        processStartInfo.Arguments = $"{arguments} /quiet";
                    }
                    else
                    {
                        processStartInfo.FileName = "msiexec.exe";
                        processStartInfo.Arguments = $"/x \"{uninstallString}\" /quiet";
                    }

                    LogService.LogInfo($"Starting uninstaller with command: {processStartInfo.FileName} {processStartInfo.Arguments}");

                    Process? process = Process.Start(processStartInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        LogService.LogInfo("Uninstaller process finished.");
                    }
                    else
                    {
                        LogService.LogError("Failed to start uninstaller process.");
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    LogService.LogError($"Error uninstalling: {ex.Message}");
                    return false;
                }
            }
            else
            {
                LogService.LogInfo("Uninstaller not found in registry. The application may not be installed.");
                return false;
            }
        }

        private string? GetUninstallString(string productDisplayName)
        {
            string uninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            using RegistryKey? uninstallKey = Registry.LocalMachine.OpenSubKey(uninstallKeyPath);

            if (uninstallKey != null)
            {
                foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                {
                    using RegistryKey? subKey = uninstallKey.OpenSubKey(subKeyName);

                    if (subKey != null)
                    {
                        if (subKey.GetValue("DisplayName") is string displayName && displayName.Equals(productDisplayName, StringComparison.OrdinalIgnoreCase))
                        {
                            return subKey.GetValue("UninstallString") as string;
                        }

                    }
                }

            }
            return null;
        }

        public void RelaunchApplication()
        {
            Assembly? entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                _ = Process.Start(entryAssembly.Location);
            }

            System.Windows.Application.Current?.Shutdown();
        }
    }
}
