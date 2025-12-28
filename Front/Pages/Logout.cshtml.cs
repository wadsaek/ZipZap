using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using ZipZap.Classes.Helpers;

namespace ZipZap.Front.Pages;

public class LogoutModel : PageModel {
    public IActionResult OnGet() {
        Response.Cookies.Delete(Constants.AUTHORIZATION);
        return Redirect("/");
    }
}
