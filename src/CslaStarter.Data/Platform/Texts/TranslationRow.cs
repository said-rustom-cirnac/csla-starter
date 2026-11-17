namespace CslaStarter.Data.Platform.Texts;

/// <summary>A row of <c>translation_type</c>: interface text, named in code by its English source string.</summary>
public sealed class TranslationRow
{
    /// <summary>Assigned by the database.</summary>
    public int Code { get; set; }

    public string SourceString { get; set; } = string.Empty;

    public string EnDesc { get; set; } = string.Empty;

    public string FrDesc { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Set by the database when the row is inserted.</summary>
    public DateTimeOffset AddedDate { get; set; }
}
