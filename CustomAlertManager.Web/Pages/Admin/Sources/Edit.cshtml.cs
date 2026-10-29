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
public sealed class EditModel(AlertDbContext db, NewsSourceService sources) : PageModel
{
    [BindProperty]
    public SourceForm Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var source = await db.NewsSources.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (source is null)
        {
            return NotFound();
        }

        Input = new SourceForm { Name = source.Name, Url = source.Url };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        var source = await db.NewsSources.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (source is null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            foreach (var (field, message) in await sources.ValidateAsync(Input.Url, id, cancellationToken))
            {
                ModelState.AddModelError(field, message);
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await sources.UpdateAsync(source, Input.Name, Input.Url, cancellationToken);
        TempData["Status"] = $"Source '{Input.Name.Trim()}' saved.";
        return RedirectToPage("Index");
    }

    public sealed class SourceForm
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(450)]
        public string Url { get; set; } = string.Empty;
    }
}
