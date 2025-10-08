using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using System;
using System.Threading;
using ZipZap.Classes;
using ZipZap.FileService.Helpers;
using ZipZap.FileService.Repositories;
using static ZipZap.FileService.Services.FilesStoringService;
using ZipZap.Classes.Helpers;
using Google.Protobuf.WellKnownTypes;

namespace ZipZap.FileService.Services;

public class FilesStoringServiceImpl : FilesStoringServiceBase {
    private readonly ILogger<FilesStoringServiceImpl> _logger;
    private readonly InterfaceIO _io;
    private readonly ISecurityHelper _securityHelper;
    private readonly IFsosRepository _fsosRepo;
    private readonly IConfiguration _config;

    public FilesStoringServiceImpl(
            ILogger<FilesStoringServiceImpl> logger,
            InterfaceIO io,
            ISecurityHelper securityHelper,
            IFsosRepository fsosRepo,
            IConfiguration config
) {
        _logger = logger;
        _io = io;
        _securityHelper = securityHelper;
        _fsosRepo = fsosRepo;
        _config = config;
    }

    private void ParseGuidOrThrow(string str, out Guid guid) {
        if (!Guid.TryParse(str, out guid))
            throw new RpcException(new(StatusCode.InvalidArgument, "Invalid guid"));
    }

    private T ThrowNotFoundIfNull<T>(Option<T> obj, string message = "Resource not found")
        => obj.UnwrapOrElse(() => throw new RpcException(new(StatusCode.NotFound, message)));

    private async Task<Fso> GetFsoOrFailAsync(string key, string ownerId, CancellationToken cancellationToken = default) {
        ParseGuidOrThrow(key, out var guid);
        ParseGuidOrThrow(ownerId, out var ownerIdGuid);
        var file = await _fsosRepo.GetByIdAsync(
                guid.ToFsoId(),
                cancellationToken
                );
        var fileInner = ThrowNotFoundIfNull(file, "Fso not found for this owner id");
        return fileInner;
    }

    public async Task<string> GenerateValidPathAsync() {
        string path;
        do {
            path = _securityHelper.GenerateString(10, _io.IsValidPathChar);
        } while (!_io.IsValidPath(path) || await _io.PathExistsAsync(path));
        return path;
    }
    public override async Task<EmptyMessage> DeleteFso(DeleteFsoRequest request, ServerCallContext context) {
        Fso fso = await GetFsoOrFailAsync(request.FsoId,Guid.Empty.ToString(),context.CancellationToken);
        if (fso is File file && await _io.PathExistsAsync(file.PhysicalPath)){
            await _io.RemoveAsync(file.PhysicalPath);
        }
        await _fsosRepo.DeleteAsync(fso);
        return new EmptyMessage{};
    }
    public override Task<SaveFileResponse> SaveFile(SaveFileRequest request, ServerCallContext context){
        return base.SaveFile(request,context);
    }
}
