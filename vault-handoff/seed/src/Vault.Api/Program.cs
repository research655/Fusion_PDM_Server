using Vault.Contracts;
using Vault.Api.Services;
using Vault.Domain.Abstractions;
using Vault.Domain.Exceptions;
using Vault.Infrastructure.Auth;
using Vault.Infrastructure.Data;
using Vault.Infrastructure.Notifications;
using Vault.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<VaultDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Vault")));

// Integration ports — local/stub defaults. TODO(Cursor): real Google + Asana (+ optional S3) in Phase 5.
builder.Services.Configure<FileStoreOptions>(builder.Configuration.GetSection("Storage:Local"));
builder.Services.AddScoped<IFileStore, LocalFileStore>();
builder.Services.AddScoped<IAuthProvider, StubAuthProvider>();
builder.Services.AddScoped<INotificationService, StubNotificationService>();

// Reference implementation (upload/check-out/submit/approve/reject worked; rest are 501 TODOs).
// StubVaultService is also in the repo if you want an all-501 baseline for contract tests.
builder.Services.AddScoped<IVaultService, VaultService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply EF migrations on startup (no-op until migrations exist). Lets container deploys
// self-provision the schema once Cursor adds the initial migration.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<VaultDbContext>().Database.Migrate();
}

// Map domain exceptions to the status codes documented in docs/openapi.yaml.
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = ex switch
        {
            DuplicateNameException or DuplicateNumberException or CheckoutConflictException => StatusCodes.Status409Conflict,
            ForbiddenActionException or UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            NotFoundException => StatusCodes.Status404NotFound,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status422UnprocessableEntity,
            NotImplementedException => StatusCodes.Status501NotImplemented,
            _ => StatusCodes.Status500InternalServerError
        };
        await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

app.UseSwagger();
app.UseSwaggerUI();

// TODO(Cursor): replace with real auth. For now the caller id comes from a header.
Guid Caller(HttpContext ctx) =>
    Guid.TryParse(ctx.Request.Headers["X-User-Id"].ToString(), out var id) ? id : Guid.Empty;

app.MapPost("/auth/google", async (GoogleAuthRequest req, IVaultService svc, CancellationToken ct) =>
    Results.Ok(await svc.AuthenticateGoogleAsync(req, ct)));

app.MapPost("/files/upload", async (HttpContext ctx, IVaultService svc, CancellationToken ct) =>
{
    var form = await ctx.Request.ReadFormAsync(ct);
    var file = form.Files["file"] ?? throw new ArgumentException("file is required.");
    var number = form["number"].ToString();
    var description = form["description"].ToString();
    var repository = form["repository"].ToString();   // optional; service defaults to the CAD vault
    await using var stream = file.OpenReadStream();
    var card = await svc.UploadAsync(stream, file.FileName, number, description, Caller(ctx), repository, ct);
    return Results.Created($"/files/{card.Id}", card);
});

app.MapGet("/files/{id:guid}", async (Guid id, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
    Results.Ok(await svc.GetAsync(id, Caller(ctx), ct)));

// Download current vault bytes to populate the local cache (read-only opens and checkout both use this).
app.MapGet("/files/{id:guid}/content", async (Guid id, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
{
    var dl = await svc.GetContentAsync(id, Caller(ctx), ct);
    return Results.File(dl.Content, "application/octet-stream", dl.FileName);
});

app.MapPost("/files/check-out", async (FileIdRequest req, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
{
    await svc.CheckOutAsync(req.FileId, Caller(ctx), ct);
    return Results.Ok();
});

// Check-in carries the edited working copy; it becomes the new vault content (403 if you don't hold the lock).
app.MapPost("/files/check-in", async (HttpContext ctx, IVaultService svc, CancellationToken ct) =>
{
    var form = await ctx.Request.ReadFormAsync(ct);
    if (!Guid.TryParse(form["fileId"].ToString(), out var fileId))
        throw new ArgumentException("fileId is required.");
    var file = form.Files["file"] ?? throw new ArgumentException("file (the working copy) is required.");
    await using var stream = file.OpenReadStream();
    await svc.CheckInAsync(fileId, stream, Caller(ctx), ct);
    return Results.Ok();
});

// EMERGENCY, Admin only: release another user's check-out, keeping the last vaulted state.
app.MapPost("/files/force-check-in", async (FileIdRequest req, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
    Results.Ok(await svc.ForceCheckInAsync(req.FileId, Caller(ctx), ct)));

app.MapPost("/files/submit-approval", async (FileIdRequest req, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
{
    await svc.SubmitForApprovalAsync(req.FileId, Caller(ctx), ct);
    return Results.Ok();
});

app.MapPost("/files/approve", async (FileIdRequest req, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
    Results.Ok(await svc.ApproveAsync(req.FileId, Caller(ctx), ct)));

app.MapPost("/files/reject", async (RejectRequest req, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
{
    await svc.RejectAsync(req.FileId, req.Reason, Caller(ctx), ct);
    return Results.Ok();
});

// "Back to Initial / Back to Under Change": undo a pending submission (submitter) OR return a rejected file to editable (Engineer/Admin).
app.MapPost("/files/return-to-editable", async (FileIdRequest req, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
    Results.Ok(await svc.ReturnToEditableAsync(req.FileId, Caller(ctx), ct)));

// Edit the data card (Number/filename/Description) — only while the caller holds the check-out.
app.MapPost("/files/{id:guid}/card", async (Guid id, UpdateCardRequest req, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
    Results.Ok(await svc.UpdateCardAsync(id, req, Caller(ctx), ct)));

// Admin/Engineer only (enforced in the service): move a Production file out of Production.
app.MapPost("/files/begin-change", async (FileIdRequest req, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
    Results.Ok(await svc.BeginChangeAsync(req.FileId, Caller(ctx), ct)));

app.MapPost("/files/obsolete", async (FileIdRequest req, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
    Results.Ok(await svc.MarkObsoleteAsync(req.FileId, Caller(ctx), ct)));

// Admin only: bring an Obsolete file back to Production.
app.MapPost("/files/reactivate", async (FileIdRequest req, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
    Results.Ok(await svc.ReactivateAsync(req.FileId, Caller(ctx), ct)));

// Prototype (Admin/Engineer, no approval/revision/history). Enter from Under Change (file must be checked in); exit back to Under Change.
app.MapPost("/files/prototype", async (FileIdRequest req, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
    Results.Ok(await svc.EnterPrototypeAsync(req.FileId, Caller(ctx), ct)));

app.MapPost("/files/prototype/exit", async (FileIdRequest req, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
    Results.Ok(await svc.ExitPrototypeAsync(req.FileId, Caller(ctx), ct)));

app.MapGet("/search", async ([AsParameters] SearchQuery query, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
    Results.Ok(await svc.SearchAsync(query, Caller(ctx), ct)));

// Admin-only. Service enforces the role and throws ForbiddenActionException otherwise.
app.MapGet("/files/{id:guid}/revisions", async (Guid id, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
    Results.Ok(await svc.ListRevisionsAsync(id, Caller(ctx), ct)));

app.MapPost("/files/rollback", async (RollbackRequest req, IVaultService svc, HttpContext ctx, CancellationToken ct) =>
    Results.Ok(await svc.RollbackAsync(req.FileId, req.TargetRevision, Caller(ctx), ct)));

app.Run();
