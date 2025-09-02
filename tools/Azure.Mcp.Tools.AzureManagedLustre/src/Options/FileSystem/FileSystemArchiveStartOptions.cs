// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Mcp.Tools.AzureManagedLustre.Options.FileSystem;

public sealed class FileSystemArchiveStartOptions : BaseAzureManagedLustreOptions
{
    [property: JsonPropertyName("name")]
    public string? Name { get; set; }

    [property: JsonPropertyName("path")]
    public string? Path { get; set; }
}
