using System.Collections;
using Microsoft.Extensions.Configuration;
using Snail.MCP.Memory.Infrastructure.Configuration;

namespace Snail.MCP.Memory.Tests.Infrastructure;

/// A variable name is a public contract — it is what a user writes into the MCP client config —
/// so the mapping onto option properties is pinned here rather than left to the binder.
public sealed class EnvironmentSettingsTests
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

        Assert.Throws<InvalidOperationException>(options.ResolveConnectionString);
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

        Assert.False(options.ResolveEmbeddingEnabled());
        Assert.Equal(Path.Combine(ServerPaths.Home, "models/custom.onnx"), options.ResolveEmbeddingModelPath());
        Assert.Equal(512, options.ResolveEmbeddingDimensions());
        Assert.Equal(0.9, options.ResolveDuplicateThreshold());
    }

    /// The embedding defaults are what a fresh machine runs with; each resolver's fallback is
    /// pinned so a renamed constant cannot silently change first-run behavior.
    [Fact]
    public void EmbeddingKeys_DefaultToTheShippedModel()
    {
        var options = Bind(new Hashtable { ["SNAIL_MCP_MEMORY_DIRECTORY"] = "/tmp/memory" });

        Assert.True(options.ResolveEmbeddingEnabled());
        Assert.Equal(StorageOptions.DefaultEmbeddingModelUrl, options.ResolveEmbeddingModelUrl());
        Assert.Equal(StorageOptions.DefaultEmbeddingTokenizerUrl, options.ResolveEmbeddingTokenizerUrl());
        Assert.Equal(StorageOptions.DefaultEmbeddingDimensions, options.ResolveEmbeddingDimensions());
        Assert.Equal(StorageOptions.DefaultDuplicateThreshold, options.ResolveDuplicateThreshold());
        Assert.Equal(Path.Combine("/tmp/memory", StorageOptions.ModelsDirectoryName, "model.onnx"),
            options.ResolveEmbeddingModelPath());
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
        var read = EnvironmentSettings.Read(new Hashtable
        {
            ["SNAIL_MCP_FIGMA_DIRECTORY"] = "/tmp/figma",
            ["SNAIL_MCP_MEMORY_CONFIG"] = "/tmp/config.json",
            ["PATH"] = "/usr/bin",
            ["SNAIL_MCP_MEMORY_DIRECTORY"] = "/tmp/memory",
        });

        Assert.Equal([new KeyValuePair<string, string?>("DIRECTORY", "/tmp/memory")], read);
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

        Assert.Equal(Path.Combine(ServerPaths.Home, "memory"), options.ResolveDirectory());
    }

    private static StorageOptions Bind(IDictionary variables) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(EnvironmentSettings.Read(variables))
            .Build()
            .Get<StorageOptions>() ?? new StorageOptions();
}
