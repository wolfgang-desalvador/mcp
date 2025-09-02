// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Commands;
using Azure.Mcp.Tools.AzureManagedLustre.Options;
using Azure.Mcp.Tools.AzureManagedLustre.Options.FileSystem;
using Azure.Mcp.Tools.AzureManagedLustre.Services;
using Microsoft.Extensions.Logging;

namespace Azure.Mcp.Tools.AzureManagedLustre.Commands.FileSystem;

public sealed class FileSystemArchiveStatusCommand(ILogger<FileSystemArchiveStatusCommand> logger)
    : BaseAzureManagedLustreCommand<FileSystemArchiveStatusOptions>(logger)
{
    private const string CommandTitle = "Get AMLFS Archive Status";

    public override string Name => "status";

    public override string Description =>
        "Gets the archive status for an Azure Managed Lustre filesystem.";

    public override string Title => CommandTitle;

    public override ToolMetadata Metadata => new() { Destructive = false, ReadOnly = true };

    private static readonly Option<string> _nameOption = AzureManagedLustreOptionDefinitions.NameOption;

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.AddOption(_nameOption);
        RequireResourceGroup();
    }

    protected override FileSystemArchiveStatusOptions BindOptions(ParseResult parseResult)
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
            var status = await svc.GetArchiveStatusAsync(
                options.Subscription!,
                options.ResourceGroup!,
                options.Name!,
                options.Tenant,
                options.RetryPolicy);

            context.Response.Results = ResponseResult.Create(new ArchiveStatusResult(
                options.Subscription!, options.ResourceGroup!, options.Name!, status),
                AzureManagedLustreJsonContext.Default.ArchiveStatusResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AMLFS archive status. Options: {@Options}", options);
            HandleException(context, ex);
        }

        return context.Response;
    }

    internal record ArchiveStatusResult(string Subscription, string ResourceGroup, string Name, string? Status);
}
