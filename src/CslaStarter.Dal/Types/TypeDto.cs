namespace CslaStarter.Dal.Types;

/// <summary>One row of a *Type lookup table.</summary>
public sealed record TypeDto(int Code, string DescriptionEn, string DescriptionFr, bool IsActive, int SortOrder);
