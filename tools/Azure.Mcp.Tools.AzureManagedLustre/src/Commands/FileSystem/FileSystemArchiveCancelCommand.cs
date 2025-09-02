// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Commands;
using Azure.Mcp.Tools.AzureManagedLustre.Options;
using Azure.Mcp.Tools.AzureManagedLustre.Options.FileSystem;
using Azure.Mcp.Tools.AzureManagedLustre.Services;
using Microsoft.Extensions.Logging;

namespace Azure.Mcp.Tools.AzureManagedLustre.Commands.FileSystem;

public sealed class FileSystemArchiveCancelCommand(ILogger<FileSystemArchiveCancelCommand> logger)
    : BaseAzureManagedLustreCommand<FileSystemArchiveCancelOptions>(logger)
{
    private const string CommandTitle = "Cancel AMLFS Archive";

    public override string Name => "cancel";

    public override string Description =>
        "Cancels a running archive job for an Azure Managed Lustre filesystem.";

    public override string Title => CommandTitle;

    public override ToolMetadata Metadata => new() { Destructive = false, ReadOnly = false };

    private static readonly Option<string> _nameOption = AzureManagedLustreOptionDefinitions.NameOption;

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.AddOption(_nameOption);
        RequireResourceGroup();
    }

    protected override FileSystemArchiveCancelOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.Name = parseResult.GetValueForOption(_nameOption);

        return options;
    }

    public override ValidationResult Validate(CommandResult commandResult, CommandResponse? commandResponse = null)
    {
        var result = base.Validate(commandResult, commandResponse);
        if (!result.IsValid)
            return result;

        var name = commandResult.GetValueForOption(_nameOption);

        if (string.IsNullOrWhiteSpace(name))
        {
            result.IsValid = false;
            result.ErrorMessage = "--name is required.";
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
            await svc.CancelArchiveAsync(
                options.Subscription!,
                options.ResourceGroup!,
                options.Name!,
                options.Tenant,
                options.RetryPolicy);

            context.Response.Results = ResponseResult.Create(new ArchiveCancelResult(
                options.Subscription!, options.ResourceGroup!, options.Name!),
                AzureManagedLustreJsonContext.Default.ArchiveCancelResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling AMLFS archive. Options: {@Options}", options);
            HandleException(context, ex);
        }

        return context.Response;
    }

    internal record ArchiveCancelResult(string Subscription, string ResourceGroup, string Name);
}
