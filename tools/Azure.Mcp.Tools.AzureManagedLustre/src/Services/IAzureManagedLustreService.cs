// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Options;
using Azure.Mcp.Tools.AzureManagedLustre.Models;

namespace Azure.Mcp.Tools.AzureManagedLustre.Services;

public interface IAzureManagedLustreService
{
    Task<List<LustreFileSystem>> ListFileSystemsAsync(
        string subscription,
        string? resourceGroup = null,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<int> GetRequiredAmlFSSubnetsSize(string subscription,
        string sku, int size,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<bool> CheckAmlFSSubnetAsync(
        string subscription,
        string sku,
        int size,
        string subnetId,
        string location,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);
}
