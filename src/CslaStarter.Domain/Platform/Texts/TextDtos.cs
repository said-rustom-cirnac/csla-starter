namespace CslaStarter.Domain.Platform.Texts;

/// <summary>A translation row: an English source string and its text in each language.</summary>
public sealed record TranslationDto(int Code, string SourceString, string EnDesc, string FrDesc, bool IsActive);

/// <summary>An error message row, keyed by its <see cref="ErrorMessageType"/> code.</summary>
public sealed record ErrorMessageDto(int Code, string EnDesc, string FrDesc, bool IsActive);
