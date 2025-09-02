// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine.Parsing;
using Azure.Mcp.Core.Models.Command;
using Azure.Mcp.Core.Options;
using Azure.Mcp.Tools.AzureManagedLustre.Commands.FileSystem;
using Azure.Mcp.Tools.AzureManagedLustre.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Azure.Mcp.Tools.AzureManagedLustre.UnitTests.FileSystem;

public class FileSystemArchiveStatusCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAzureManagedLustreService _amlfsService;
    private readonly ILogger<FileSystemArchiveStatusCommand> _logger;
    private readonly FileSystemArchiveStatusCommand _command;
    private readonly CommandContext _context;
    private readonly Parser _parser;

    public FileSystemArchiveStatusCommandTests()
    {
        _amlfsService = Substitute.For<IAzureManagedLustreService>();
        _logger = Substitute.For<ILogger<FileSystemArchiveStatusCommand>>();

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
            "--name", "fs1"
        ]);

        var response = await _command.ExecuteAsync(_context, args);
        Assert.True(response.Status >= 400);
        Assert.Contains("resource-group", response.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ValidInputs_CallsService()
    {
        _amlfsService.GetArchiveStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<RetryPolicyOptions?>())
                .Returns("Configured");

        var args = _parser.Parse([
            "--subscription", "sub123",
            "--resource-group", "rg1",
            "--name", "fs1"
        ]);

        var response = await _command.ExecuteAsync(_context, args);
        Assert.Equal(200, response.Status);
        await _amlfsService.Received(1).GetArchiveStatusAsync(
            Arg.Is("sub123"), Arg.Is("rg1"), Arg.Is("fs1"), Arg.Is<string?>(x => x == null), Arg.Any<RetryPolicyOptions?>());
    }

    [Fact]
    public async Task ExecuteAsync_MissingName_Returns400()
    {
        var args = _parser.Parse([
            "--subscription", "sub123",
            "--resource-group", "rg1"
        ]);

        var response = await _command.ExecuteAsync(_context, args);
        Assert.True(response.Status >= 400);
        Assert.Contains("--name", response.Message!, StringComparison.OrdinalIgnoreCase);
    }
}
