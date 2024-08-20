using System.Text;

namespace PCDToPNG;




/// <summary>
/// Image info for a given PPM file
/// </summary>
public struct PPMImageInfo(int width, int height, int bitDepth)
{
    /// <summary>
    /// Width of the PPM image
    /// </summary>
    public int Width = width;

    /// <summary>
    /// Height of the PPM image
    /// </summary>
    public int Height = height;

    /// <summary>
    /// Maximum bit-depth of the PPM image
    /// </summary>
    public int BitDepth = bitDepth;


    //

    /// <summary>
    /// Tries to parse a PPM image's metadata
    /// </summary>
    /// <param name="stream">The stream to parse from</param>
    /// <param name="info">The resulting info, or default if parsing fails</param>
    /// <returns>Whether parsing succeeded</returns>
    public static bool TryParse(Stream stream, out PPMImageInfo info)
    {
        // StringReader buffers and thus consumes more stream than needed, so the following ugly for loops are necessary
        StringBuilder sb = new();

        // Read the file header
        for (;;)
        {
            int read = stream.ReadByte();

            if ((char)read == '\n')
                break;
            
            sb.Append((char)read);
        }

        // If not P6, then just die
        if (sb.ToString() != "P6")
        {
            Program.ProgramLog.LogWarn("This ain't a ppm file DUMMY");
            info = default;
            return false;
        }


        string? widthString;
        string? heightString;
        string? depthString;


        // Read width/height
        sb.Clear();
        for (;;)
        {
            int read = stream.ReadByte();

            if ((char)read == '\n')
                break;
            
            sb.Append((char)read);
        }

        // Split the width/height in the string
        string[]? dimensions = sb.ToString().Split(' ');
        widthString = dimensions?[0];
        heightString = dimensions?[1];


        // Read the bit depth
        sb.Clear();
        for (;;)
        {
            int read = stream.ReadByte();

            if ((char)read == '\n')
                break;
            
            sb.Append((char)read);
        }
        
        depthString = sb.ToString();
        
        info = new(
            int.Parse(widthString ?? ""),
            int.Parse(heightString ?? ""),
            int.Parse(depthString ?? ""));
        
        return true;
    }
}