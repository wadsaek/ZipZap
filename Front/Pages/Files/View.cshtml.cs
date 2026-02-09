
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
using System.Threading;

using ZipZap.Classes.Extensions;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Pages.Files;

using GetError = GetHandler.GetHandlerError;
using DeleteError = DeleteHandler.DeleteHandlerError;

public class FileViewModel : PageModel {

    private readonly ILogger<FileViewModel> _logger;
    private readonly IFactory<IBackend, BackendConfiguration> _backendFactory;

    public FileViewModel(ILogger<FileViewModel> logger, IFactory<IBackend, BackendConfiguration> backendFactory, IGetHandler getHandler) {
        _logger = logger;
        _backendFactory = backendFactory;
        _getHandler = getHandler;
    }

    private IGetHandler _getHandler; 

    public async Task<IActionResult> OnGetAsync(string path, [FromQuery] IdType type,CancellationToken cancellationToken)
        => await _getHandler.OnGetAsync(
                new(path, type),    Request, cancellationToken
            ) switch {
                Ok<GetHandler.GetHandlerResult, GetError>(var handler) => SetupHandler(handler),
                Err<GetHandler.GetHandlerResult, GetError>(GetError.ShouldRedirect(var target)) => Redirect(target),
                Err<GetHandler.GetHandlerResult, GetError>(GetError.BadRequest) => BadRequest(),
                Err<GetHandler.GetHandlerResult, GetError>(GetError.NotFound) => NotFound(),
                _ => throw new InvalidEnumArgumentException()

            };

    private PageResult SetupHandler(GetHandler.GetHandlerResult handler) {
        Result = handler;
        return Page();
    }

    public GetHandler.GetHandlerResult? Result { get; set; }

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

