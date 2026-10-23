using System.Net.Http.Headers;
using System.Net.Http.Json;

// UI and API fixtures run at the same time, as in the nightly pipeline.
[assembly: Parallelizable(ParallelScope.Fixtures)]
[assembly: LevelOfParallelism(2)]

namespace Claims.Tests;

public static class TestSettings
{
    public static string BaseUrl { get; } = Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:5080";

    public const string StaffUser = "staff";
    public const string StaffPassword = "staff-local";
    public const string MemberPassword = "member-local";

    /// <summary>Gets an access token from the API's OAuth2 endpoint (password grant, local test users only).</summary>
    public static async Task<string> GetTokenAsync(string username, string password)
    {
        using var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        var response = await http.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password,
        }));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return body!.access_token;
    }

    /// <summary>An HttpClient for the API, signed in as the given user.</summary>
    public static async Task<HttpClient> ClientForAsync(string username, string password)
    {
        var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync(username, password));
        return http;
    }

    private record TokenResponse(string access_token, string token_type, int expires_in);
}

public record ClaimDto(string Id, string MemberId, string Provider, DateOnly ServiceDate, decimal Amount, string Status);
