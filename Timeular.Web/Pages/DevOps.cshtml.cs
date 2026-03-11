using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Timeular.Web.Pages;

public class DevOpsModel : PageModel
{
    [BindProperty]
    public string Action { get; set; } = string.Empty;

    [BindProperty]
    public string Organization { get; set; } = string.Empty;

    [BindProperty]
    public string Project { get; set; } = string.Empty;

    public List<string>? WorkItems { get; set; }

    public void OnGet(string action)
    {
        Action = action;
    }

    public IActionResult OnPostList()
    {
        // TODO: query Azure DevOps API using Organization/Project
        WorkItems = new List<string> { "SampleItem1", "SampleItem2" };
        return Page();
    }
}