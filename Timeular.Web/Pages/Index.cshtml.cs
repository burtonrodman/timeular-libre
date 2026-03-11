using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Timeular.Web.Pages;

public class IndexModel : PageModel
{
    [BindProperty]
    public string Action { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public IActionResult OnPostRecord()
    {
        if (!string.IsNullOrEmpty(Action))
        {
            // TODO: record action (e.g. save to database or telemetry)
            Console.WriteLine($"Action recorded: {Action}");
        }
        return Page();
    }
}
