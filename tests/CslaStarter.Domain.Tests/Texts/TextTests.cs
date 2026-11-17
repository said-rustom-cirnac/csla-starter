using CslaStarter.Domain.Platform.Texts;

namespace CslaStarter.Domain.Tests.Texts;

public class TextTests
{
    [Fact] public void T_English_IsTheSourceString() { using var _ = Culture.Use("en-CA"); Assert.Equal("Save", Text.T("Save")); }
    [Fact] public void T_French_Unseeded_IsMarked() { using var _ = Culture.Use("fr-CA"); Assert.Equal("fr-Save", Text.T("Save")); }
    [Fact] public void T_OtherCulture_IsEnglish() { using var _ = Culture.Use("es-MX"); Assert.Equal("Save", Text.T("Save")); }
    [Fact] public void T_FillsPlaceholders() { using var _ = Culture.Use("en-CA"); Assert.Equal("Updated by Ada", Text.T("Updated by {0}", "Ada")); }
    [Fact] public void T_WithoutArguments_LeavesPlaceholdersAlone() { using var _ = Culture.Use("en-CA"); Assert.Equal("Updated by {0}", Text.T("Updated by {0}")); }
    [Fact] public void Error_UsesTheSeedText() { using var _ = Culture.Use("fr-CA"); Assert.Equal("Le champ Nom est obligatoire.", Text.Error(ErrorMessageType.Required, "Nom")); }
    [Fact] public void Error_UnknownCode_NamesTheCode() { using var _ = Culture.Use("en-CA"); Assert.Contains("999", Text.Error(999)); }
}
