
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using ZipZap.Front.Handlers.Files.View;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using ZipZap.Classes.Helpers;
using System.ComponentModel;
using ZipZap.Front.Handlers;
using System;
using ZipZap.Classes.Extensions;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Pages.Files;

using GetError = GetHandler.GetHandlerError;
using DeleteError = DeleteHandler.DeleteHandlerError;

public class FileViewModel : PageModel {

    private readonly ILogger<GetHandler> _logger;
    private readonly IFactory<IBackend, BackendConfiguration> _backendFactory;

    public FileViewModel(ILogger<GetHandler> logger, IFactory<IBackend, BackendConfiguration> backendFactory) {
        _logger = logger;
        _backendFactory = backendFactory;
    }
    public GetHandler? GetHandler { get; set; }

    public async Task<IActionResult> OnGetAsync(string path, [FromQuery] IdType type)
        => await GetHandler.OnGetAsync(
                new(path, type),
                Request, _logger, _backendFactory
            ) switch {
                Ok<GetHandler, GetError>(var handler) => SetupHandler(handler),
                Err<GetHandler, GetError>(GetError.ShouldRedirect(var target)) => Redirect(target),
                Err<GetHandler, GetError>(GetError.BadRequest) => BadRequest(),
                Err<GetHandler, GetError>(GetError.NotFound) => NotFound(),
                _ => throw new InvalidEnumArgumentException()

            };

    private PageResult SetupHandler(GetHandler handler) {
        GetHandler = handler;
        return Page();
    }

    public async Task<IActionResult> OnPostDelete([FromRoute] Guid path) {
        return await DeleteHandler.OnDeleteAsync(path.ToFsoId(), Request, _backendFactory) switch {

            Ok<Unit, DeleteError> => Redirect("."),
            Err<Unit, DeleteError>(DeleteError.Internal) => ReportInternalAndRedirect(),
            Err<Unit, DeleteError>(DeleteError.BadRequest) => BadRequest(),
            Err<Unit, DeleteError>(DeleteError.NotFound) => NotFound(),
            _ => throw new InvalidEnumArgumentException()
        };
    }
    private RedirectResult ReportInternalAndRedirect() {

        if (_logger.IsEnabled(LogLevel.Critical))
            _logger.LogCritical("Internal error Found");
        return Redirect(".");

    }
}

