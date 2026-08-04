using System.Reflection;
using NovelCsam.Helpers;

namespace NovelCsamDetection.Tests;

[TestClass]
public class AzureSqlHelperTests
{
    [TestMethod]
    public void BuildConnectionString_UsesConfiguredConnectionString_WhenPresent()
    {
        using var scope = new EnvironmentVariableScope();
        const string configured = "Server=tcp:example.database.windows.net,1433;Database=demo;Authentication=Active Directory Managed Identity;";

        Environment.SetEnvironmentVariable("AZURE_SQL_CONNECTION_STRING", configured);
        Environment.SetEnvironmentVariable("SQL_SERVER", "ignored.database.windows.net");
        Environment.SetEnvironmentVariable("SQL_DATABASE", "ignored");

        var actual = GetConnectionStringFromHelper();

        Assert.AreEqual(configured, actual);
    }

    [TestMethod]
    public void BuildConnectionString_BuildsManagedIdentityConnection_WhenServerAndDatabaseProvided()
    {
        using var scope = new EnvironmentVariableScope();

        Environment.SetEnvironmentVariable("AZURE_SQL_CONNECTION_STRING", string.Empty);
        Environment.SetEnvironmentVariable("SQL_SERVER", "myserver.database.windows.net");
        Environment.SetEnvironmentVariable("SQL_DATABASE", "mydb");
        Environment.SetEnvironmentVariable("SQL_MANAGED_IDENTITY_CLIENT_ID", string.Empty);

        var actual = GetConnectionStringFromHelper();

        StringAssert.Contains(actual, "Data Source=myserver.database.windows.net", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(actual, "Initial Catalog=mydb", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(actual, "Authentication=ActiveDirectoryManagedIdentity", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(actual, "Encrypt=True", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void BuildConnectionString_SetsUserId_WhenClientIdProvided()
    {
        using var scope = new EnvironmentVariableScope();

        Environment.SetEnvironmentVariable("AZURE_SQL_CONNECTION_STRING", string.Empty);
        Environment.SetEnvironmentVariable("SQL_SERVER", "myserver.database.windows.net");
        Environment.SetEnvironmentVariable("SQL_DATABASE", "mydb");
        Environment.SetEnvironmentVariable("SQL_MANAGED_IDENTITY_CLIENT_ID", "11111111-1111-1111-1111-111111111111");

        var actual = GetConnectionStringFromHelper();

        StringAssert.Contains(actual, "User ID=11111111-1111-1111-1111-111111111111", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetConnectionStringFromHelper()
    {
        var helper = new AzureSQLHelper();
        var field = typeof(AzureSQLHelper).GetField("_connectionString", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(field);

        return field.GetValue(helper) as string ?? string.Empty;
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _before = new(StringComparer.OrdinalIgnoreCase);
        private readonly string[] _keys =
        [
            "AZURE_SQL_CONNECTION_STRING",
            "SQL_SERVER",
            "SQL_DATABASE",
            "SQL_MANAGED_IDENTITY_CLIENT_ID"
        ];

        public EnvironmentVariableScope()
        {
            foreach (var key in _keys)
            {
                _before[key] = Environment.GetEnvironmentVariable(key);
            }
        }

        public void Dispose()
        {
            foreach (var key in _keys)
            {
                Environment.SetEnvironmentVariable(key, _before[key]);
            }
        }
    }
}
