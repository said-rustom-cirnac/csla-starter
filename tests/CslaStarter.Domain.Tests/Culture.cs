using System.Globalization;

namespace CslaStarter.Domain.Tests;

/// <summary>Sets the current culture and UI culture for a test and restores both on dispose.</summary>
internal static class Culture
{
    public static IDisposable Use(string name) => new Scope(name);

    private sealed class Scope : IDisposable
    {
        private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _previousUiCulture = CultureInfo.CurrentUICulture;

        public Scope(string name)
        {
            var culture = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;
        }
    }
}
