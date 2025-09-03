// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Mcp.Core.Models.Command;
using Azure.Mcp.Core.Options;
using Azure.Mcp.Tools.AzureManagedLustre.Commands.FileSystem;
using Azure.Mcp.Tools.AzureManagedLustre.Models;
using Azure.Mcp.Tools.AzureManagedLustre.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Azure.Mcp.Tools.AzureManagedLustre.UnitTests.FileSystem;

public class FileSystemUpdateCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAzureManagedLustreService _svc;
    private readonly ILogger<FileSystemUpdateCommand> _logger;
    private readonly FileSystemUpdateCommand _command;
    private readonly CommandContext _context;
    private readonly Parser _parser;

    private const string Sub = "sub123";
    private const string Rg = "rg1";
    private const string Name = "amlfs-01";

    public FileSystemUpdateCommandTests()
    {
        _svc = Substitute.For<IAzureManagedLustreService>();
        _logger = Substitute.For<ILogger<FileSystemUpdateCommand>>();
        var services = new ServiceCollection().AddSingleton(_svc);
        _serviceProvider = services.BuildServiceProvider();
        _command = new(_logger);
        _context = new(_serviceProvider);
        _parser = new(_command.GetCommand());
    }

    [Fact]
    public void Constructor_InitializesCommandCorrectly()
    {
        var command = _command.GetCommand();
        Assert.Equal("update", command.Name);
        Assert.NotNull(command.Description);
        Assert.NotEmpty(command.Description);
    }

    [Theory]
    [InlineData("--subscription sub123 --resource-group rg1 --name amlfs-01", false)] // Missing update params
    [InlineData("--resource-group rg1 --name amlfs-01 --maintenance-day Monday", false)] // Missing subscription
    [InlineData("--subscription sub123 --name amlfs-01 --maintenance-day Monday", false)] // Missing resource group
    [InlineData("--subscription sub123 --resource-group rg1 --maintenance-day Monday", false)] // Missing name
    [InlineData("--subscription sub123 --resource-group rg1 --name amlfs-01 --maintenance-day Monday", false)]
    [InlineData("--subscription sub123 --resource-group rg1 --name amlfs-01 --maintenance-time 00:00", false)]
    [InlineData("--subscription sub123 --resource-group rg1 --name amlfs-01 --no-squash-nid-list nid1,nid2 --squash-uid 1000", false)] // missing gid
    public async Task ExecuteAsync_ValidatesInputCorrectly(string args, bool shouldSucceed)
    {
        // Arrange
        if (shouldSucceed)
        {
            _svc.UpdateFileSystemAsync(
                Arg.Is(Sub), Arg.Is(Rg), Arg.Is(Name),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<long?>(), Arg.Any<long?>(),
                Arg.Any<string?>(), Arg.Any<RetryPolicyOptions?>())
                .Returns(CreateLustre());
        }

        var parseResult = _parser.Parse(args.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        // Act
        var response = await _command.ExecuteAsync(_context, parseResult);

        // Assert
        Assert.Equal(shouldSucceed ? 200 : 400, response.Status);
        if (!shouldSucceed)
        {
            Assert.False(string.IsNullOrWhiteSpace(response.Message));
            Assert.True(
                response.Message.Contains("required", StringComparison.OrdinalIgnoreCase)
                || response.Message.Contains("provide", StringComparison.OrdinalIgnoreCase)
                || response.Message.Contains("must be", StringComparison.OrdinalIgnoreCase)
            );
        }
        else
        {
            Assert.NotNull(response.Results);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MaintenanceUpdate_CallsServiceAndReturnsResult()
    {
        var expected = CreateLustre();
        _svc.UpdateFileSystemAsync(Sub, Rg, Name, "Monday", "01:00", null, null, null, null, null, Arg.Any<RetryPolicyOptions?>())
            .Returns(expected);

        var args = _parser.Parse([
            "--subscription", Sub,
            "--resource-group", Rg,
            "--name", Name,
            "--maintenance-day", "Monday",
            "--maintenance-time", "01:00"
        ]);

        var response = await _command.ExecuteAsync(_context, args);
        Assert.Equal(200, response.Status);
        await _svc.Received(1).UpdateFileSystemAsync(Sub, Rg, Name, "Monday", "01:00", null, null, null, null, null, Arg.Any<RetryPolicyOptions?>());

        var json = JsonSerializer.Serialize(response.Results);
        var result = JsonSerializer.Deserialize<UpdateResultJson>(json);
        Assert.NotNull(result);
        Assert.Equal(Name, result!.FileSystem.Name);
    }

    [Fact]
    public async Task ExecuteAsync_RootSquashUpdate_CallsService()
    {
        var expected = CreateLustre();
        _svc.UpdateFileSystemAsync(Sub, Rg, Name, null, null, "All", "nid1,nid2", 1000, 1000, null, Arg.Any<RetryPolicyOptions?>())
            .Returns(expected);

        var args = _parser.Parse([
            "--subscription", Sub,
            "--resource-group", Rg,
            "--name", Name,
            "--root-squash-mode", "All",
            "--no-squash-nid-list", "nid1,nid2",
            "--squash-uid", "1000",
            "--squash-gid", "1000"
        ]);

        var response = await _command.ExecuteAsync(_context, args);
        Assert.Equal(200, response.Status);
        await _svc.Received(1).UpdateFileSystemAsync(Sub, Rg, Name, null, null, "All", "nid1,nid2", 1000, 1000, null, Arg.Any<RetryPolicyOptions?>());
    }

    [Fact]
    public async Task ExecuteAsync_ServiceThrowsRequestFailed_ReturnsStatus()
    {
        _svc.UpdateFileSystemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<long?>(), Arg.Any<long?>(),
            Arg.Any<string?>(), Arg.Any<RetryPolicyOptions?>())
            .ThrowsAsync(new Azure.RequestFailedException(404, "not found"));

        var args = _parser.Parse([
            "--subscription", Sub,
            "--resource-group", Rg,
            "--name", Name,
            "--maintenance-day", "Monday",
            "--maintenance-time", "00:00"
        ]);

        var response = await _command.ExecuteAsync(_context, args);
        Assert.Equal(404, response.Status);
        Assert.Contains("not found", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("--subscription sub123 --resource-group rg1 --name amlfs-01 --root-squash-mode All --squash-uid 1000", false)] // missing gid
    [InlineData("--subscription sub123 --resource-group rg1 --name amlfs-01 --root-squash-mode All --squash-gid 1000", false)] // missing uid
    [InlineData("--subscription sub123 --resource-group rg1 --name amlfs-01 --root-squash-mode None", true)] // None doesn't require uid/gid
    public async Task ExecuteAsync_RootSquashMode_Validation_Works(string args, bool shouldSucceed)
    {
        if (shouldSucceed)
        {
            _svc.UpdateFileSystemAsync(
                Arg.Is(Sub), Arg.Is(Rg), Arg.Is(Name),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<long?>(), Arg.Any<long?>(),
                Arg.Any<string?>(), Arg.Any<RetryPolicyOptions?>())
                .Returns(CreateLustre());
        }

        var parseResult = _parser.Parse(args.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var response = await _command.ExecuteAsync(_context, parseResult);

        Assert.Equal(shouldSucceed ? 200 : 400, response.Status);
        if (!shouldSucceed)
        {
            Assert.Contains("squash", response.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RootSquashMode_WithUidGid_SucceedsAndCallsService()
    {
        var expected = CreateLustre();
        _svc.UpdateFileSystemAsync(Sub, Rg, Name, null, null, "All", "nid1,nid2", 2000, 3000, null, Arg.Any<RetryPolicyOptions?>())
            .Returns(expected);

        var args = _parser.Parse([
            "--subscription", Sub,
            "--resource-group", Rg,
            "--name", Name,
            "--root-squash-mode", "All",
            "--no-squash-nid-list", "nid1,nid2",
            "--squash-uid", "2000",
            "--squash-gid", "3000"
        ]);

        var response = await _command.ExecuteAsync(_context, args);
        Assert.Equal(200, response.Status);
        await _svc.Received(1).UpdateFileSystemAsync(Sub, Rg, Name, null, null, "All", "nid1,nid2", 2000, 3000, null, Arg.Any<RetryPolicyOptions?>());
    }

    [Fact]
    public async Task ExecuteAsync_RootSquashNotNone_MissingNoSquashNidList_Returns400()
    {
        var args = _parser.Parse([
            "--subscription", Sub,
            "--resource-group", Rg,
            "--name", Name,
            "--root-squash-mode", "All",
            "--squash-uid", "1000",
            "--squash-gid", "1000"
        ]);

        var response = await _command.ExecuteAsync(_context, args);

        Assert.True(response.Status >= 400);
        Assert.Contains("no-squash", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static LustreFileSystem CreateLustre() => new(
        Name,
        $"/subs/{Sub}/rg/{Rg}/providers/Microsoft.StorageCache/amlfs/{Name}",
        Rg,
        Sub,
        "eastus",
        "Succeeded",
        "Healthy",
        "10.0.0.4",
        "AMLFS-Durable-Premium-125",
        4,
        null,
        "Monday",
        "00:00");

    private class UpdateResultJson
    {
        [JsonPropertyName("fileSystem")]
        public LustreFileSystem FileSystem { get; set; } = null!;
    }
}
