namespace PCDToPNG;

/// <summary>
/// Class to handle the optimization step/action in the image manipulation pipeline
/// </summary>
public static class FileHelper
{
    /// <summary>
    /// Finds all files of a single specified type by extension
    /// </summary>
    /// <param name="inputPath">The path to search for files (recursively)</param>
    /// <param name="extensions">The extension(s) to search for</param>
    /// <returns></returns>
    public static FileInfo[] FindFilesByExtension(string inputPath, params string[] extensions)
    {
        FileInfo[] files =
            Directory.EnumerateFiles(inputPath, "*.*", SearchOption.AllDirectories)
                .Where(p =>
                    extensions.Any(ext =>
                        p.ToLower().EndsWith(ext.ToLower())))
                .Select(f => new FileInfo(f))
                .ToArray();
        return files;
    }

    public static void WriteFile(string outputPath, string outputFileName, bool preserveDirectory = false)
    {
        
    }

}