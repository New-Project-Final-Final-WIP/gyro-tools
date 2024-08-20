using System.Collections.Concurrent;
using ImageMagick;



namespace PCDToPNG;


/// <summary>
/// Class to handle the optimization step/action in the image manipulation pipeline
/// </summary>
public static class OptimizationHelper
{
    const long BYTE_TO_MEGABYTE = 1048576;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="inputPath"> Input path to optimize</param>
    /// <param name="parallelism"> Degree of paralellism by which to optimize</param>
    /// <returns></returns>
    public static async Task OptimizeDirectory(string inputPath, int parallelism = 0)
    {
        var validExtensions = new[] {".png", ".jpg", ".jpeg", ".gif", ".ico"};
        var optimizeableFiles =
            Directory.EnumerateFiles(inputPath, "*.*", SearchOption.AllDirectories)
                .Where(p => validExtensions.Any(ext => p.ToLower().EndsWith(ext)))
                    .Select(f => new FileInfo(f))
                    .ToArray();

        Console.WriteLine("Optimizing images...");
        (FileInfo[] optimized, long totalSaved) = await OptimizeImage(parallelism, optimizeableFiles);

    }

    /// <summary>
    /// Handles running and tracking Image Optimization with Magick.NET
    /// Ain't a perfect library but we gotta save where we can
    /// </summary>
    /// <param name="parallelism"> Number of cores to dedicate to this task</param>
    /// <param name="imageFiles"> Array of FileInfo objects that contain references to all files we plan to optimize</param>
    /// <returns></returns>
    public static async Task<(FileInfo[], long bytesSaved)> OptimizeImage(int parallelism = 0, params FileInfo[] imageFiles)
    {
        var parallelQuery = imageFiles.AsParallel();
        ConcurrentBag<FileInfo> bag = [];
        
        
        if (parallelism > 0)
            parallelQuery = parallelQuery.WithDegreeOfParallelism(parallelism);

        
        long totalSpaceSaved = 0;

        await Task.Run(() =>
        {
            parallelQuery.ForAll(file =>
            {
                if (file.Exists)
                {
                    long preOptimizedSize = file.Length;
                    var opt = new ImageOptimizer()
                    {
                        OptimalCompression = true,
                    };

                    // check if the format is supported
                    if (!opt.IsSupported(file))
                    {
                        Console.WriteLine($"Extension '{file.Extension}' is not supported");
                        return;
                    }

                    opt.LosslessCompress(file);
                    FileInfo fileInfo = new(file.FullName);

                    long sizeDifference = preOptimizedSize - fileInfo.Length;
                    float percentDecrease = (float)sizeDifference / preOptimizedSize * 100;
                    float megabyteDifference = BytesToMegabytes(sizeDifference);
                                    
                    Console.WriteLine($"Optimized '{file}'");
                    Console.WriteLine($"Saved {megabyteDifference:f2} MB ({percentDecrease:f2}%)");

                    Interlocked.Add(ref totalSpaceSaved, sizeDifference);
                }

                bag.Add(file);
            });
        });
        
        Console.WriteLine($"Saved {BytesToMegabytes(totalSpaceSaved)} MB");
        return ([.. bag], totalSpaceSaved);
    }


    /// <summary>
    /// Converts bytes into megabytes
    /// </summary>
    /// <param name="bytes">Number of byte</param>
    /// <returns></returns>
    public static float BytesToMegabytes(long bytes) => (float)bytes / BYTE_TO_MEGABYTE;
}