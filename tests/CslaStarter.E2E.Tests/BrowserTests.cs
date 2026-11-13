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

        await page.EvaluateAsync("document.querySelector('form[data-csla-signout]').submit()");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.GotoAsync("/projects");

        await Expect(page.Locator("input[name=username]")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task E2E_TwoBrowsers_LiveUpdateAndPresence()
    {
        E2E.Require();
        var ada = await environment.SignInAsync(E2EEnvironment.Admin, "/projects/new");
        await ada.Locator("gcds-input[name=Code] input").FillAsync("E2E-LIVE");
        await ada.Locator("gcds-input[name=Name] input").FillAsync("Live project");
        await ada.Locator("gcds-button[data-kind=Save]").ClickAsync();
        await ada.WaitForURLAsync("**/projects/*-*");
        var url = ada.Url;

        var bea = await environment.SignInAsync(E2EEnvironment.Technical, new Uri(url).AbsolutePath);
        await Expect(ada.Locator("[data-csla-presence]")).ToContainTextAsync(E2EEnvironment.Technical);

        await ada.Locator("gcds-input[name=Name] input").FillAsync("Renamed live");
        await ada.Locator("gcds-button[data-kind=Save]").ClickAsync();

        await Expect(bea.Locator("gcds-input[name=Name]")).ToHaveAttributeAsync("value", "Renamed live");
    }

    [Fact]
    public async Task E2E_LanguageSwitch_KeepsEdits()
    {
        E2E.Require();
        var page = await environment.SignInAsync(E2EEnvironment.Admin, "/projects/new");
        await page.Locator("gcds-input[name=Code] input").FillAsync("E2E-LANG");
        await page.Locator("gcds-input[name=Name] input").FillAsync("Bilingual");
        await page.Locator("gcds-button[data-kind=Save]").ClickAsync();
        await page.WaitForURLAsync("**/projects/*-*");

        await page.Locator("gcds-input[name=Name] input").FillAsync("Unsaved edit");
        await page.Locator("[data-csla-language]").ClickAsync();
        await Expect(page.Locator("html")).ToHaveAttributeAsync("lang", "fr-CA");
        await page.Locator("[data-csla-draft] gcds-button[data-action=restore]").ClickAsync();

        await Expect(page.Locator("gcds-input[name=Name]")).ToHaveAttributeAsync("value", "Unsaved edit");
    }

    [Theory]
    [InlineData("/projects")]
    [InlineData("/projects/new")]
    [InlineData("/users")]
    [InlineData("/profile")]
    public async Task E2E_SamplePages_PassAxe(string path)
    {
        E2E.Require();
        var page = await environment.SignInAsync(E2EEnvironment.Admin, path);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var results = await page.RunAxe(new Deque.AxeCore.Commons.AxeRunOptions
        {
            RunOnly = new Deque.AxeCore.Commons.RunOnlyOptions { Type = "tag", Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] },
        });

        var serious = results.Violations.Where(v => v.Impact is "serious" or "critical").Select(v => $"{v.Id}: {v.Help}").ToList();
        Assert.True(serious.Count == 0, $"{path}: " + string.Join("; ", serious));
    }

    [Fact]
    public async Task E2E_SessionTimeout_WarnsThenSignsOut()
    {
        E2E.Require();
        var page = await environment.SignInAsync(E2EEnvironment.Admin);

        await Expect(page.Locator("[data-csla-session]")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await page.WaitForURLAsync(url => !url.Contains("/projects", StringComparison.Ordinal), new() { Timeout = 60_000 });
    }
}
