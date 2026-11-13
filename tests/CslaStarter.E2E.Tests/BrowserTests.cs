using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace CslaStarter.E2E.Tests;

public sealed class BrowserTests(E2EEnvironment environment)
{
    [Fact]
    public async Task E2E_SignIn_SeesProjects_SignOut_EndsSession()
    {
        E2E.Require();
        var page = await environment.SignInAsync(E2EEnvironment.Admin);
        await Expect(page.Locator("table")).ToBeVisibleAsync();

        // The sign-out POST redirects through the mock's end-session endpoint and back to "/": wait until it has left
        // the page, or the next navigation cancels it.
        await page.EvaluateAsync("document.querySelector('form[data-csla-signout]').submit()");
        await page.WaitForURLAsync(url => !url.Contains("/projects", StringComparison.Ordinal));
        await page.GotoAsync("/projects");

        await Expect(page.Locator("input[name=username]")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task E2E_TwoBrowsers_LiveUpdateAndPresence()
    {
        E2E.Require();
        var ada = await environment.SignInAsync(E2EEnvironment.Admin, "/projects/new");
        await CreateProjectAsync(ada, "E2E-LIVE", "Live project");

        var bea = await environment.SignInAsync(E2EEnvironment.Technical, new Uri(ada.Url).AbsolutePath);
        await Expect(ada.Locator("[data-csla-presence]")).ToContainTextAsync(E2EEnvironment.Technical);

        await ada.Locator("gcds-input[name=Name] input").FillAsync("Renamed live");
        await SaveAsync(ada);

        await Expect(bea.Locator("gcds-input[name=Name]")).ToHaveAttributeAsync("value", "Renamed live");
    }

    [Fact]
    public async Task E2E_LanguageSwitch_KeepsEdits()
    {
        E2E.Require();
        var page = await environment.SignInAsync(E2EEnvironment.Admin, "/projects/new");
        await CreateProjectAsync(page, "E2E-LANG", "Bilingual");

        await page.Locator("gcds-input[name=Name] input").FillAsync("Unsaved edit");
        await page.Locator("[data-csla-language]").ClickAsync();
        await Expect(page.Locator("html")).ToHaveAttributeAsync("lang", "fr-CA");
        await page.Locator("[data-csla-draft] gcds-button[data-action=restore]").ClickAsync();

        await Expect(page.Locator("gcds-input[name=Name]")).ToHaveAttributeAsync("value", "Unsaved edit");
    }

    // Every sample page (src/CslaStarter.Web/CLAUDE.md), each scanned once its content has rendered over the circuit.
    [Theory]
    [InlineData("/projects")]
    [InlineData("/projects/new")]
    [InlineData("a project")]
    [InlineData("/users")]
    [InlineData("a user")]
    [InlineData("/profile")]
    public async Task E2E_SamplePages_PassAxe(string target)
    {
        E2E.Require();
        var page = await environment.SignInAsync(E2EEnvironment.Admin, target switch
        {
            "a project" => "/projects/new",
            "a user" => $"/users/{environment.TechnicalUserId}",
            _ => target,
        });
        if (target == "a project")
        {
            // Codes are upper case (ProjectEdit's format rule); a lower-case code would leave the form on its error summary.
            await CreateProjectAsync(page, $"E2E-AXE-{Guid.NewGuid():N}"[..16].ToUpperInvariant(), "Accessible project");
        }

        await Expect(page.Locator("main form, main table").First).ToBeVisibleAsync();
        await Expect(page.Locator("[aria-busy=true]")).ToHaveCountAsync(0);

        var results = await page.RunAxe(new Deque.AxeCore.Commons.AxeRunOptions
        {
            RunOnly = new Deque.AxeCore.Commons.RunOnlyOptions { Type = "tag", Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] },
        });

        var serious = results.Violations.Where(v => v.Impact is "serious" or "critical").Select(v => $"{v.Id}: {v.Help}").ToList();
        Assert.True(serious.Count == 0, $"{target}: " + string.Join("; ", serious));
    }

    [Fact]
    public async Task E2E_SessionTimeout_WarnsThenSignsOut()
    {
        E2E.Require();
        var page = await environment.SignInAsync(E2EEnvironment.Admin, "/projects", environment.ShortSessionUrl);

        await Expect(page.Locator("[data-csla-session]")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await page.WaitForURLAsync(url => !url.Contains("/projects", StringComparison.Ordinal), new() { Timeout = 60_000 });
    }

    // Fills a new project's code and name, saves it, and waits for the page to move to the saved project.
    private static async Task CreateProjectAsync(IPage page, string code, string name)
    {
        await page.Locator("gcds-input[name=Code] input").FillAsync(code);
        await page.Locator("gcds-input[name=Name] input").FillAsync(name);
        await SaveAsync(page);
        await page.WaitForURLAsync("**/projects/*-*");
    }

    // Save is disabled while a rule is still checking (the code's uniqueness). Playwright treats a custom element's
    // disabled attribute as enabled, so wait for the attribute to go before clicking.
    private static async Task SaveAsync(IPage page)
    {
        var save = page.Locator("gcds-button[data-kind=Save]:not([disabled])");
        await Expect(save).ToBeVisibleAsync();
        await save.ClickAsync();
    }
}
