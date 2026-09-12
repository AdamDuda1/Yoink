using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Yoink_Downloader.Services
{
    public static class WingetService
    {
        public const string YtDlpPackage = "yt-dlp.yt-dlp";
        public const string FfmpegPackage = "yt-dlp.FFmpeg";
        public const string Aria2Package = "aria2.aria2";

        public static async Task<int> InstallAsync(string packageId, IProgress<string>? log = null)
        {
            using var process = new Process();
            var startInfo = process.StartInfo;

            startInfo.FileName = "winget.exe";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            startInfo.ArgumentList.Add("install");
            startInfo.ArgumentList.Add("--id");
            startInfo.ArgumentList.Add(packageId);
            startInfo.ArgumentList.Add("--exact");
            startInfo.ArgumentList.Add("--source");
            startInfo.ArgumentList.Add("winget");
            startInfo.ArgumentList.Add("--accept-package-agreements");
            startInfo.ArgumentList.Add("--accept-source-agreements");
            startInfo.ArgumentList.Add("--disable-interactivity");

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    log?.Report(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    log?.Report(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            process.WaitForExit();

            return process.ExitCode;
        }
    }
}
