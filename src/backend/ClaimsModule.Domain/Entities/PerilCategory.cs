namespace ClaimsModule.Domain.Entities;

/// <summary>FRS §9 data model: the closed set of peril categories a cause-of-loss code belongs to.</summary>
public enum PerilCategory
{
    Property,
    Auto,
    Liability,
    Weather,
    Equipment,
    Crime,
    General
}
