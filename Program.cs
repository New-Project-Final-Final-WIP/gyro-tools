

using System.CommandLine;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using ImageMagick;
using ImageMagick.Formats;
using ImageMagick.ImageOptimizers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Microsoft.Extensions.Logging;

namespace PCDToPNG;

/// <summary>
/// Main program
/// </summary>
public class Program
{
    const string DEFAULT_INPUT = "./input";
    const string DEFAULT_OUTPUT = "./output";
    const string LOG_PATH = "./Output.log";
    
    /// <summary>
    /// 
    /// </summary>
    public static readonly string WorkOutput = Path.GetFullPath("./work");

    /// <summary>
    /// 
    /// </summary>
    public static readonly string MissingLogPath = Path.GetFullPath("./Missing.log");

    /// <summary>
    /// 
    /// </summary>
    public static readonly string HpcdToPpmPath = Path.GetFullPath("./hpcdtoppm.exe");
    static readonly object lockObj = new();

    /// <summary>
    /// 
    /// </summary>
    public static Logger ProgramLog = new();


    public static FileStream LogStream;
    public static StreamWriter LogWriter;


    /// <summary>
    /// Main entrypoint
    /// </summary>
    /// <param name="args">Command-line args</param>
    /// <returns>Integer</returns>
    public static async Task<int> Main(string[] args)
    {
        if (!File.Exists(LOG_PATH))
            File.Create(LOG_PATH);
        
        LogStream = File.OpenWrite(LOG_PATH);

        LogWriter = new(LogStream);



        ProgramLog.OnLog += (source, args) =>
        {
            lock (lockObj)
            {
                LogWriter.WriteLine(args.Msg);
                LogWriter.Flush();
            }
        };

        ProgramLog.OnWarn += (source, args) =>
        {
            lock (lockObj)
            {
                LogWriter.WriteLine(args.Msg);
                LogWriter.Flush();
            }
        };

        ProgramLog.OnError += (source, args) =>
        {
            lock (lockObj)
            {
                LogWriter.WriteLine(args.Msg);
                LogWriter.Flush();
            }
        };



        RootCommand root = new("Converts one or more PCD files into PNG");


        Argument<string> inputPath = new(
            "inputPath",
            () => DEFAULT_INPUT,
            "Determines the path to the input file(s)");

        root.AddArgument(inputPath);


        Argument<string> outputPath = new(
            "outputPath",
            () => DEFAULT_OUTPUT,
            "Determines the path to the output file(s)");

        root.AddArgument(outputPath);


        Option<bool> folder = new(
            ["--folder", "-f"],
            () => false,
            "When specified, denotes that the input and output paths are to folders rather than files");

        root.AddOption(folder);

        Option<bool> bmp = new(
            "-bmp",
            () => false,
            "When specified, will output bmp files instead");

        root.AddOption(bmp);

        Option<bool> noOptimize = new(
            "-no",
            () => false,
            "When specified, will not run optimization");

        root.AddOption(noOptimize);


        Option<int> parallelism = new(
            ["--parallelism", "-p"],
            () => 0,
            "Specifies the number of cores to utilize for batch processing (0 = all cores)");

        root.AddOption(parallelism);
        
        Option<int> mode = new(
            ["--mode"],
            () => 0,
            "Specifies which mode the program should operate in.\n0 - PCD -> PNG \n 1 - Optimize directory (.gif, .png, .jpg, .ico)");

        root.AddOption(mode);

        root.SetHandler(Execute, inputPath, outputPath, folder, bmp, noOptimize, parallelism, mode);

        return await root.InvokeAsync(args);
    }


    /// <summary>
    /// Executes the main functionality based on command line flags
    /// </summary>
    /// <param name="inputPath">The input path to operate on</param>
    /// <param name="outputPath">The output path to write to</param>
    /// <param name="folder">Whether the paths are to be parsed as folders or files</param>
    /// <param name="bmp">When true, outputs files as bmp instead of png</param>
    /// <param name="noOptimize">When true, does not run Magick.Net Image Optimization on png export</param>
    /// <param name="parallelism">When set higher than 0, specifies the number of cores to use for batch processing</param>
    /// <param name="mode">Controls program operating mode, replace with something better later</param>
    public static async Task Execute(string inputPath, string outputPath, bool folder, bool bmp = false, bool noOptimize = false, int parallelism = 0, int mode = 0)
    {
        // Create dirs if they don't exist
        if (!Directory.Exists(DEFAULT_OUTPUT))
            Directory.CreateDirectory(DEFAULT_OUTPUT);

        if (!Directory.Exists(DEFAULT_INPUT))
            Directory.CreateDirectory(DEFAULT_INPUT);

        if (!Directory.Exists(WorkOutput))
            Directory.CreateDirectory(WorkOutput);

        // Get full paths just in case
        string fullInput = Path.GetFullPath(inputPath);
        string fullOutput = Path.GetFullPath(outputPath);
        string hpcdPath = HpcdToPpmPath;


        //TODO: Rewrite loop to work with single input versus requiring two seperate functions?
        switch(mode)
        {
            // PCD -> PNG conversion
            case 0:
                if (!File.Exists(hpcdPath))
                    hpcdPath = Path.Combine(Path.GetDirectoryName(hpcdPath)!, Path.GetFileNameWithoutExtension(hpcdPath));
            
                if (!File.Exists(hpcdPath))
                    ProgramLog.LogInfo($"Failed to find {hpcdPath}! Did you place it next to this executable?");
                
                
            await ProcessPCDsToPNGs(fullInput, fullOutput, bmp, parallelism);

            break;
            // Optimize folder
            case 1:
                if (folder)
                    await OptimizationHelper.OptimizeDirectory(fullInput, parallelism);
                else
                    ProgramLog.LogWarn("Nope, too lazy");
            break;
        }

        LogStream.Dispose();
        
    }

    //// does not handle overriding files

    /// <summary>
    /// Processes a folder of PPM files into PNG images
    /// </summary>
    /// <param name="inputPath">The input folder path</param>
    /// <param name="outputPath">The output folder path</param>
    /// <param name="bmp">Whether to save the output as bmp</param>
    /// <param name="parallelism">When set higher than 0, specifies the number of cores to use for batch processing</param>
    public static async Task ProcessPCDsToPNGs(string inputPath, string outputPath, bool bmp = false, int parallelism = 0)
    {
        var inputAttributes = File.GetAttributes(inputPath);
        var outputAttributes = File.GetAttributes(outputPath);

        bool inputIsDir = (inputAttributes & FileAttributes.Directory) == FileAttributes.Directory;
        FileInfo[] pcdFiles;
        
        if (inputIsDir)
        {
            pcdFiles = FileHelper.FindFilesByExtension(inputPath, ".pcd");
        }
        else
        {
            pcdFiles = [new FileInfo(inputPath)];
        }
        

        ProgramLog.LogInfo("Processing PCDs into PPMs and storing them temporarily in the work directory...");
        (FileInfo[] processedPCD, FileInfo[] resultingPPMs) = await PCDHelper.ProcessPCDToPPM(parallelism, pcdFiles);

        ProgramLog.LogInfo("Processing PPMs into images...");
        (FileInfo[] processedPPMs, FileInfo[] resultingImages) = await PCDHelper.ProcessPPM(outputPath, parallelism, bmp, resultingPPMs);

        ProgramLog.LogInfo("Optimizing images...");
        (FileInfo[] optimized, long totalSaved) = await OptimizationHelper.OptimizeImage(parallelism, resultingImages);

        Parallel.ForEach(resultingPPMs, file =>
        {
            File.Delete(file.FullName);
        });
    }


    /// <summary>
    /// Consumes logs from a text reader
    /// </summary>
    /// <param name="reader">Reader to consume from</param>
    /// <returns>Task that can be awaited</returns>
    public static async Task ConsumeLog(TextReader reader)
    {
        string? text;

        while ((text = await reader.ReadLineAsync()) != null)
        {
            ProgramLog.LogInfo(text);
        }
    }

    /// <summary>
    /// Consumes logs from a text reader
    /// </summary>
    /// <param name="reader">Reader to consume from</param>
    /// <returns>Task that can be awaited</returns>
    public static async Task ConsumeError(TextReader reader)
    {
        string? text;

        while ((text = await reader.ReadLineAsync()) != null)
        {
            ProgramLog.LogError(text);
        }
    }    
}




