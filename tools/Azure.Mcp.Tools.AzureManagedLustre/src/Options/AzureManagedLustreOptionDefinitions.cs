// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Mcp.Tools.AzureManagedLustre.Options;

public static class AzureManagedLustreOptionDefinitions
{
    public const string sku = "sku";
    public const string size = "size";
    public const string name = "name";
    public const string path = "path";
    public static readonly Option<string> SkuOption = new(
        $"--{sku}",
        "The AMLFS SKU. Allowed values: AMLFS-Durable-Premium-40, AMLFS-Durable-Premium-125, AMLFS-Durable-Premium-250, AMLFS-Durable-Premium-500."
    )
    {
        IsRequired = true
    };

    public static readonly Option<int> SizeOption = new(
        $"--{size}",
        "The AMLFS size (TiB)."
    )
    {
        IsRequired = true
    };

    public static readonly Option<string> NameOption = new(
        $"--{name}",
        "The name of the Azure Managed Lustre filesystem.")
    {
        IsRequired = true
    };

    public static readonly Option<string> PathOption = new(
        $"--{path}",
        "The filesystem path to archive (for example: /, /datasets, or a sub-folder).")
    {
        IsRequired = true
    };
}
