using System.Diagnostics;
using System.Collections.Concurrent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PCDToPNG;


/// <summary>
/// 
/// </summary>
public static class PCDHelper
{
    /// <summary>
    /// Handles multithreading conversions from PCD to PPM
    /// </summary>
    /// <param name="parallelism">How many cores to use for the process</param>
    /// <param name="pcdFiles">One to Many FileInfo objects that reference ppm files</param>
    /// <returns></returns>
    public static async Task<(FileInfo[] processedFiles, FileInfo[] resultingFiles)> ProcessPCDToPPM(int parallelism = 0, params FileInfo[] pcdFiles)
    {
        var parallelQuery = pcdFiles.AsParallel();
        ConcurrentDictionary<FileInfo, FileInfo> bag = [];
        
        
        if (parallelism > 0)
            parallelQuery = parallelQuery.WithDegreeOfParallelism(parallelism);
        

        await Task.Run(() =>
        {
            parallelQuery.ForAll(file =>
            {
                string inputFileName = Path.GetFileNameWithoutExtension(file.FullName);
                string outputFile = Path.Combine(Program.WorkOutput, inputFileName + ".ppm");
                Program.ProgramLog.LogInfo($"Converting {file} into {outputFile}");
                FileInfo outputInfo = new(outputFile);


                ProcessStartInfo info = new(Program.HpcdToPpmPath, ["-5", "-rep", file.FullName, outputFile])
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };


                Process? proc = Process.Start(info);

                if (proc == null)
                {
                    Program.ProgramLog.LogInfo("Process was null, WTF!!!!!!!!!!!!!!!");
                    return;
                }
                
                // Spin off tasks that handle the log consumption. This is due to a weird oversight in the process
                // object where it will hang forever if you WaitForExit() without sucking out the stderr and stdout
                Task.Run(() => Program.ConsumeLog(proc.StandardOutput), CancellationToken.None);
                Task.Run(() => Program.ConsumeLog(proc.StandardError), CancellationToken.None);

                proc?.WaitForExit();

                if (!File.Exists(outputFile))
                    Program.ProgramLog.LogWarn($"'{outputFile}' doesn't exist, skipping!");

                bag.TryAdd(file, outputInfo);

                Program.ProgramLog.LogInfo($"Converting {outputInfo} into a PPM");
            });
        });

        return (bag.Keys.ToArray(), bag.Values.ToArray());
    }



    /// <summary>
    /// Handles reading in all PPM files and passing them to the PPMToImageSharp function for conversion
    /// </summary>
    /// <param name="outputPath">Output path for converted pcd files</param>
    /// <param name="parallelism">How many cores to use for the process</param>
    /// <param name="bmp">Boolean to control if outputted files are .bmp vs png</param>
    /// <param name="ppmFiles">One to Many FileInfo objects that reference ppm files</param>
    /// <returns></returns>
    public static async Task<(FileInfo[] processedFiles, FileInfo[] resultingFiles)> ProcessPPM(string outputPath, int parallelism = 0, bool bmp = false, params FileInfo[] ppmFiles)
    {
        var parallelQuery = ppmFiles.AsParallel();
        ConcurrentDictionary<FileInfo, FileInfo> bag = [];
        
        
        if (parallelism > 0)
            parallelQuery = parallelQuery.WithDegreeOfParallelism(parallelism);
        
        await Task.Run(() =>
        {
            parallelQuery.ForAll(file =>
            {
                string inputFileName = Path.GetFileNameWithoutExtension(file.FullName);

                string outputFile;

                if (bmp)
                    outputFile = Path.Combine(outputPath, inputFileName + ".bmp");
                else
                    outputFile = Path.Combine(outputPath, inputFileName + ".png");

                

                if (!file.Exists)
                {
                    bag.TryAdd(file, new(outputFile));
                    return;
                }


                FileStream ppm = File.OpenRead(file.FullName);
                using Image? processed = PPMToImageSharp(ppm); // 'using' will dispose of this image at the end of the context it's used in to free resources
                if (bmp)
                    processed?.SaveAsBmp(outputFile);
                else
                    processed?.SaveAsPng(outputFile);
                
                ppm.Dispose();

                bag.TryAdd(file, new(outputFile));

                Program.ProgramLog.LogInfo($"Processed '{file}' into {(bmp ? "bmp" : "png")} format");
            });
        });

        return (bag.Keys.ToArray(), bag.Values.ToArray());
    }



    /// <summary>
    /// Processes a ppm file from a stream
    /// </summary>
    /// <param name="ppm">The stream to read the ppm file from</param>
    /// <returns>An image, or null if the PPM failed to parse</returns>
    public static Image? PPMToImageSharp(Stream ppm)
    {
        if (PPMImageInfo.TryParse(ppm, out PPMImageInfo imgInfo))
        {
            // Slurp the rest of the ppm file into memory
            using MemoryStream imgData = new();
            ppm.CopyTo(imgData);
            byte[] imgBytes = imgData.GetBuffer(); // Uses the internal buffer so no copy needs to be made


            // Make a new image from the data in memory
            Image? img;
            return imgInfo.BitDepth switch
            {
                255 => img = Image.LoadPixelData<Rgb24>(imgBytes, imgInfo.Width, imgInfo.Height),
                _ => img = Image.LoadPixelData<Rgb48>(imgBytes, imgInfo.Width, imgInfo.Height)
            };
        }
        else
        {
            return null;
        }
    }
}