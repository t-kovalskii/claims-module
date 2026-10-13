# Claim aggregate includes its reserve components

BR-R-07 caps total approved reserves across all components at $10,000,000 per claim — a cross-component invariant scoped entirely to one claim. We modeled `ReserveComponent`, `LossEvent`, `ClaimParty`, and `ClaimRiskObject` as child entities of the `Claim` aggregate root, loaded and persisted together through a single `ClaimRepository`, rather than as independent aggregates referencing `ClaimId`.

Enforcing BR-R-07 across separate aggregates would require a saga or eventual-consistency mechanism for no real benefit, since a claim's reserves are never contended by writers outside that claim's own workflow. Keeping them in one aggregate lets the invariant be enforced synchronously, in-memory, on write.
