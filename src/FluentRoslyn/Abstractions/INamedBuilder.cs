namespace FluentRoslyn.Abstractions;

/// <summary>A builder for a named C# construct.</summary>
public interface INamedBuilder
{
    /// <summary>The construct's name as it will be emitted.</summary>
    string Name { get; }
}
