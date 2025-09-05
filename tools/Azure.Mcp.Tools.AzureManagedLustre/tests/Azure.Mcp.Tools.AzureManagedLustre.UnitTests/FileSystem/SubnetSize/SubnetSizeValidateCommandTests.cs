// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Mcp.Core.Models.Command;
using Azure.Mcp.Core.Options;
using Azure.Mcp.Tools.AzureManagedLustre.Commands.FileSystem;
using Azure.Mcp.Tools.AzureManagedLustre.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Azure.Mcp.Tools.AzureManagedLustre.UnitTests.FileSystem;

public class FileSystemCheckSubnetCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAzureManagedLustreService _amlfsService;
    private readonly ILogger<FileSystemSubnetSizeCheckCommand> _logger;
    private readonly FileSystemSubnetSizeCheckCommand _command;
    private readonly Command _commandDefinition;
    private readonly CommandContext _context;
    private readonly string _knownSubscriptionId = "sub123";

    public FileSystemCheckSubnetCommandTests()
    {
        _amlfsService = Substitute.For<IAzureManagedLustreService>();
        _logger = Substitute.For<ILogger<FileSystemSubnetSizeCheckCommand>>();

        var services = new ServiceCollection().AddSingleton(_amlfsService);
        _serviceProvider = services.BuildServiceProvider();

        _command = new(_logger);
        _context = new(_serviceProvider);
        _commandDefinition = _command.GetCommand();
    }

    [Fact]
    public void Constructor_InitializesCommandCorrectly()
    {
        var command = _command.GetCommand();
        Assert.Equal("validate", command.Name);
        Assert.NotNull(command.Description);
        Assert.NotEmpty(command.Description);
    }

    [Fact]
    public async Task ExecuteAsync_Succeeds_ForValidInput()
    {
        // Arrange
        _amlfsService.CheckAmlFSSubnetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RetryPolicyOptions?>())
            .Returns(Task.FromResult(true));

        // Arrange
        var args = _commandDefinition.Parse([
            "--sku", "AMLFS-Durable-Premium-40",
            "--size", "48",
            "--location", "eastus",
            "--subnet-id", "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet/subnets/sn1",
            "--subscription", _knownSubscriptionId
        ]);

        // Act
        var response = await _command.ExecuteAsync(_context, args);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Results);

        var json = JsonSerializer.Serialize(response.Results);
        var result = JsonSerializer.Deserialize<ResultJson>(json);
        Assert.NotNull(result);
        Assert.True(result!.Valid);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSku_Returns400()
    {
        // Arrange
        var args = _commandDefinition.Parse([
            "--sku", "INVALID-SKU",
            "--size", "48",
            "--location", "eastus",
            "--subnet-id", "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet/subnets/sn1",
            "--subscription", _knownSubscriptionId
        ]);

        // Act
        var response = await _command.ExecuteAsync(_context, args);

        // Assert
        Assert.True(response.Status >= 400);
        Assert.Contains("invalid sku", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ServiceThrows_IsHandled()
    {
        // Arrange
        _amlfsService.CheckAmlFSSubnetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RetryPolicyOptions?>())
            .ThrowsAsync(new Exception("kaboom"));

        var args = _commandDefinition.Parse([
            "--sku", "AMLFS-Durable-Premium-40",
            "--size", "48",
            "--location", "eastus",
            "--subnet-id", "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet/subnets/sn1",
            "--subscription", _knownSubscriptionId
        ]);

        // Act
        var response = await _command.ExecuteAsync(_context, args);

        // Assert
        Assert.True(response.Status >= 500);
        Assert.Contains("kaboom", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    private class ResultJson
    {
        [JsonPropertyName("valid")]
        public bool Valid { get; set; }
    }
}
