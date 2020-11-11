using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;

namespace Video_Maker
{
#pragma warning disable CS414 //For FFMPEG_Command because it thinks it is not used
    using static Console;

    class Program
    {
        private const string ErrorFormat = "Error fixing {0}: {1} - {2}";
        private const string PNG = ".png";
        private const string PATHERROR = "Path does not exist!";
        private static int VW = 1920;
        private static int VH = 1080;
        private static readonly string OUTPUT_PATH = "output.mp4";
        private static readonly string OUTPUT2_PATH = "output_fixed.mp4";
        private static readonly string OUTPUT3_PATH = "output_final.mp4";
        private static readonly string AUDIO_PREFIX = "audio";
        private static readonly string CONC_PATH = "conc.txt";
        private static readonly string FFMPEG_Command = $"-safe 0 -f concat -i {CONC_PATH} -pix_fmt yuv420p -y -vsync vfr {OUTPUT_PATH}";
        private static readonly string FFMPEG_PATH = "ffmpeg.exe";
        private static Process FFMPEG;
        private static readonly string IMG_PATH = "img\\";
        private static readonly string SHUFF_PATH = "shuffled\\";

        [STAThread]
        static void Main(string[] args)
        {
            Clean();
            string folder = null;
            folder = GetFolder();
            if (folder == null)
            {
                WriteLine("Folder was null or you pressed Cancel or Escape! Exiting!");
                Read();
                Environment.Exit(-1);
            }
            WriteLine("Number of pictures: " + GetNPictures(folder));
            if (GetNPictures(folder) <= 0)
            {
                WriteLine("Unknown error...");
                Read();
                Environment.Exit(-1);
            }
            string AudioPrefix = null;
            decimal SecondsPerPicture = decimal.MinusOne;
            Title = "Get FPS";
            GetFPS(folder, out SecondsPerPicture, out AudioPrefix);
            Title = "Init External Tools";
            InitExternalTools(SecondsPerPicture);
            Title = "Shuffle Files";
            ShuffleFiles(folder);
            Title = "Convert Images";
            ConvertImages(SHUFF_PATH);
            Title = "Get Best Resolution";
            GetBestResolution(SHUFF_PATH);
            Title = "Fix Aspect";
            FixAspect(SHUFF_PATH);
            Title = "Rename Files";
            RenameFiles(SHUFF_PATH);
            Title = "Get Concat File";
            GetConcatFile(SecondsPerPicture);
            Title = "Run FFMPEG";
            RunFFMPEG(AudioPrefix, SecondsPerPicture);
            Title = "Video Maker";
            string vname = AudioPrefix == null ? "output_fixed.mp4" : "output_final.mp4";
            Title = "Video Maker is done!";
            WriteLine($"The video file is called: {vname}");
            Read();
        }

        private static void GetBestResolution(string folder)
        {
            WriteLine("Finding Best Resolution!");
            Title = "Get Best Resolution: Finding average resolution";
            List<Tuple<int, int>> resolutions = new List<Tuple<int, int>>();
            IEnumerable<string> files = Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
            int max = files.Count();
            for (int i = 0; i < 2; i++)
            {
                int n = 0;
                foreach (string path in files)
                {
                    try
                    {
                        if (i == 0)
                        {
                            Title = $"Getting average resolution: {n}/{max} files checked";
                            using (Bitmap b = new Bitmap(path))
                            {
                                if (b.Width > b.Height) //ignore portrait photos
                                {
                                    resolutions.Add(new Tuple<int, int>(b.Width, b.Height));
                                }
                                b.Dispose();
                            }
                        }
                        else
                        {
                            Title = $"Scaling images {n}/{max}";
                            Bitmap b = ScaleImage(new Bitmap(path), VW, VH);
                            SaveBitmapSafe(b, path + "2");
                            b.Dispose();
                        }
                    }
                    catch (Exception e)
                    {
                        WriteLine("Error with file {0}: {1} - {2}", path, e.GetType(), e.Message);
                    }
                    n++;
                }
                if (i == 0)
                {
                    int avgX = 0;
                    int avgY = 0;
                    foreach (Tuple<int, int> t in resolutions)
                    {
                        avgX += t.Item1;
                        avgY += t.Item2;
                    }
                    avgX /= resolutions.Count;
                    avgY /= resolutions.Count;
                    resolutions.Clear();
                    WriteLine($"Average resoultion: {avgX}x{avgY}");
                    VW = avgX;
                    VH = avgY;
                    Title = "Get Best Resolution: Converting to 16 by 9";
                    WriteLine("Converting to 16:9!");
                    double aspect = -1;
                    while (Math.Round(aspect) != VH && VW % 2 != 0)
                    {
                        VW = aspect < VH ? VH + 1 : VW - 1;
                        aspect = VW / 16d * 9d;
                        VH = (int)Math.Round(aspect);
                    }
                    if (VW > 1920)
                    {
                        while (VW > 1920)
                        {
                            VW -= 16;
                            VH -= 9;
                        }
                    }
                    Title = "Get Best Resolution: Making resolution divisible by 2";
                    WriteLine(Title);
                    while (VW % 2 != 0)
                        VW++;
                    while (VH % 2 != 0)
                        VH++;
                    WriteLine($"\n\nNew resolution: {VW}x{VH}");
                }
            }
            files = null;
        }

        private static void FixAspect(string folder)
        {
            WriteLine("Fixing aspect ratios!");
            Title = "Fix Aspect: init";
            int n = 0;
            IEnumerable<string> files = Directory.EnumerateFiles(folder, "*.png2", SearchOption.TopDirectoryOnly);
            int max = files.Count();
            Title = $"Fix Aspect: {n}/{max}";
            foreach (string path in files)
            {
                Title = $"Fix Aspect: {n}/{max}";
                try
                {
                    Bitmap b = ScaleImage(new Bitmap(path), VW, VH);
                    SaveBitmapSafe(b, path + "3");
                    b.Dispose();
                    n++;
                }
                catch (Exception e)
                {
                    max--;
                    WriteLine(ErrorFormat, path, e.GetType(), e.Message);
                }
            }
            files = null;
        }

        private static void GetConcatFile(decimal FPS)
        {
            using (StreamWriter sw = new StreamWriter(CONC_PATH))
            {
                string last = string.Empty;
                foreach (string path in Directory.EnumerateFiles(IMG_PATH + "\\", "*.png", SearchOption.TopDirectoryOnly))
                {
                    sw.WriteLine("file " + path + "\nduration " + FPS);
                    last = path;
                }
                sw.WriteLine("file " + last); //FFMPEG bug, have to put last one again
                sw.Flush();
                sw.Close();
                last = null;
            }
        }

        private static void ConvertImages(string folder)
        {
            int max = GetNPictures(SHUFF_PATH);
            int n = 0;
            foreach (string path in Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly).Where(s => (s.EndsWith("png") == false)))
            {
                Title = $"Converted {n}/{max} pictures!";
                try
                {
                    using (Bitmap b = new Bitmap(path))
                    {
                        b.Save(Path.GetFullPath(path).Split('.')[0] + PNG, ImageFormat.Png);
                        b.Dispose();
                    }
                }
                catch (Exception e)
                {
                    WriteLine("Could not convert {0}: {1} - {2}", path, e.GetType(), e.Message);
                    max--;
                    continue;
                }
                WriteLine("Converted {0}", path);
                n++;
            }
            Title = "Video Maker";
        }

        private static void GetFPS(string folder, out decimal FPS, out string AudioPrefix)
        {
            AudioPrefix = null;
            FPS = decimal.MinusOne;
            while (FPS <= decimal.Zero)
            {
                for (int i = 1; i <= 5; i++)
                {
                    WriteLine("{0} min = {1} sec", i, i * 60);
                }
                WriteLine("Enter time of video (in seconds) or enter \"music\" to sync it to an audio file");
                string input = ReadLine();
                if (decimal.TryParse(input, out FPS) == false)
                {
                    if (input == "music")
                    {
                        input = string.Empty;
                        while (!File.Exists(input))
                        {
                            WriteLine("Enter file path:");
                            input = ReadLine();
                            if (File.Exists(input))
                            {
                                WriteLine("Found file! It is a {0} file!", GetMimeFromFile(input));//                      MP3
                                if (GetMimeFromFile(input).StartsWith("audio/") || GetMimeFromFile(input) == "application/octet-stream")
                                {
                                    WriteLine("Found audio file! Remuxing to WAV then MP3");
                                    if (!File.Exists(FFMPEG_PATH))
                                    {
                                        WriteLine("FFMPEG not found! :(");
                                        Read();
                                        Environment.Exit(-1);
                                    }
                                    FFMPEG = new Process();
                                    for (int i = 0; i < 2; i++)
                                    {
                                        ProcessStartInfo psi = new ProcessStartInfo
                                        {
                                            FileName = FFMPEG_PATH,
                                            UseShellExecute = false,
                                            RedirectStandardInput = true,
                                            Arguments = $"-y -i \"{input}\" {AUDIO_PREFIX}" + (i == 0 ? ".wav" : ".mp3")
                                        };
                                        FFMPEG.StartInfo = psi;
                                        FFMPEG.Start();
                                        FFMPEG.WaitForExit();
                                    }
                                    WriteLine("Done!");
                                    int sec = GetAudioDuration(AUDIO_PREFIX + ".wav");
                                    WriteLine("Audio is {0} seconds long!", sec);
                                    FPS = sec;
                                    FPS /= GetNPictures(folder);
                                    WriteLine("FPS: {0}", FPS);
                                    AudioPrefix = AUDIO_PREFIX;
                                    return;
                                }
                                else
                                {
                                    input = string.Empty;
                                    WriteLine("File is not an audio file!");
                                }
                            }
                        }
                    }
                    WriteLine("Please enter a number! (It can be a decimal like 2.5)");
                    continue;
                }
                FPS /= GetNPictures(folder);
            }
        }

        private static void RunFFMPEG(string AudioPrefix, decimal FPS)
        {
            WriteLine("Running FFMPEG...");
            Title = "Run FFMPEG: Concat frames";
            WriteLine("Command: ffmpeg {0}", FFMPEG.StartInfo.Arguments);
            FFMPEG.Start();
            FFMPEG.WaitForExit();
            Title = "Run FFMPEG: Convert to 30 FPS";
            WriteLine("\n\n\n\nFixing FPS\n\n\n\n");
            FFMPEG.StartInfo.Arguments = $"-i {OUTPUT_PATH} -r 30 -c:a aac -y {OUTPUT2_PATH}";
            FFMPEG.Start();
            FFMPEG.WaitForExit();
            if (AudioPrefix != null)
            {
                Title = "Add audio";
                WriteLine("\n\n\n\nAdding Audio\n\n\n\n");
                FFMPEG.StartInfo.Arguments = $"-y -i {OUTPUT2_PATH} -i \"{AudioPrefix}.mp3\" -pix_fmt yuv420p -c:a aac -map 0:v:0 -map 1:a:0 {OUTPUT3_PATH}";
                WriteLine("Command: ffmpeg {0}", FFMPEG.StartInfo.Arguments);
                FFMPEG.Start();
                FFMPEG.WaitForExit();
            }
            FFMPEG = null;
            WriteLine("\n\nDone!\n\n");
        }

        private static int GetNPictures(string folder)
        {
            if (!Directory.Exists(folder))
            {
                WriteLine(PATHERROR);
                return -1;
            }
            return Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly).Count();
        }

        private static void Clean()
        {
            WriteLine("Cleaning!");
            foreach (string f in new string[] { CONC_PATH, OUTPUT_PATH, OUTPUT2_PATH, OUTPUT3_PATH, AUDIO_PREFIX + ".wav", AUDIO_PREFIX + ".mp3" })
            {
                if (File.Exists(f))
                {
                    File.Delete(f);
                }
            }
            foreach (string f in new string[] { IMG_PATH, SHUFF_PATH })
            {
                if (!Directory.Exists(f))
                {
                    WriteLine("{0} does not exist!", f);
                    continue;
                }
                foreach (string path in Directory.EnumerateFiles(f, "*.*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch
                    {
                        WriteLine("Could not remove {0}", path);
                    }
                }
            }
            WriteLine("Done!");
        }

        private static void RenameFiles(string folder)
        {
            if (!Directory.Exists(folder))
            {
                WriteLine(PATHERROR);
                return;
            }
            if (!Directory.Exists(IMG_PATH))
            {
                Directory.CreateDirectory(IMG_PATH);
            }
            int n = 1;
            foreach (string path in Directory.EnumerateFiles(folder, "*.png2", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Copy(path, IMG_PATH + n + PNG);
                }
                catch (Exception e)
                {
                    WriteLine("Could not copy {0}: {1} - {2}", path, e.GetType(), e.Message);
                }
                n++;
            }

        }

        private static void ShuffleFiles(string folder)
        {
            if (!Directory.Exists(folder))
            {
                WriteLine(PATHERROR);
                return;
            }
            if (!Directory.Exists(SHUFF_PATH))
            {
                Directory.CreateDirectory(SHUFF_PATH);
            }
            List<int> used = new List<int>();
            int done = 0;
            int nfiles = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly).Where(s => s.EndsWith(PNG) || s.EndsWith(".jpg")).ToList().Count;
            foreach (string file in Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly).Where(s => s.EndsWith(PNG) || s.EndsWith(".jpg")))
            {
                string name = Path.GetFileName(file);
                int nfilename = 0;
                do
                {
                    nfilename = new Random(DateTime.Now.Millisecond * DateTime.Now.Second).Next(0, int.MaxValue);
                }
                while (used.Contains(nfilename));
                File.Copy(file, SHUFF_PATH + nfilename + Path.GetExtension(file), true);
                used.Add(nfilename);
                Title = "Shuffled " + done + "/" + nfiles + " files!";
            }
        }

        private static string GetFolder()
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog
            {
                Description = "Select a folder of images to make a video of!"
            };
            DialogResult result = DialogResult.Retry;
            while (result != DialogResult.OK && result != DialogResult.Cancel)
            {
                result = fbd.ShowDialog();
            }
            if (result == DialogResult.Cancel)
            {
                WriteLine("Exiting...");
                return null;
            }
            string s = fbd.SelectedPath;
            fbd.Dispose();
            return s;
        }

        private static void InitExternalTools(decimal FPS)
        {
            WriteLine("FFMPEG init");
            if (!File.Exists(FFMPEG_PATH))
            {
                WriteLine("FFMPEG not found! :(");
                Read();
                Environment.Exit(-1);
            }
            FFMPEG = new Process();
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = FFMPEG_PATH,
                UseShellExecute = false,
                RedirectStandardInput = true,
                Arguments = FFMPEG_Command.Replace("{0}", (1 / FPS).ToString())
            };
            FFMPEG.StartInfo = psi;
            WriteLine("Done!");
        }

        private static void MakeBinaryFile(string path, byte[] bytes)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write(bytes);
                    bw.Flush();
                    bw.Dispose();
                }
                fs.Flush();
                fs.Dispose();
            }
        }

        //from https://efundies.com/scale-an-image-in-c-sharp-preserving-aspect-ratio/
        //i modified it though
        public static Bitmap ScaleImage(Bitmap bmp, int maxWidth, int maxHeight)
        {
            var ratioX = (double)maxWidth / bmp.Width;
            var ratioY = (double)maxHeight / bmp.Height;
            var ratio = Math.Min(ratioX, ratioY);
            WriteLine($"Ratio: {ratio}");
            var newWidth = (int)(bmp.Width * ratio);
            var newHeight = (int)(bmp.Height * ratio);
            var newImage = new Bitmap(VW, VH, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(newImage))
            {
                graphics.DrawRectangle(new Pen(new SolidBrush(Color.Black)), new Rectangle(0, 0, VW, VH));
                graphics.DrawImage(bmp, (VW / 2) - (newWidth / 2), (VH / 2) - (newHeight / 2), newWidth, newHeight);
                graphics.Flush();
                graphics.Dispose();
            }
            return newImage;
        }

        private static int GetAudioDuration(string path)
        {
            try
            {
                return GetSoundLength(path) / 1000;
            }
            catch (Exception e)
            {
                WriteLine("{0}: {1}", e.GetType(), e.Message);
                return -1;
            }
        }

        //from https://stackoverflow.com/questions/82319/how-can-i-determine-the-length-i-e-duration-of-a-wav-file-in-c
        [DllImport("winmm.dll")]
        private static extern uint mciSendString(
            string command,
            StringBuilder returnValue,
            int returnLength,
            IntPtr winHandle);

        public static int GetSoundLength(string fileName)
        {
            StringBuilder lengthBuf = new StringBuilder(32);

            mciSendString(string.Format("open \"{0}\" type waveaudio alias wave", fileName), null, 0, IntPtr.Zero);
            mciSendString("status wave length", lengthBuf, lengthBuf.Capacity, IntPtr.Zero);
            mciSendString("close wave", null, 0, IntPtr.Zero);

            int length = 0;
            int.TryParse(lengthBuf.ToString(), out length);

            return length;
        }

        internal static void SaveBitmapSafe(Bitmap image, string path)
        {
            //from https://stackoverflow.com/questions/15862810/a-generic-error-occurred-in-gdi-in-bitmap-save-method
            //modified
            using (MemoryStream mem = new MemoryStream())
            {
                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
                {
                    image.Save(mem, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] bytes = mem.ToArray();
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush();
                    fs.Dispose();
                }
                mem.Flush();
                mem.Dispose();
            }
        }

        //from https://www.daniweb.com/programming/software-development/threads/265236/how-to-check-if-a-sound-file-is-a-wav-file-in-c
        [DllImport(@"urlmon.dll", CharSet = CharSet.Auto)]
        private extern static System.UInt32 FindMimeFromData(
            System.UInt32 pBC,
            [MarshalAs(UnmanagedType.LPStr)] System.String pwzUrl,
            [MarshalAs(UnmanagedType.LPArray)] byte[] pBuffer,
            System.UInt32 cbSize,
            [MarshalAs(UnmanagedType.LPStr)] System.String pwzMimeProposed,
            System.UInt32 dwMimeFlags,
            out System.UInt32 ppwzMimeOut,
            System.UInt32 dwReserverd
        );
        private static string GetMimeFromFile(string filename)
        {
            try
            {
                if (!File.Exists(filename))
                    throw new FileNotFoundException(filename + " not found");
                byte[] buffer = new byte[256];
                using (FileStream fs = new FileStream(filename, FileMode.Open))
                {
                    if (fs.Length >= 256)
                        fs.Read(buffer, 0, 256);
                    else
                        fs.Read(buffer, 0, (int)fs.Length);
                }
                FindMimeFromData(0, null, buffer, 256, null, 0, out uint mimetype, 0);
                IntPtr mimeTypePtr = new IntPtr(mimetype);
                string mime = Marshal.PtrToStringUni(mimeTypePtr);
                Marshal.FreeCoTaskMem(mimeTypePtr);
                return mime;
            }
            catch
            {
                return "unknown/unknown";
            }
        }
    }
}
