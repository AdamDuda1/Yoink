using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Yoink_Downloader_Services
{
    /// <summary>
    /// Straight ffmpeg calls for files that are already on disk - convert and/or trim.
    /// No progress percentage: ffmpeg only reports elapsed time, and turning that into a
    /// percentage means parsing the source duration first. The page shows a busy bar and
    /// the last output line instead, which is enough to see it working.
    /// </summary>
    public class FfmpegService
    {
        public static async Task<int> ConvertAsync(
            string inputFile,
            string outputFile,
            string? trimStart = null,
            string? trimEnd = null,
            bool copyStreams = false,
            IProgress<string>? log = null)
        {
            if (!File.Exists(inputFile))
            {
                throw new FileNotFoundException("Input file not found.", inputFile);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

            using var process = new Process();
            var startInfo = process.StartInfo;

            startInfo.FileName = "ffmpeg.exe";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-y");           // overwrite without asking
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(inputFile);

            // Placed after -i so both times are measured from the start of the file.
            if (!string.IsNullOrWhiteSpace(trimStart))
            {
                startInfo.ArgumentList.Add("-ss");
                startInfo.ArgumentList.Add(trimStart.Trim());
            }

            if (!string.IsNullOrWhiteSpace(trimEnd))
            {
                startInfo.ArgumentList.Add("-to");
                startInfo.ArgumentList.Add(trimEnd.Trim());
            }

            if (copyStreams)
            {
                // Fast, but cuts land on the nearest keyframe, so a trim can start a
                // moment early. Uncheck it when the cut has to be exact.
                startInfo.ArgumentList.Add("-c");
                startInfo.ArgumentList.Add("copy");
            }

            startInfo.ArgumentList.Add(outputFile);

            // ffmpeg writes everything - including progress - to stderr, not stdout.
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    log?.Report(e.Data);
                }
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
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
