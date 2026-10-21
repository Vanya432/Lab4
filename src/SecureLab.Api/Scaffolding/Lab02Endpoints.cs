using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;
using Microsoft.AspNetCore.Identity;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    public static void MapLab02Endpoints(this WebApplication app)
    {
        app.MapGet("/api/incidents/search", async (string? q, string? sortBy, SecureLabDbContext db, CancellationToken ct) =>
        {
            if (sortBy is not (null or "" or "createdAtUtc" or "severity" or "status"))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["sortBy"] = ["Allowed values: createdAtUtc, severity, status."]
                    });
            }
            var search = (q ?? "")
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

            var query = db.Incidents
            .AsNoTracking()
            .Where(i =>
            EF.Functions.ILike(i.Title, "%" + search + "%") ||
            EF.Functions.ILike(i.Description, "%" + search + "%"));

            var sortedQuery = sortBy switch
            {
                "severity" => query
                .OrderBy(i => i.Severity == IncidentSeverity.Critical ? 0 :
                i.Severity == IncidentSeverity.High ? 1 :
                i.Severity == IncidentSeverity.Medium ? 2 : 3)
                .ThenBy(i => i.Id),

                "status" => query
                .OrderBy(i => i.Status == IncidentStatus.New ? 0 :
                i.Status == IncidentStatus.Triaged ? 1 :
                i.Status == IncidentStatus.InProgress ? 2 :
                i.Status == IncidentStatus.Resolved ? 3 : 4)
                .ThenBy(i => i.Id),
                
                _=> query
                .OrderByDescending(i => i.CreatedAtUtc)
                .ThenBy(i => i.Id)
            };

            var rows = await sortedQuery
            .Take(50)
            .ToListAsync(ct);

            return Results.Ok(rows.Select(row => new
            {
                row.Id, row.Title, row.Description,
                Severity = row.Severity.ToString(), Status = row.Status.ToString(), row.CreatedAtUtc
            }));
        });
        app.MapPost("/api/incidents", async (CreateIncidentRequest request, SecureLabDbContext db, HttpContext context, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            // ЛР 02: доповніть server validation, business rules та response contract.
            var currentUser = await users.GetUserAsync(context.User);
            if (currentUser is null)
                return Results.Unauthorized();
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(request.Title))
                errors["title"] = ["Title is required."];
            else if (request.Title.Length > 160)
                errors["title"] = ["Max title length is 160 chars"];

            if (string.IsNullOrWhiteSpace(request.Description))
                errors["description"] = ["Description is required."];
            else if (request.Description.Length > 4000)
                errors["description"] = ["Max description length is 4000 chars"];

            IncidentSeverity severity = default;

            if (string.IsNullOrWhiteSpace(request.Severity)
            || !Enum.TryParse<IncidentSeverity>(
                request.Severity, ignoreCase: true, out severity)
            || !Enum.IsDefined(severity))
            {
                errors["severity"] = ["Allowed values: Low, Medium, High, Critical."];
            }

            var now = DateTimeOffset.UtcNow;
            if (request.OccurredAtUtc is null)
            {
                errors["occurredAtUtc"] = ["OccurredAtUtc is required."];
            }
            else if (request.OccurredAtUtc.Value > now.AddMinutes(5))
            {
                errors["occurredAtUtc"] = ["Date cannot be more than 5 minutes ahead of server UTC time."];
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var incident = new Incident
            {
                Id = Guid.NewGuid(),
                OwnerUserId = currentUser.Id,
                Title = request.Title!.Trim(),
                Description = request.Description!.Trim(),
                Severity = severity,
                Status = IncidentStatus.New,
                OccurredAtUtc = request.OccurredAtUtc!.Value.ToUniversalTime(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.Incidents.Add(incident);
            await db.SaveChangesAsync(ct);

            var response = new CreatedIncidentResponse(
                incident.Id,
                incident.Title,
                incident.Description,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc);
            return Results.Created($"/api/incidents/{incident.Id}", response);
        }).RequireAuthorization();
    }
}


