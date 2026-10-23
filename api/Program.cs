// Claims Exchange API: a small stand-in for the healthcare claims service the regression suite covers.
// Data lives in memory and resets when the API restarts.
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var store = new ClaimStore();
var tokens = new ConcurrentDictionary<string, Caller>();

// Simplified OAuth2 password grant, local only. Members sign in with their id; staff with "staff".
app.MapPost("/oauth/token", (HttpRequest request) =>
{
    var form = request.Form;
    if (form["grant_type"] != "password")
        return Results.BadRequest(new { error = "unsupported_grant_type" });

    var username = form["username"].ToString();
    var password = form["password"].ToString();
    Caller? caller = username == "staff" && password == "staff-local" ? new Caller(username, IsStaff: true)
        : store.Members.ContainsKey(username) && password == "member-local" ? new Caller(username, IsStaff: false)
        : null;
    if (caller is null)
        return Results.BadRequest(new { error = "invalid_grant" });

    var token = Guid.NewGuid().ToString("N");
    tokens[token] = caller;
    return Results.Ok(new { access_token = token, token_type = "Bearer", expires_in = 900 });
});

var api = app.MapGroup("/api");

// Every /api call needs a valid bearer token.
api.AddEndpointFilter(async (context, next) =>
{
    var header = context.HttpContext.Request.Headers.Authorization.ToString();
    var token = header.StartsWith("Bearer ") ? header["Bearer ".Length..] : "";
    if (!tokens.TryGetValue(token, out var caller))
        return Results.Unauthorized();
    context.HttpContext.Items["caller"] = caller;
    return await next(context);
});

api.MapGet("/members/{memberId}", async (string memberId) =>
{
    await Task.Delay(Random.Shared.Next(100, 650)); // the member directory is a slow downstream service
    return store.Members.TryGetValue(memberId, out var name)
        ? Results.Ok(new { id = memberId, name })
        : Results.NotFound();
});

api.MapGet("/members/{memberId}/claims", async (string memberId) =>
{
    await Task.Delay(Random.Shared.Next(100, 300));
    if (!store.Members.ContainsKey(memberId))
        return Results.NotFound();
    return Results.Ok(store.ForMember(memberId));
});

api.MapGet("/claims/{claimId}", (string claimId) =>
    store.Find(claimId) is { } claim ? Results.Ok(claim) : Results.NotFound());

api.MapPost("/claims", (ClaimRequest request) =>
{
    if (!store.Members.ContainsKey(request.MemberId))
        return Results.BadRequest(new { error = "unknown member" });
    if (request.Amount <= 0)
        return Results.BadRequest(new { error = "amount must be positive" });

    var claim = store.Add(request.MemberId, request.Provider, request.ServiceDate, Math.Round(request.Amount));

    // Adjudication runs in the background; the claim moves from Submitted to Accepted when it finishes.
    _ = Task.Run(async () =>
    {
        await Task.Delay(Random.Shared.Next(300, 1400));
        store.SetStatus(claim.Id, "Accepted", onlyFrom: "Submitted");
    });

    return Results.Created($"/api/claims/{claim.Id}", claim);
});

api.MapPost("/claims/{claimId}/cancel", (string claimId) =>
{
    var claim = store.Find(claimId);
    if (claim is null)
        return Results.NotFound();
    if (claim.Status == "Cancelled")
        return Results.Conflict(new { error = "claim already cancelled" });
    return Results.Ok(store.SetStatus(claimId, "Cancelled"));
});

app.Run();

record Caller(string Id, bool IsStaff);

record ClaimRequest(string MemberId, string Provider, DateOnly ServiceDate, decimal Amount);

record Claim(string Id, string MemberId, string Provider, DateOnly ServiceDate, decimal Amount, string Status);

class ClaimStore
{
    private readonly object _lock = new();
    private readonly List<Claim> _claims =
    [
        new("CLM-5001", "M-1001", "Northside Clinic", new DateOnly(2026, 8, 4), 120.00m, "Accepted"),
        new("CLM-5002", "M-1001", "Lakeview Pharmacy", new DateOnly(2026, 8, 19), 42.75m, "Accepted"),
        new("CLM-5003", "M-1001", "Northside Clinic", new DateOnly(2026, 9, 2), 310.40m, "Submitted"),
        new("CLM-5004", "M-1002", "Riverside Imaging", new DateOnly(2026, 7, 28), 890.00m, "Accepted"),
        new("CLM-5005", "M-1002", "Lakeview Pharmacy", new DateOnly(2026, 9, 11), 18.20m, "Rejected"),
        new("CLM-5006", "M-1003", "Hillcrest Dental", new DateOnly(2026, 9, 15), 210.00m, "Accepted"),
    ];
    private int _nextId = 5007;

    public IReadOnlyDictionary<string, string> Members { get; } = new Dictionary<string, string>
    {
        ["M-1001"] = "Ana Ruiz",
        ["M-1002"] = "Ben Carter",
        ["M-1003"] = "Carla Gomez",
    };

    public List<Claim> ForMember(string memberId)
    {
        lock (_lock) return _claims.Where(c => c.MemberId == memberId).ToList();
    }

    public Claim? Find(string claimId)
    {
        lock (_lock) return _claims.FirstOrDefault(c => c.Id == claimId);
    }

    public Claim Add(string memberId, string provider, DateOnly serviceDate, decimal amount)
    {
        lock (_lock)
        {
            var claim = new Claim($"CLM-{_nextId++}", memberId, provider, serviceDate, amount, "Submitted");
            _claims.Add(claim);
            return claim;
        }
    }

    public Claim? SetStatus(string claimId, string status, string? onlyFrom = null)
    {
        lock (_lock)
        {
            var index = _claims.FindIndex(c => c.Id == claimId);
            if (index < 0 || (onlyFrom is not null && _claims[index].Status != onlyFrom))
                return null;
            _claims[index] = _claims[index] with { Status = status };
            return _claims[index];
        }
    }
}
