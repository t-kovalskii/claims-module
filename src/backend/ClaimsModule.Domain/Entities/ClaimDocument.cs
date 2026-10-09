using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// Ticket 10 / FRS §9.7: a supporting document attached to a <see cref="Claim"/>. Deliberately
/// not an <see cref="AuditableEntity"/> — like <see cref="ClaimAuditLog"/> and
/// <see cref="ReserveHistory"/>, it's an immutable, append-only record (no update/delete
/// endpoint exists) with no RowVersion/soft-delete concern. Unlike those two, though,
/// <c>ClaimDocumentConfiguration</c> does give it a real DB-level FK (cascade delete) to Claims —
/// a document has no reason to outlive the claim it was uploaded against, whereas an audit log
/// or reserve history row is deliberately kept as a denormalised, FK-less fact. Still not part
/// of the Claim aggregate itself (ADR-0001 lists only
/// LossEvent/ClaimParty/ClaimRiskObject/ReserveComponent): attaching a document enforces no
/// cross-entity invariant, so the upload command writes it directly via
/// <c>IApplicationDbContext</c> rather than through Claim/ClaimRepository.
/// </summary>
public class ClaimDocument : BaseEntity, IHasOrganization
{
    public Guid OrganizationId { get; set; }
    public Guid ClaimId { get; set; }

    /// <summary>The filename as the uploader supplied it, for display only.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Sanitised against path traversal — the name segment actually used in
    /// <see cref="StoragePath"/> (prefixed there with a GUID to avoid collisions).</summary>
    public string SanitizedFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    /// <summary>The storage provider's key/path: <c>{organisationId}/{claimId}/{guid}_{sanitizedFileName}</c>,
    /// scoped per-organisation and per-claim regardless of which <c>IStorageService</c> is active.</summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>Free-form category supplied by the uploader (e.g. "Police Report", "Photo") —
    /// the FRS names no fixed taxonomy, so this isn't an enum.</summary>
    public string? DocumentType { get; set; }

    public Guid? UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}
