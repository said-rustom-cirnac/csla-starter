namespace CslaStarter.Data.Platform.Texts;

/// <summary>A row of <c>error_message_type</c>, keyed by the <c>ErrorMessageType</c> constant that code uses.</summary>
public sealed class ErrorMessageRow
{
    /// <summary>Owned by code: the value of an <c>ErrorMessageType</c> constant, never generated.</summary>
    public int Code { get; set; }

    public string EnDesc { get; set; } = string.Empty;

    public string FrDesc { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Set by the database when the row is inserted.</summary>
    public DateTimeOffset AddedDate { get; set; }
}
