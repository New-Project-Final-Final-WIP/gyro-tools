﻿

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


namespace PCDToPNG;

/// <summary>
/// Main program
/// </summary>
public class Program
{
    const string DEFAULT_INPUT = "./input";
    const string DEFAULT_OUTPUT = "./output";
    
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
    /// Main entrypoint
    /// </summary>
    /// <param name="args">Command-line args</param>
    /// <returns>Integer</returns>
    public static async Task<int> Main(string[] args)
    {
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
        File.Delete(MissingLogPath);

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
                    Console.WriteLine($"Failed to find {hpcdPath}! Did you place it next to this executable?");
                
                if (folder)
                    await ProcessPCDsToPNGs(fullInput, fullOutput, bmp, parallelism);
                else
                {
                    await ProcessPCDToPNG(fullInput, fullOutput, bmp, parallelism);
                }
            break;
            // Optimize folder
            case 1:
                if (folder)
                    await OptimizationHelper.OptimizeDirectory(fullInput, parallelism);
                else
                    Console.WriteLine("Nope, too lazy");
            break;
        }
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
        var pcdFiles =
            Directory.EnumerateFiles(inputPath, "*.*", SearchOption.AllDirectories)
                .Where(p => p.ToLower()
                    .EndsWith(".pcd"))
                    .Select(f => new FileInfo(f))
                    .ToArray();
        

        Console.WriteLine("Processing PCDs into PPMs and storing them temporarily in the work directory...");
        FileInfo[] ppms = await PCDHelper.ProcessPCDToPPM(parallelism, pcdFiles);

        Console.WriteLine("Processing PPMs into images...");
        FileInfo[] images = await PCDHelper.ProcessPPM(outputPath, parallelism, bmp, ppms);

        Console.WriteLine("Optimizing images...");
        (FileInfo[] optimized, long totalSaved) = await OptimizationHelper.OptimizeImage(parallelism, images);

        Parallel.ForEach(ppms, file =>
        {
            File.Delete(file.FullName);
        });
    }



    /// <summary>
    /// Processes a single PCD file into a PNG
    /// </summary>
    /// <param name="inputPath">The input file path</param>
    /// <param name="outputPath">The output folder path</param>
    /// <param name="bmp">Whether to save the output as bmp</param>
    /// <param name="parallelism">When set higher than 0, specifies the number of cores to use for batch processing</param>
    public static async Task ProcessPCDToPNG(string inputPath, string outputPath, bool bmp = false, int parallelism = 0)
    {
        Console.WriteLine("Processing PCDs into PPMs and storing them temporarily in the work directory...");
        FileInfo[] ppms = await PCDHelper.ProcessPCDToPPM(parallelism, new FileInfo(inputPath));

        Console.WriteLine("Processing PPMs into images...");
        FileInfo[] images = await PCDHelper.ProcessPPM(outputPath, parallelism, bmp, ppms);

        Console.WriteLine("Optimizing images...");
        (FileInfo[] optimized, long totalSaved) = await OptimizationHelper.OptimizeImage(parallelism, images);

        Console.WriteLine($"Total space saved: {OptimizationHelper.BytesToMegabytes(totalSaved)}");
        Parallel.ForEach(ppms, file =>
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
            Console.WriteLine(text);
        }
    }



    /// <summary>
    /// Appends text to Missing.log
    /// </summary>
    /// <param name="text">Text to append</param>
    public static void AppendMissing(string text)
    {
        Console.WriteLine($"Writing {text} to Missing.log!");
        
        if (!File.Exists(MissingLogPath))
            File.Create(MissingLogPath).Dispose();
        
        File.AppendAllText(MissingLogPath, text + '\n');
    }

    
}



