using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using Microsoft.AspNetCore.Mvc;

namespace ZipZap.Front;

public class IndexModel : PageModel {
    required public string Path { get; set; }

    public Fso? Item { get; set; }
    public string? Text { get; set; }
    private readonly ILogger<IndexModel> _logger;
    private readonly IFactory<IBackend, BackendConfiguration> _backendFactory;

    public IndexModel(ILogger<IndexModel> logger, IFactory<IBackend, BackendConfiguration> backendFactory) {
        _logger = logger;
        _backendFactory = backendFactory;
    }

    public async Task<IActionResult> OnGetAsync(string? path) {
        Path = path ?? "/";
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Path: {path}", Path);
        var token = Request.Cookies[Constants.AUTHORIZATION];
        if (token is null)
            return NotFound();
        var backend = _backendFactory.Create(new(token));
        Item = (await backend.GetFsoByPathAsync(new PathDataWithPath(Path)))
            .UnwrapOr(null!);
        if (Item is Symlink { Target: var target })
            return Redirect(target);
        else if (Item is File { Content: var bytes }) {

        }
        return Page();
    }
}

