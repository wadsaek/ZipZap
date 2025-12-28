using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Services;

namespace ZipZap.Front.Pages;

public class LoginModel : PageModel {
    private readonly ILoginSerivce _loginService;

    public LoginModel(ILoginSerivce loginService) {
        _loginService = loginService;
    }

    public User? ServiceUser { get; set; }
    public string Error { get; set; } = string.Empty;

    [BindProperty]
    public string Username { get; set; } = string.Empty;
    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public void OnGet() {
    }

    public async Task OnPost() {
        var response = await _loginService.Login(Username, Password);
        if (response is Ok<string, LoginError>(var token)) {
            Error = string.Empty;
            Response.Cookies.Append(Constants.AUTHORIZATION, token);
        } else
            Error = response.ToString();
    }

}
