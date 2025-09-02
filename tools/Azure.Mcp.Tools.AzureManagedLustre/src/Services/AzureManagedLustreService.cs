// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Options;
using Azure.Mcp.Core.Services.Azure;
using Azure.Mcp.Core.Services.Azure.ResourceGroup;
using Azure.Mcp.Core.Services.Azure.Subscription;
using Azure.Mcp.Core.Services.Azure.Tenant;
using Azure.Mcp.Tools.AzureManagedLustre.Models;
using Azure.ResourceManager.StorageCache;
using Azure.ResourceManager.StorageCache.Models;

namespace Azure.Mcp.Tools.AzureManagedLustre.Services;

public sealed class AzureManagedLustreService(ISubscriptionService subscriptionService, IResourceGroupService resourceGroupService, ITenantService tenantService) : BaseAzureService(tenantService), IAzureManagedLustreService
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
    private readonly IResourceGroupService _resourceGroupService = resourceGroupService;

    public async Task<List<LustreFileSystem>> ListFileSystemsAsync(string subscription, string? resourceGroup = null, string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);

        var results = new List<LustreFileSystem>();

        try
        {
            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                var rg = await _resourceGroupService.GetResourceGroupResource(subscription, resourceGroup, tenant, retryPolicy) ?? throw new Exception($"Resource group '{resourceGroup}' not found");
                foreach (var fs in rg.GetAmlFileSystems())
                {
                    results.Add(Map(fs));
                }
                return results;
            }
            else
            {
                var sub = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy) ?? throw new Exception($"Subscription '{subscription}' not found");
                foreach (var fs in sub.GetAmlFileSystems())
                {
                    results.Add(Map(fs));
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error listing AMLFS file systems in subscription '{subscription}': {ex.Message}", ex);
        }

        return results;
    }

    private static LustreFileSystem Map(AmlFileSystemResource fs)
    {
        var data = fs.Data;
        return new LustreFileSystem(
            data.Name,
            fs.Id.ToString(),
            fs.Id.ResourceGroupName,
            fs.Id.SubscriptionId,
            data.Location,
            data.ProvisioningState?.ToString(),
            data.Health?.ToString(),
            data.ClientInfo?.MgsAddress,
            data.SkuName,
            data.StorageCapacityTiB.HasValue ? (long?)Convert.ToInt64(Math.Round(data.StorageCapacityTiB.Value)) : null,
            data.Hsm?.Settings?.Container,
            data.MaintenanceWindow?.DayOfWeek?.ToString(),
            data.MaintenanceWindow?.TimeOfDayUTC?.ToString()
        );
    }

    public async Task<int> GetRequiredAmlFSSubnetsSize(string subscription,
    string sku, int size,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null
        )
    {
        var sub = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy) ?? throw new Exception($"Subscription '{subscription}' not found");
        var fileSystemSizeContent = new RequiredAmlFileSystemSubnetsSizeContent
        {
            SkuName = sku,
            StorageCapacityTiB = size
        };

        try
        {
            var sdkResult = sub.GetRequiredAmlFSSubnetsSize(fileSystemSizeContent);
            var numberOfRequiredIPs = sdkResult.Value.FilesystemSubnetSize ?? throw new Exception($"Failed to retrieve the number of IPs");
            return numberOfRequiredIPs;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving required subnet size: {ex.Message}", ex);
        }
    }

    public async Task StartArchiveAsync(
        string subscription,
        string resourceGroup,
        string fileSystemName,
        string filesystemPath,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceGroup);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileSystemName);
        ArgumentException.ThrowIfNullOrWhiteSpace(filesystemPath);

        var rg = await _resourceGroupService.GetResourceGroupResource(subscription, resourceGroup, tenant, retryPolicy)
            ?? throw new Exception($"Resource group '{resourceGroup}' not found");

        try
        {
            var fs = await rg.GetAmlFileSystemAsync(fileSystemName);
            var content = new AmlFileSystemArchiveContent
            {
                FilesystemPath = filesystemPath
            };
            _ = fs.Value.Archive(content);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error starting archive for AMLFS '{fileSystemName}' in RG '{resourceGroup}': {ex.Message}", ex);
        }
    }

    public async Task<string?> GetArchiveStatusAsync(
        string subscription,
        string resourceGroup,
        string fileSystemName,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceGroup);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileSystemName);

        var rg = await _resourceGroupService.GetResourceGroupResource(subscription, resourceGroup, tenant, retryPolicy)
            ?? throw new Exception($"Resource group '{resourceGroup}' not found");
        try
        {
            var fs = await rg.GetAmlFileSystemAsync(fileSystemName);
            var data = fs.Value.Data;
            // SDK doesn't expose a specific archive job state. Report if HSM is configured as a minimal status.
            return data.Hsm is not null ? "Configured" : "NotConfigured";
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting archive status for AMLFS '{fileSystemName}' in RG '{resourceGroup}': {ex.Message}", ex);
        }
    }

    public async Task CancelArchiveAsync(
        string subscription,
        string resourceGroup,
        string fileSystemName,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceGroup);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileSystemName);

        var rg = await _resourceGroupService.GetResourceGroupResource(subscription, resourceGroup, tenant, retryPolicy)
            ?? throw new Exception($"Resource group '{resourceGroup}' not found");
        try
        {
            var fs = await rg.GetAmlFileSystemAsync(fileSystemName);
            _ = fs.Value.CancelArchive();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error canceling archive for AMLFS '{fileSystemName}' in RG '{resourceGroup}': {ex.Message}", ex);
        }
    }
}
