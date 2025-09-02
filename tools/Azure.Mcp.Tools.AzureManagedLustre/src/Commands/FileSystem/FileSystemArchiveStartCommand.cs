// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Commands;
using Azure.Mcp.Tools.AzureManagedLustre.Options;
using Azure.Mcp.Tools.AzureManagedLustre.Options.FileSystem;
using Azure.Mcp.Tools.AzureManagedLustre.Services;
using Microsoft.Extensions.Logging;

namespace Azure.Mcp.Tools.AzureManagedLustre.Commands.FileSystem;

public sealed class FileSystemArchiveStartCommand(ILogger<FileSystemArchiveStartCommand> logger)
    : BaseAzureManagedLustreCommand<FileSystemArchiveStartOptions>(logger)
{
    private const string CommandTitle = "Start AMLFS Archive";

    public override string Name => "start";

    public override string Description =>
        "Starts an archive job for an Azure Managed Lustre filesystem at the specified path. The target filesystem should have proper integration with Azure Blob Storage configured.";

    public override string Title => CommandTitle;

    public override ToolMetadata Metadata => new() { Destructive = false, ReadOnly = false };

    private static readonly Option<string> _nameOption = AzureManagedLustreOptionDefinitions.NameOption;
    private static readonly Option<string> _pathOption = AzureManagedLustreOptionDefinitions.PathOption;

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.AddOption(_nameOption);
        command.AddOption(_pathOption);
        RequireResourceGroup();
    }

    protected override FileSystemArchiveStartOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.Name = parseResult.GetValueForOption(_nameOption);
        options.Path = parseResult.GetValueForOption(_pathOption);
        return options;
    }

    public override ValidationResult Validate(CommandResult commandResult, CommandResponse? commandResponse = null)
    {
        var result = base.Validate(commandResult, commandResponse);
        if (!result.IsValid)
            return result;

        var name = commandResult.GetValueForOption(_nameOption);
        var path = commandResult.GetValueForOption(_pathOption);
        var resourceGroup = commandResult.GetValueForOption(_resourceGroupOption);

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
        {
            result.IsValid = false;
            result.ErrorMessage = "Both --name and --path are required.";
            if (commandResponse != null)
            {
                commandResponse.Status = 400;
                commandResponse.Message = result.ErrorMessage;
            }
        }

        return result;
    }

    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult)
    {
        var options = BindOptions(parseResult);

        try
        {
            if (!Validate(parseResult.CommandResult, context.Response).IsValid)
            {
                return context.Response;
            }

            var svc = context.GetService<IAzureManagedLustreService>();
            await svc.StartArchiveAsync(
                options.Subscription!,
                options.ResourceGroup!,
                options.Name!,
                options.Path!,
                options.Tenant,
                options.RetryPolicy);

            context.Response.Results = ResponseResult.Create(new ArchiveStartResult(
                options.Subscription!, options.ResourceGroup!, options.Name!, options.Path!),
                AzureManagedLustreJsonContext.Default.ArchiveStartResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting AMLFS archive. Options: {@Options}", options);
            HandleException(context, ex);
        }

        return context.Response;
    }

    internal record ArchiveStartResult(string Subscription, string ResourceGroup, string Name, string Path);
}
