using System.Net.Http.Json;
using System.Text.Json;
using Vault.Contracts;

namespace Vault.Client;

/// <summary>
/// Typed client over the Vault PDM API — one method per endpoint. Both the WPF app and the
/// Explorer client use this, so neither hand-rolls HTTP or redeclares DTOs.
/// Construct with an HttpClient whose BaseAddress is the API root (e.g. https://vault.lan:7000).
/// </summary>
public sealed class VaultApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Current user id, sent as X-User-Id. TODO: replace with a Bearer token once AuthenticateGoogleAsync issues one.</summary>
    public Guid? UserId { get; set; }

    public VaultApiClient(HttpClient http) => _http = http;

    // ---- auth ----
    public Task<AuthResponse> AuthenticateGoogleAsync(string code, CancellationToken ct = default)
        => PostAsync<GoogleAuthRequest, AuthResponse>("/auth/google", new GoogleAuthRequest(code), ct);

    // ---- content ----
    public async Task<FileCardDto> UploadAsync(Stream content, string fileName, string number, string description, string? repository = null, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent
        {
            { new StreamContent(content), "file", fileName },
            { new StringContent(number), "number" },
            { new StringContent(description), "description" }
        };
        if (!string.IsNullOrWhiteSpace(repository))
            form.Add(new StringContent(repository), "repository");   // omit -> server uses the CAD vault
        using var req = Request(HttpMethod.Post, "/files/upload", form);
        using var res = await _http.SendAsync(req, ct);
        return await ReadAsync<FileCardDto>(res, ct);
    }

    public Task<FileCardDto> GetAsync(Guid id, CancellationToken ct = default)
        => GetAsync<FileCardDto>($"/files/{id}", ct);

    /// <summary>Download the current vault bytes to populate the local cache. Caller owns the returned stream.</summary>
    public async Task<Stream> GetContentAsync(Guid id, CancellationToken ct = default)
    {
        using var req = Request(HttpMethod.Get, $"/files/{id}/content");
        var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureAsync(res, ct);
        return await res.Content.ReadAsStreamAsync(ct);
    }

    public Task CheckOutAsync(Guid fileId, CancellationToken ct = default)
        => PostAsync("/files/check-out", new FileIdRequest(fileId), ct);

    /// <summary>Check in the edited working copy. It becomes the new vault content (revision unchanged).</summary>
    public async Task CheckInAsync(Guid fileId, Stream content, string fileName, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(fileId.ToString()), "fileId" },
            { new StreamContent(content), "file", fileName }
        };
        using var req = Request(HttpMethod.Post, "/files/check-in", form);
        using var res = await _http.SendAsync(req, ct);
        await EnsureAsync(res, ct);
    }

    // ---- workflow ----
    public Task SubmitForApprovalAsync(Guid fileId, CancellationToken ct = default)
        => PostAsync("/files/submit-approval", new FileIdRequest(fileId), ct);

    public Task<FileCardDto> ApproveAsync(Guid fileId, CancellationToken ct = default)
        => PostAsync<FileIdRequest, FileCardDto>("/files/approve", new FileIdRequest(fileId), ct);

    public Task RejectAsync(Guid fileId, string? reason, CancellationToken ct = default)
        => PostAsync("/files/reject", new RejectRequest(fileId, reason), ct);

    public Task<FileCardDto> BeginChangeAsync(Guid fileId, CancellationToken ct = default)
        => PostAsync<FileIdRequest, FileCardDto>("/files/begin-change", new FileIdRequest(fileId), ct);

    public Task<FileCardDto> MarkObsoleteAsync(Guid fileId, CancellationToken ct = default)
        => PostAsync<FileIdRequest, FileCardDto>("/files/obsolete", new FileIdRequest(fileId), ct);

    public Task<FileCardDto> ReactivateAsync(Guid fileId, CancellationToken ct = default)
        => PostAsync<FileIdRequest, FileCardDto>("/files/reactivate", new FileIdRequest(fileId), ct);

    public Task<FileCardDto> ReturnToEditableAsync(Guid fileId, CancellationToken ct = default)
        => PostAsync<FileIdRequest, FileCardDto>("/files/return-to-editable", new FileIdRequest(fileId), ct);

    public Task<FileCardDto> UpdateCardAsync(Guid fileId, UpdateCardRequest req, CancellationToken ct = default)
        => PostAsync<UpdateCardRequest, FileCardDto>($"/files/{fileId}/card", req, ct);

    public Task<FileCardDto> ForceCheckInAsync(Guid fileId, CancellationToken ct = default)
        => PostAsync<FileIdRequest, FileCardDto>("/files/force-check-in", new FileIdRequest(fileId), ct);

    public Task<FileCardDto> EnterPrototypeAsync(Guid fileId, CancellationToken ct = default)
        => PostAsync<FileIdRequest, FileCardDto>("/files/prototype", new FileIdRequest(fileId), ct);

    public Task<FileCardDto> ExitPrototypeAsync(Guid fileId, CancellationToken ct = default)
        => PostAsync<FileIdRequest, FileCardDto>("/files/prototype/exit", new FileIdRequest(fileId), ct);

    // ---- search & admin ----
    public Task<IReadOnlyList<FileCardDto>> SearchAsync(SearchQuery query, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<FileCardDto>>("/search" + ToQueryString(query), ct);

    public Task<IReadOnlyList<RevisionBackupDto>> ListRevisionsAsync(Guid fileId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<RevisionBackupDto>>($"/files/{fileId}/revisions", ct);

    public Task<FileCardDto> RollbackAsync(Guid fileId, string targetRevision, CancellationToken ct = default)
        => PostAsync<RollbackRequest, FileCardDto>("/files/rollback", new RollbackRequest(fileId, targetRevision), ct);

    // ---- helpers ----
    private HttpRequestMessage Request(HttpMethod method, string url, HttpContent? content = null)
    {
        var req = new HttpRequestMessage(method, url) { Content = content };
        if (UserId is { } id) req.Headers.Add("X-User-Id", id.ToString());
        return req;
    }

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var req = Request(HttpMethod.Get, url);
        using var res = await _http.SendAsync(req, ct);
        return await ReadAsync<T>(res, ct);
    }

    private async Task PostAsync<TReq>(string url, TReq body, CancellationToken ct)
    {
        using var req = Request(HttpMethod.Post, url, JsonContent.Create(body, options: Json));
        using var res = await _http.SendAsync(req, ct);
        await EnsureAsync(res, ct);
    }

    private async Task<TRes> PostAsync<TReq, TRes>(string url, TReq body, CancellationToken ct)
    {
        using var req = Request(HttpMethod.Post, url, JsonContent.Create(body, options: Json));
        using var res = await _http.SendAsync(req, ct);
        return await ReadAsync<TRes>(res, ct);
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage res, CancellationToken ct)
    {
        await EnsureAsync(res, ct);
        return (await res.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    private static async Task EnsureAsync(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;
        var message = res.ReasonPhrase ?? res.StatusCode.ToString();
        try
        {
            var err = await res.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            if (!string.IsNullOrWhiteSpace(err?.Error)) message = err!.Error;
        }
        catch { /* body was not JSON */ }
        throw new VaultApiException(res.StatusCode, message);
    }

    private static string ToQueryString(SearchQuery q)
    {
        var parts = new List<string>();
        void Add(string k, string? v) { if (!string.IsNullOrWhiteSpace(v)) parts.Add($"{k}={Uri.EscapeDataString(v)}"); }
        Add("number", q.Number);
        Add("description", q.Description);
        Add("revision", q.Revision);
        Add("state", q.State);
        Add("designer", q.Designer);
        Add("repository", q.Repository);
        Add("createdFrom", q.CreatedFrom?.ToString("yyyy-MM-dd"));
        Add("createdTo", q.CreatedTo?.ToString("yyyy-MM-dd"));
        Add("updatedFrom", q.UpdatedFrom?.ToString("yyyy-MM-dd"));
        Add("updatedTo", q.UpdatedTo?.ToString("yyyy-MM-dd"));
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    private sealed record ApiError(string Error);
}
