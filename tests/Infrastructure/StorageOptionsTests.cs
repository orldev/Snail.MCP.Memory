using System.Collections;
using Microsoft.Extensions.Configuration;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.MCP.Memory.Tests.TestDoubles;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Memory.Tests.Infrastructure;

/// A variable name is a public contract — it is what a user writes into the MCP client config —
/// so the mapping onto option properties is pinned here rather than left to the binder.
public sealed class StorageOptionsTests
{
    [Fact]
    public void PrefixedVariables_BindOntoStorageOptions()
    {
        var options = Bind(new Hashtable
        {
            ["SNAIL_MCP_MEMORY_PROVIDER"] = "Postgres",
            ["SNAIL_MCP_MEMORY_DIRECTORY"] = " /tmp/memory ",
            ["SNAIL_MCP_MEMORY_CONNECTION_STRING"] = "Host=db;Database=memory",
            ["SNAIL_MCP_MEMORY_WORKSPACE"] = " Backend ",
            ["SNAIL_MCP_MEMORY_IDLE_TIMEOUT_MINUTES"] = "15",
        });

        Assert.Equal(Provider.Postgres, options.Provider);
        Assert.Equal("/tmp/memory", options.Directory);
        Assert.Equal("Host=db;Database=memory", options.ResolveConnectionString());
        Assert.Equal("backend", options.ResolveWorkspace());
        Assert.Equal(TimeSpan.FromMinutes(15), options.ResolveIdleTimeout());
    }

    /// A server database cannot be guessed from a data directory; a missing connection string
    /// must fail loudly at startup, not half-work.
    [Fact]
    public void Postgres_RequiresAConnectionString()
    {
        var options = new StorageOptions { Provider = Provider.Postgres };

        var refusal = Assert.Throws<StartRefusedException>(options.ResolveConnectionString);

        Assert.StartsWith("SNAIL_MCP_MEMORY_CONNECTION_STRING (or ConnectionString in the settings file) is empty", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Workspace_DefaultsToTheWholeSharedBase()
    {
        Assert.Equal("", Bind(new Hashtable()).ResolveWorkspace());
    }

    [Fact]
    public void IdleTimeout_DefaultsToAnHourAndIgnoresNonPositiveValues()
    {
        Assert.Equal(TimeSpan.FromHours(1), Bind(new Hashtable()).ResolveIdleTimeout());
        Assert.Equal(TimeSpan.FromHours(1), Bind(new Hashtable { ["SNAIL_MCP_MEMORY_IDLE_TIMEOUT_MINUTES"] = "0" }).ResolveIdleTimeout());
    }

    [Fact]
    public void PrefixedVariables_BindOntoTheEmbeddingKeys()
    {
        var options = Bind(new Hashtable
        {
            ["SNAIL_MCP_MEMORY_EMBEDDING_ENABLED"] = "false",
            ["SNAIL_MCP_MEMORY_EMBEDDING_MODEL_PATH"] = "~/models/custom.onnx",
            ["SNAIL_MCP_MEMORY_EMBEDDING_DIMENSIONS"] = "512",
            ["SNAIL_MCP_MEMORY_DUPLICATE_THRESHOLD"] = "0.9",
        });

        Assert.False(options.IsEmbeddingEnabled());
        Assert.Equal(Path.Combine(ServerFolders.Home, "models/custom.onnx"), options.ResolveEmbeddingModelPath());
        Assert.Equal(512, options.ResolveEmbeddingDimensions());
        Assert.Equal(0.9, options.ResolveDuplicateThreshold());
    }

    /// The embedding defaults are what a fresh machine runs with; each resolver's fallback is
    /// pinned so a renamed constant cannot silently change first-run behavior.
    [Fact]
    public void EmbeddingKeys_DefaultToTheShippedModel()
    {
        var options = Bind(new Hashtable { ["SNAIL_MCP_MEMORY_DIRECTORY"] = "/tmp/memory" });

        Assert.True(options.IsEmbeddingEnabled());
        Assert.Equal(StorageOptions.DefaultEmbeddingModelUrl, options.ResolveEmbeddingModelUrl());
        Assert.Equal(StorageOptions.DefaultEmbeddingTokenizerUrl, options.ResolveEmbeddingTokenizerUrl());
        Assert.Equal(StorageOptions.DefaultEmbeddingDimensions, options.ResolveEmbeddingDimensions());
        Assert.Equal(TimeSpan.FromMinutes(StorageOptions.DefaultEmbeddingIdleMinutes), options.ResolveEmbeddingIdlePeriod());
        Assert.Equal(StorageOptions.DefaultDuplicateThreshold, options.ResolveDuplicateThreshold());
        Assert.Equal(Path.Combine("/tmp/memory", StorageOptions.ModelsDirectoryName, "model.onnx"),
            options.ResolveEmbeddingModelPath());
    }

    /// An explicit 0 keeps the model for good, and a negative value is no setting at all.
    [Theory]
    [InlineData("1", 1)]
    [InlineData("0", 0)]
    [InlineData("-5", 30)]
    public void EmbeddingIdleMinutes_ResolveToTheIdlePeriod(string value, int minutes)
    {
        var options = Bind(new Hashtable { ["SNAIL_MCP_MEMORY_EMBEDDING_IDLE_MINUTES"] = value });

        Assert.Equal(TimeSpan.FromMinutes(minutes), options.ResolveEmbeddingIdlePeriod());
    }

    [Fact]
    public void EmbeddingIdleMinutes_ComesFromTheSettingsFile()
    {
        var file = WriteSettings("""{ "EmbeddingIdleMinutes": 0 }""");

        try
        {
            Assert.Equal(TimeSpan.Zero, StorageOptions.Load(file, new Hashtable()).ResolveEmbeddingIdlePeriod());
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// A bare path is a convenience; a full connection string passes through untouched, and an
    /// empty value lands on the default database file in the data directory.
    [Theory]
    [InlineData("", "Data Source={dir}/memory.db")]
    [InlineData("/tmp/other.db", "Data Source=/tmp/other.db")]
    [InlineData("Data Source=/x.db;Mode=ReadOnly", "Data Source=/x.db;Mode=ReadOnly")]
    public void ConnectionString_ResolvesBarePathsAndPassesFullStringsThrough(string value, string expected)
    {
        var options = new StorageOptions { Directory = "/tmp/memory", ConnectionString = value };

        Assert.Equal(expected.Replace("{dir}", "/tmp/memory"), options.ResolveConnectionString());
    }

    [Fact]
    public void VariablesOfOtherServersAndTheConfigPath_AreIgnored()
    {
        var options = Bind(new Hashtable
        {
            ["SNAIL_MCP_FIGMA_DIRECTORY"] = "/tmp/figma",
            ["SNAIL_MCP_MEMORY_CONFIG"] = "/tmp/config.json",
            ["PATH"] = "/usr/bin",
            ["SNAIL_MCP_MEMORY_WORKSPACE"] = "backend",
        });

        Assert.Null(options.Directory);
        Assert.Equal("backend", options.ResolveWorkspace());
    }

    /// The settings file is where a machine's settings live; a key written there reaches the options as its variable would.
    [Fact]
    public void SettingsFile_ReachesTheOptions()
    {
        var file = WriteSettings("""{ "Directory": "/tmp/from-file", "IdleTimeoutMinutes": 20 }""");

        try
        {
            var options = StorageOptions.Load(file, new Hashtable());

            Assert.Equal("/tmp/from-file", options.Directory);
            Assert.Equal(TimeSpan.FromMinutes(20), options.ResolveIdleTimeout());
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// A variable set for one start overrides the file for that start only, and the rest of the file still applies.
    [Fact]
    public void Variable_WinsOverTheSettingsFile()
    {
        var file = WriteSettings("""{ "Directory": "/tmp/from-file", "IdleTimeoutMinutes": 20 }""");

        try
        {
            var options = StorageOptions.Load(file, new Hashtable { ["SNAIL_MCP_MEMORY_DIRECTORY"] = "/tmp/from-variable" });

            Assert.Equal("/tmp/from-variable", options.Directory);
            Assert.Equal(TimeSpan.FromMinutes(20), options.ResolveIdleTimeout());
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// Underscores inside a segment are dropped and '__' becomes a path separator; the resulting
    /// key stays upper-case, which the binder does not care about.
    [Theory]
    [InlineData("EMBEDDING_MODEL_PATH", "EMBEDDINGMODELPATH")]
    [InlineData("STORAGE__CONNECTION_STRING", "STORAGE:CONNECTIONSTRING")]
    public void SegmentSeparator_BecomesAConfigurationPath(string variable, string key) =>
        Assert.Equal(key, EnvironmentSettings.ToConfigurationKey(variable));

    [Fact]
    public void HomeShortcut_IsExpandedForTheDataDirectory()
    {
        var options = Bind(new Hashtable { ["SNAIL_MCP_MEMORY_DIRECTORY"] = "~/memory" });

        Assert.Equal(Path.Combine(ServerFolders.Home, "memory"), options.ResolveDirectory());
    }

    /// No directory is the server's own data folder, ~/.s3l-mcp-memory, and a relative one is made absolute.
    [Fact]
    public void DataDirectory_Unset_IsTheServersDataFolder()
    {
        Assert.Equal(ServerFolders.DataDirectory(StorageOptions.ServerName), Bind(new Hashtable()).ResolveDirectory());
        Assert.Equal(ServerFolders.DataDirectory(StorageOptions.ServerName), Bind(new Hashtable { ["SNAIL_MCP_MEMORY_DIRECTORY"] = "  " }).ResolveDirectory());
        Assert.Equal(Path.GetFullPath("memory"), Bind(new Hashtable { ["SNAIL_MCP_MEMORY_DIRECTORY"] = "memory" }).ResolveDirectory());
    }

    /// Stdio unless the settings say Http or name a port; the shared server's address is loopback port 9230 unless Url says otherwise.
    [Fact]
    public void Endpoint_FromTheSameSettings_IsStdioOrTheConfiguredHttpAddress()
    {
        var unset = McpEndpoint.Read(Settings(new Hashtable()), StorageOptions.DefaultUrl, StorageOptions.EndpointOptions);
        var http = McpEndpoint.Read(
            Settings(new Hashtable { ["SNAIL_MCP_MEMORY_TRANSPORT"] = "Http", ["SNAIL_MCP_MEMORY_URL"] = "http://127.0.0.1:9231/" }),
            StorageOptions.DefaultUrl,
            StorageOptions.EndpointOptions);

        Assert.Equal(McpTransport.Stdio, unset.Transport);
        Assert.Equal(new Uri("http://127.0.0.1:9230"), unset.Url);
        Assert.Equal(McpTransport.Http, http.Transport);
        Assert.Equal(9231, http.Url.Port);
        Assert.False(http.ListensOnEveryInterface);
    }

    /// PostgreSQL without a connection string was refused while the services were registered, outside the refusal of
    /// the start, and crashed with a trace and exit code 134; it names the variable in one line and exits with 1.
    [Fact]
    public async Task Program_PostgresWithoutAConnectionString_ExitsNamingTheVariable()
    {
        var exited = await RefusedStart.RunAsync(new Dictionary<string, string?> { ["SNAIL_MCP_MEMORY_PROVIDER"] = "Postgres" });

        Assert.Equal(1, exited.Code);
        Assert.StartsWith("SNAIL_MCP_MEMORY_CONNECTION_STRING (or ConnectionString in the settings file) is empty", exited.Stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", exited.Stderr, StringComparison.Ordinal);
    }

    /// The program bound a word for a number before its host existed and crashed with the binder's trace and exit
    /// code 134; it names the variable in one line and exits with 1.
    [Fact]
    public async Task Program_SettingOfTheWrongType_ExitsNamingTheVariable()
    {
        var exited = await RefusedStart.RunAsync(new Dictionary<string, string?> { ["SNAIL_MCP_MEMORY_IDLE_TIMEOUT_MINUTES"] = "abc" });

        Assert.Equal(1, exited.Code);
        Assert.Equal(
            "SNAIL_MCP_MEMORY_IDLE_TIMEOUT_MINUTES (or IdleTimeoutMinutes in the settings file) is 'abc'; it is not a number (Int32).",
            exited.Stderr);
        Assert.DoesNotContain("   at ", exited.Stderr, StringComparison.Ordinal);
    }

    private static StorageOptions Bind(IDictionary variables) => StorageOptions.Load(file: null, variables);

    private static IConfiguration Settings(IDictionary variables) =>
        new ConfigurationBuilder().AddServerSettings(StorageOptions.ServerName, file: null, variables).Build();

    private static string WriteSettings(string json)
    {
        var file = Path.Combine(TestFolder.Create(), "settings.json");

        File.WriteAllText(file, json);

        return file;
    }
}
