using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Claims.Tests;

public class ClaimsPageTests : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { BaseURL = TestSettings.BaseUrl };

    [Test]
    public async Task Member_claims_table_lists_all_claims()
    {
        await Page.GotoAsync("/");
        await Page.FillAsync("#member-id", "M-1001");
        await Page.ClickAsync("button[type=submit]");

        // Ana Ruiz (M-1001) has three claims in the test data.
        await Expect(Page.Locator("#claims tbody tr")).ToHaveCountAsync(3);
    }

    [Test]
    public async Task Search_shows_member_name()
    {
        await Page.GotoAsync("/");
        await Page.FillAsync("#member-id", "M-1003");
        await Page.ClickAsync("button[type=submit]");

        await Task.Delay(500);
        var name = await Page.InnerTextAsync("#member-name");

        Assert.That(name, Is.EqualTo("Carla Gomez"));
    }
}
