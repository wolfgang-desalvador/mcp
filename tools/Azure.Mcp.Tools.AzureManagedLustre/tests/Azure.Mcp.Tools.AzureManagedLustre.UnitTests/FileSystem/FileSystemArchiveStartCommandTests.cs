// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Mcp.Core.Models.Command;
using Azure.Mcp.Core.Options;
using Azure.Mcp.Tools.AzureManagedLustre.Commands.FileSystem;
using Azure.Mcp.Tools.AzureManagedLustre.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Azure.Mcp.Tools.AzureManagedLustre.UnitTests.FileSystem;

public class FileSystemArchiveStartCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAzureManagedLustreService _amlfsService;
    private readonly ILogger<FileSystemArchiveStartCommand> _logger;
    private readonly FileSystemArchiveStartCommand _command;
    private readonly CommandContext _context;
    private readonly Parser _parser;

    public FileSystemArchiveStartCommandTests()
    {
        _amlfsService = Substitute.For<IAzureManagedLustreService>();
        _logger = Substitute.For<ILogger<FileSystemArchiveStartCommand>>();

        var services = new ServiceCollection().AddSingleton(_amlfsService);
        _serviceProvider = services.BuildServiceProvider();

        _command = new(_logger);
        _context = new(_serviceProvider);
        _parser = new(_command.GetCommand());
    }

    [Fact]
    public async Task ExecuteAsync_MissingResourceGroup_Returns400()
    {
        var args = _parser.Parse([
            "--subscription", "sub123",
            "--name", "fs1",
            "--path", "/"
        ]);

        var response = await _command.ExecuteAsync(_context, args);
        Assert.True(response.Status >= 400);
        Assert.Contains("resource-group", response.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ValidInputs_CallsService()
    {
        var args = _parser.Parse([
            "--subscription", "sub123",
            "--resource-group", "rg1",
            "--name", "fs1",
            "--path", "/data"
        ]);

        var response = await _command.ExecuteAsync(_context, args);

        await _amlfsService.Received(1).StartArchiveAsync(
            Arg.Is("sub123"), Arg.Is("rg1"), Arg.Is("fs1"), Arg.Is("/data"), Arg.Any<string?>(), Arg.Any<RetryPolicyOptions?>());

        Assert.NotNull(response.Results);
        var json = JsonSerializer.Serialize(response.Results);
        var result = JsonSerializer.Deserialize<ResultJson>(json);
        Assert.NotNull(result);
        Assert.Equal("fs1", result!.Name);
        Assert.Equal("/data", result.Path);
    }

    [Fact]
    public async Task ExecuteAsync_MissingNameOrPath_Returns400()
    {
        var args = _parser.Parse([
            "--subscription", "sub123",
            "--resource-group", "rg1",
            "--path", "/data"
        ]);

        var response = await _command.ExecuteAsync(_context, args);
        Assert.True(response.Status >= 400);
        Assert.Contains("--name", response.Message!, StringComparison.OrdinalIgnoreCase);
    }

    private class ResultJson
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("path")] public string? Path { get; set; }
    }
}
