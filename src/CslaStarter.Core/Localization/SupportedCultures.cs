namespace CslaStarter.Core.Localization;

public static class SupportedCultures
{
    public const string English = "en-CA";

    public const string French = "fr-CA";

    public static IReadOnlyList<string> All { get; } = [English, French];
}
