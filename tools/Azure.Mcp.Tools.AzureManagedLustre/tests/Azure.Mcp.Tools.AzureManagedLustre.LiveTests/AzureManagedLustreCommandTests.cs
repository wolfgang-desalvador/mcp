// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Mcp.Tests;
using Azure.Mcp.Tests.Client;
using Azure.Mcp.Tests.Client.Helpers;
using Xunit;

namespace Azure.Mcp.Tools.AzureManagedLustre.LiveTests
{
    public class AzureManagedLustreCommandTests(LiveTestFixture liveTestFixture, ITestOutputHelper output)
        : CommandTestsBase(liveTestFixture, output), IClassFixture<LiveTestFixture>
    {
        [Fact]
        public async Task Should_list_filesystems_by_subscription()
        {
            var result = await CallToolAsync(
                "azmcp_azuremanagedlustre_filesystem_list",
                new()
                {
                    { "subscription", Settings.SubscriptionId }
                });

            var fileSystems = result.AssertProperty("fileSystems");
            Assert.Equal(JsonValueKind.Array, fileSystems.ValueKind);
            var found = false;

            foreach (var fs in fileSystems.EnumerateArray())
            {
                if (fs.ValueKind != JsonValueKind.Object) continue;

                if (fs.TryGetProperty("name", out var nameProp) &&
                    nameProp.ValueKind == JsonValueKind.String &&
                    string.Equals(nameProp.GetString(), Settings.ResourceBaseName , StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            Assert.True(found, $"Expected at least one filesystem in resource group with name '{Settings.ResourceBaseName }'.");
        }

        [Fact]
        public async Task Should_calculate_required_subnet_size()
        {
            var result = await CallToolAsync(
                "azmcp_azuremanagedlustre_filesystem_required-subnet-size",
                new()
                {
                    { "subscription", Settings.SubscriptionId },
                    { "sku", "AMLFS-Durable-Premium-40" },
                    { "size", 480 }
                });

            var ips = result.AssertProperty("numberOfRequiredIPs");
            Assert.Equal(JsonValueKind.Number, ips.ValueKind);
            Assert.Equal(21, ips.GetInt32());
        }

        [Fact]
        public async Task Should_create_azure_managed_lustre_no_blob_no_cmk()
        {
            var fsName = $"amlfs-{Guid.NewGuid().ToString("N")[..8]}";
            var subnetId = Environment.GetEnvironmentVariable("AMLFS_SUBNET_ID");
            var location = Environment.GetEnvironmentVariable("LOCATION");

            // Calculate CMK required variables

            var keyUri = Environment.GetEnvironmentVariable("KEY_URI_WITH_VERSION");
            var keyVaultResourceId = Environment.GetEnvironmentVariable("KEY_VAULT_RESOURCE_ID");
            var userAssignedIdentityId = Environment.GetEnvironmentVariable("USER_ASSIGNED_IDENTITY_RESOURCE_ID");

            // Calculate HSM required variables
            var hsmDataContainerId = Environment.GetEnvironmentVariable("HSM_CONTAINER_ID");
            var hsmLogContainerId = Environment.GetEnvironmentVariable("HSM_LOGS_CONTAINER_ID");

            var result = await CallToolAsync(
                "azmcp_azuremanagedlustre_filesystem_create",
                new()
                {
                        { "subscription", Settings.SubscriptionId },
                        { "resource-group", Settings.ResourceGroupName },
                        { "location", location },
                        { "name", fsName },
                        { "sku", "AMLFS-Durable-Premium-500" },
                        { "size", 4 },
                        { "zone", 1 },
                        { "subnet-id", subnetId },
                        { "hsm-container", hsmDataContainerId },
                        { "hsm-log-container", hsmLogContainerId },
                        { "custom-encryption", true },
                        { "key-url", keyUri },
                        { "source-vault", keyVaultResourceId },
                        { "user-assigned-identity-id", userAssignedIdentityId },
                        { "maintenance-day", "Monday" },
                        { "maintenance-time", "01:00" },
                        { "root-squash-mode", "All" },
                        { "no-squash-nid-list", "10.0.0.4"},
                        { "squash-uid", 1000 },
                        { "squash-gid", 1000 }
                });

            var fileSystem = result.AssertProperty("fileSystem");
            Assert.Equal(JsonValueKind.Object, fileSystem.ValueKind);

            var name = fileSystem.GetProperty("name").GetString();
            Assert.Equal(fsName, name);

            var fsLocation = fileSystem.GetProperty("location").GetString();
            Assert.Equal(location, fsLocation);

            var hasCapacity = false;
            var capacityValue = 0;

            if (fileSystem.TryGetProperty("storageCapacityTiB", out var capTiB) && capTiB.ValueKind == JsonValueKind.Number)
            {
                hasCapacity = true;
                capacityValue = capTiB.GetInt32();
            }
            Assert.True(hasCapacity, "Expected a storage capacity property.");
            Assert.Equal(4, capacityValue);

        }

        [Fact]
        public async Task Should_update_maintenance_and_verify_with_list()
        {
            // Update maintenance window for existing filesystem
            var updateResult = await CallToolAsync(
                "azmcp_azuremanagedlustre_filesystem_update",
                new()
                {
                    { "subscription", Settings.SubscriptionId },
                    { "resource-group", Settings.ResourceGroupName },
                    { "name", Settings.ResourceBaseName },
                    { "maintenance-day", "Wednesday" },
                    { "maintenance-time", "11:00" }
                });

            var updatedFs = updateResult.AssertProperty("fileSystem");
            Assert.Equal(JsonValueKind.Object, updatedFs.ValueKind);

            // Verify via list
            var listResult = await CallToolAsync(
                "azmcp_azuremanagedlustre_filesystem_list",
                new()
                {
                    { "subscription", Settings.SubscriptionId }
                });

            var fileSystems = listResult.AssertProperty("fileSystems");
            Assert.Equal(JsonValueKind.Array, fileSystems.ValueKind);

            var found = false;
            foreach (var fs in fileSystems.EnumerateArray())
            {
                if (fs.ValueKind != JsonValueKind.Object) continue;

                if (fs.TryGetProperty("name", out var nameProp) &&
                    nameProp.ValueKind == JsonValueKind.String &&
                    string.Equals(nameProp.GetString(), Settings.ResourceBaseName, StringComparison.OrdinalIgnoreCase))
                {
                    // Check maintenance fields
                    if (fs.TryGetProperty("maintenanceDay", out var dayProp) && dayProp.ValueKind == JsonValueKind.String &&
                        fs.TryGetProperty("maintenanceTime", out var timeProp) && timeProp.ValueKind == JsonValueKind.String)
                    {
                        Assert.Equal("Wednesday", dayProp.GetString());
                        Assert.Equal("11:00", timeProp.GetString());
                        found = true;
                        break;
                    }
                }
            }

            Assert.True(found, $"Expected filesystem '{Settings.ResourceBaseName}' to have maintenance Wednesday at 11:00.");
        }

    }
}
