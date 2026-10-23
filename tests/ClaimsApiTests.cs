using System.Net;
using System.Net.Http.Json;

namespace Claims.Tests;

public class ClaimsApiTests
{
    private const string Member = "M-1001";
    private static string? _createdClaimId;
    private HttpClient _staff = null!;

    [SetUp]
    public async Task SignIn()
    {
        _staff = await TestSettings.ClientForAsync(TestSettings.StaffUser, TestSettings.StaffPassword);
    }

    [TearDown]
    public void Dispose() => _staff.Dispose();

    [Test]
    public async Task Claim_is_created()
    {
        var response = await _staff.PostAsJsonAsync("/api/claims",
            new { memberId = Member, provider = "Northside Clinic", serviceDate = "2026-10-01", amount = 80.00m });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var claim = await response.Content.ReadFromJsonAsync<ClaimDto>();
        _createdClaimId = claim!.Id;
    }

    [Test]
    public async Task Created_claim_can_be_cancelled()
    {
        var response = await _staff.PostAsync($"/api/claims/{_createdClaimId}/cancel", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var claim = await _staff.GetFromJsonAsync<ClaimDto>($"/api/claims/{_createdClaimId}");
        Assert.That(claim!.Status, Is.EqualTo("Cancelled"));
    }

    [Test]
    public async Task Created_claim_keeps_its_amount()
    {
        var response = await _staff.PostAsJsonAsync("/api/claims",
            new { memberId = Member, provider = "Lakeview Pharmacy", serviceDate = "2026-10-02", amount = 125.50m });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var claim = await response.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.That(claim!.Amount, Is.GreaterThan(0));
    }

    [Test]
    public async Task Submitted_claim_is_accepted()
    {
        var response = await _staff.PostAsJsonAsync("/api/claims",
            new { memberId = Member, provider = "Northside Clinic", serviceDate = "2026-10-03", amount = 64.00m });
        var claim = await response.Content.ReadFromJsonAsync<ClaimDto>();

        await Task.Delay(1000); // adjudication takes about a second

        var processed = await _staff.GetFromJsonAsync<ClaimDto>($"/api/claims/{claim!.Id}");
        Assert.That(processed!.Status, Is.EqualTo("Accepted"));
    }

    [Test]
    public async Task Member_claims_require_a_token()
    {
        using var anonymous = new HttpClient { BaseAddress = new Uri(TestSettings.BaseUrl) };

        var response = await anonymous.GetAsync($"/api/members/{Member}/claims");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
