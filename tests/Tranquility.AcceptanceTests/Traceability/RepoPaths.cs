namespace Tranquility.AcceptanceTests.Traceability;

/// <summary>Locates the repository root from the test execution directory.</summary>
public static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string SpecsDirectory => Path.Combine(Root, "docs", "specs");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "docs", "specs")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Repository root (containing docs/specs) not found above {AppContext.BaseDirectory}.");
    }
}
