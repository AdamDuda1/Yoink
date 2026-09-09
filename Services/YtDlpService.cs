using System;
using System.Diagnostics;

namespace Yoink_Downloader_Services
{
    public class YtDlpService
    {
    
        public static void tr()
        {
            try
            {
                using (Process myProcess = new Process())
                {
                    myProcess.StartInfo.UseShellExecute = false;
                    myProcess.StartInfo.WorkingDirectory = "C:\\Users\\adamd\\Downloads";
                    myProcess.StartInfo.FileName = "yt-dlp.exe";
                    myProcess.StartInfo.ArgumentList.Add("-f");
                    myProcess.StartInfo.ArgumentList.Add("bestvideo[ext=mp4]+bestaudio[ext=m4a]/best");
                    myProcess.StartInfo.ArgumentList.Add("--ffmpeg-location");
                    myProcess.StartInfo.ArgumentList.Add(@"C:\Users\adamd\Downloads\ffmpeg.exe");
                    myProcess.StartInfo.ArgumentList.Add("--verbose");
                    myProcess.StartInfo.ArgumentList.Add("https://www.youtube.com/watch?v=FxKcM7xJoOQ");

                    myProcess.StartInfo.RedirectStandardOutput = true;
                    myProcess.StartInfo.RedirectStandardError = true;
                    myProcess.StartInfo.CreateNoWindow = true;

                    myProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null) Debug.WriteLine("[OUT] " + e.Data);
                    };
                    myProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null) Debug.WriteLine("[ERR] " + e.Data);
                    };

                    myProcess.Start();
                    myProcess.BeginOutputReadLine();
                    myProcess.BeginErrorReadLine();
                    myProcess.WaitForExit();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }   
}