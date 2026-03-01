using DbUp;
using System.Reflection;

namespace Creaturedex.Data;

public static class MigrationRunner
{
    public static bool Run(string connectionString)
    {
        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();
        return result.Successful;
    }
}
