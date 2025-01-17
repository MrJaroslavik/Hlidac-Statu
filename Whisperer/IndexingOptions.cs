using Lucene.Net.Analysis;

namespace Whisperer;

public class IndexingOptions<T> where T : IEquatable<T>
{
    public Func<T, string> TextSelector { get; init; }
    public Func<T, float>? BoostSelector { get; init; } = null;
    public Func<T, string>? FilterSelector { get; init; } = null;
    
}