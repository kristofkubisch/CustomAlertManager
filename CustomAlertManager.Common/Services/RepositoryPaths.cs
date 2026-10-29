namespace CustomAlertManager.Common.Services;

public static class RepositoryPaths
{
    // The logs folder sits next to the solution file, wherever the application was started from.
    public static string LogDirectory => Path.Combine(FindRepositoryRoot(), "logs");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var current = directory; current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "CustomAlertManager.slnx")))
            {
                return current.FullName;
            }
        }

        return AppContext.BaseDirectory;
    }
}
