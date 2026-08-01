namespace FluentRoslyn.UnitTests.Colliding;

/// <summary>
/// Deliberately named to collide with System.Threading.Tasks.Task, so the using-directive
/// tests can exercise the ambiguous-simple-name path. Kept in its own namespace (and file)
/// so it does not shadow the real Task for the rest of the suite.
/// </summary>
public class Task;
