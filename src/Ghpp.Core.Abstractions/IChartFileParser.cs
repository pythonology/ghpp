using System.IO;

namespace Ghpp.Core.Abstractions
{
    /// <summary>
    /// Parses a <see cref="Chart"/> from a file.
    /// </summary>
    public interface IChartFileParser
    {
        Chart Parse(string path);
        Chart Parse(TextReader reader);
    }
}
