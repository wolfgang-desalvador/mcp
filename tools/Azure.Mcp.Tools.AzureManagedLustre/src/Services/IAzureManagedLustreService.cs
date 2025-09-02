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

    Task StartArchiveAsync(
        string subscription,
        string resourceGroup,
        string fileSystemName,
        string filesystemPath,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<string?> GetArchiveStatusAsync(
        string subscription,
        string resourceGroup,
        string fileSystemName,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task CancelArchiveAsync(
        string subscription,
        string resourceGroup,
        string fileSystemName,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);
}
