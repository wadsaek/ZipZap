using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using static ZipZap.Classes.Helpers.Constructors;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using Microsoft.AspNetCore.Mvc;

namespace ZipZap.Front;

public class IndexModel : PageModel {
    required public string Path { get; set; }
    required public string Name { get; set; }
    public Option<Fso> Item { get; set; } = None<Fso>();
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
            .UnwrapOrElse(_ => None<Fso>()!);
        if (Item is Some<Fso>(Symlink { Target: var target }))
            return Redirect(target);
        return Page();
    }
}

