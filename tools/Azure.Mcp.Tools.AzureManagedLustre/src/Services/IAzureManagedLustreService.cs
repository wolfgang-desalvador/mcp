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

    Task<LustreFileSystem> CreateFileSystemAsync(
        string subscription,
        string resourceGroup,
        string name,
        string location,
        string sku,
        int sizeTiB,
        string subnetId,
        string zone,
        // Maintenance window
        string maintenanceDay,
        string maintenanceTime,
        // HSM
        string? hsmContainer = null,
        string? hsmLogContainer = null,
        string? importPrefix = null,
        // Root squash
        string? rootSquashMode = null,
        string? noSquashNidLists = null,
        long? squashUid = null,
        long? squashGid = null,
        // Encryption
        bool enableCustomEncryption = false,
        string? keyUrl = null,
        string? sourceVaultId = null,
        string? userAssignedIdentityId = null,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<LustreFileSystem> UpdateFileSystemAsync(
        string subscription,
        string resourceGroup,
        string name,
        // Maintenance window (optional)
        string? maintenanceDay = null,
        string? maintenanceTime = null,
        // Root squash updates (all optional; if UID/GID provided, both required)
        string? rootSquashMode = null,
        string? noSquashNidLists = null,
        long? squashUid = null,
        long? squashGid = null,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);
}

