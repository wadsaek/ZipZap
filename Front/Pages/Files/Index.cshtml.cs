using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ZipZap.Front.Pages.Files;

public class IndexModel : PageModel {
    public IActionResult OnGet() {
        return Redirect("View");
    }
}
