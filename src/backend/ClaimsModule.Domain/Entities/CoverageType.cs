namespace ClaimsModule.Domain.Entities;

/// <summary>
/// The coverage types that appear across the seeded simulated policies (FRS §5.5). Coverage
/// evaluation itself is out of scope (FRS §2 capability matrix) — these are reference values only.
/// </summary>
public enum CoverageType
{
    Vehicle,
    Cargo,
    Property,
    Liability,
    Equipment
}
