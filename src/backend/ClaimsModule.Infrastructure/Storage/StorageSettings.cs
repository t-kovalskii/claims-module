namespace ClaimsModule.Infrastructure.Storage;

/// <summary>Ticket 10 / spec.md's Document storage decision: which <c>IStorageService</c>
/// DependencyInjection wires up is entirely config-driven — swapping providers (e.g. because
/// Azure Blob isn't reachable in local development) never needs a code change.</summary>
public class StorageSettings
{
    public const string SectionName = "Storage";

    /// <summary><c>"Local"</c> or <c>"AzureBlob"</c>; defaults to <c>Local</c> so a missing
    /// section (e.g. an older appsettings.json) still boots.</summary>
    public string Provider { get; set; } = "Local";

    public LocalStorageSettings Local { get; set; } = new();
    public AzureBlobStorageSettings AzureBlob { get; set; } = new();
}

public class LocalStorageSettings
{
    /// <summary>Relative to the API's content root unless rooted.</summary>
    public string RootPath { get; set; } = "App_Data/documents";

    /// <summary>The externally-reachable base URL this API is served on — used to build the
    /// signed local-download link returned in place of a real SAS URL.</summary>
    public string PublicBaseUrl { get; set; } = "https://localhost:5001";

    /// <summary>HMAC signing key for locally-issued download links. Local-dev-only — production
    /// uses Azure Blob's own SAS signing, never this.</summary>
    public string SigningKey { get; set; } = "local-development-only-signing-key-change-me";
}

public class AzureBlobStorageSettings
{
    public string? ConnectionString { get; set; }
    public string ContainerName { get; set; } = "claim-documents";
}
