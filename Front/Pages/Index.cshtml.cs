using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.RazorPages;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using static ZipZap.Classes.Helpers.Constructors;
using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace ZipZap.Front.Pages;

public class IndexModel : PageModel {
    private readonly IFactory<IBackend, BackendConfiguration> _backendFactory;
    private readonly ILogger<IndexModel> _logger;
    private readonly ILoginSerivce _loginService;

    public IndexModel(IFactory<IBackend, BackendConfiguration> backendFactory, ILoginSerivce loginService, ILogger<IndexModel> logger) {
        _backendFactory = backendFactory;
        _loginService = loginService;
        _logger = logger;
    }
    public new User? User { get; set; }

    public async Task OnGet(CancellationToken cancellationToken) {
        var token = Request.Cookies[Constants.AUTHORIZATION];
        if (_logger.IsEnabled(LogLevel.Critical))
            _logger.LogCritical("Token: {}", token);
        if (token is null)
            return;

        var backend = _backendFactory.Create(new(token));
        User = await backend.GetSelf(cancellationToken) switch {
            Ok<User, ServiceError>(var user) => user,
            _ => null
        };
    }

    public async Task<IActionResult> OnPost() {
        var response = await _loginService.Login(Username, Password);
        if (response is Ok<string, LoginError>(var token)) {
            Error = null;
            Response.Cookies.Append(Constants.AUTHORIZATION, token);
            return Redirect("/");

        } else if (response is Err<string, LoginError>(var error)) {
            Error = error.ToString();
        }
        return Page();
    }

    public string? Error { get; set; } = null;

    [Required]
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [Required]
    [BindProperty]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
