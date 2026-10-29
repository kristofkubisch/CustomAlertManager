using System.ComponentModel.DataAnnotations;
using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Web.Pages.Admin.Sources;

[Authorize(Roles = "Admin")]
public sealed class IndexModel(AlertDbContext db, NewsSourceService sources) : PageModel
{
    [BindProperty]
    public SourceInput Input { get; set; } = new();

    public List<SourceRow> Rows { get; private set; } = [];

    // Set when the add form was submitted with errors, so the modal opens again showing them.
    public bool ShowAddModal { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Rows = await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostAddAsync(CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            foreach (var (field, message) in await sources.ValidateAsync(Input.Url, null, cancellationToken))
            {
                ModelState.AddModelError(field, message);
            }
        }

        if (!ModelState.IsValid)
        {
            Rows = await LoadAsync(cancellationToken);
            ShowAddModal = true;
            return Page();
        }

        await sources.CreateAsync(Input.Name, Input.Url, cancellationToken);
        TempData["Status"] = $"Source '{Input.Name.Trim()}' added.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, CancellationToken cancellationToken)
    {
        var source = await db.NewsSources.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (source is null)
        {
            return NotFound();
        }

        await sources.SetActiveAsync(source, !source.IsActive, cancellationToken);
        TempData["Status"] = $"Source '{source.Name}' {(source.IsActive ? "activated" : "deactivated")}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        var source = await db.NewsSources.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (source is null)
        {
            return NotFound();
        }

        if (await sources.DeleteAsync(source, cancellationToken))
        {
            TempData["Status"] = $"Source '{source.Name}' deleted.";
        }
        else
        {
            // Subscriptions still use it, so it is switched off rather than removed.
            await sources.SetActiveAsync(source, false, cancellationToken);
            TempData["Error"] = $"'{source.Name}' is used by subscriptions, so it was deactivated instead of deleted.";
        }

        return RedirectToPage();
    }

    private Task<List<SourceRow>> LoadAsync(CancellationToken cancellationToken) =>
        db.NewsSources
            .OrderBy(source => source.Name)
            .Select(source => new SourceRow(
                source.Id,
                source.Name,
                source.Url,
                source.IsActive,
                db.SubscriptionSources.Count(link => link.NewsSourceId == source.Id)))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public sealed record SourceRow(int Id, string Name, string Url, bool IsActive, int SubscriptionCount);

    public sealed class SourceInput
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(450)]
        public string Url { get; set; } = string.Empty;
    }
}
