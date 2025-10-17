using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using System;
using System.Threading;
using ZipZap.Classes;
using ZipZap.FileService.Helpers;
using ZipZap.FileService.Repositories;
using ZipZap.Grpc;
using static ZipZap.Classes.Helpers.OptionExt;
using ZipZap.Classes.Helpers;
using System.Diagnostics.CodeAnalysis;
using static Grpc.Core.Metadata;
using System.IO;
using Directory = ZipZap.Classes.Directory;
using File = ZipZap.Classes.File;
using ZipZap.FileService.Models;
using ZipZap.Classes.Extensions;

namespace ZipZap.FileService.Services;

public class FilesStoringServiceImpl : FilesStoringService.FilesStoringServiceBase {
    private readonly ILogger<FilesStoringServiceImpl> _logger;
    private readonly InterfaceIO _io;
    private readonly ISecurityHelper _securityHelper;
    private readonly IFsosRepository _fsosRepo;
    private readonly IConfiguration _config;
    private readonly IUserService _userService;

    public FilesStoringServiceImpl(
            ILogger<FilesStoringServiceImpl> logger,
            InterfaceIO io,
            ISecurityHelper securityHelper,
            IFsosRepository fsosRepo,
            IConfiguration config,
            IUserService userService
) {
        _logger = logger;
        _io = io;
        _securityHelper = securityHelper;
        _fsosRepo = fsosRepo;
        _config = config;
        _userService = userService;
    }

    /// typeparam name="T"
    /// Does not change the behaviour in any way
    [DoesNotReturn]
    private static T ThrowUnauthenticated<T>(string detail = "Unauthenticated")
        => throw new RpcException(new(StatusCode.Unauthenticated, detail));

    private static void ParseGuidOrThrow(string str, out Guid guid) {
        if (!Guid.TryParse(str, out guid))
            throw new RpcException(new(StatusCode.InvalidArgument, "Invalid guid"));
    }

    private static T ThrowNotFoundIfNull<T>(Option<T> obj, string message = "Resource not found")
        => obj.UnwrapOrElse(() => throw new RpcException(new(StatusCode.NotFound, message)));

    private Func<Fso, Task<bool>> OwnedBy(User owner) =>
            async fso => (await _fsosRepo.GetRootDirectory(fso.Id)).IsSomeAnd(fso => fso.Id == owner.Root.Id);


    private Task<Fso> GetFsoOrFailAsync(string key, User owner, CancellationToken cancellationToken = default)
        => GetFsoOrFailAsync(key, owner, null, cancellationToken);

    private async Task<Fso> GetFsoOrFailAsync(string key, User owner, Func<Fso, bool>? predicate = null, CancellationToken cancellationToken = default) {
        ParseGuidOrThrow(key, out var guid);
        var file = await _fsosRepo.GetByIdAsync(
                guid.ToFsoId(),
                cancellationToken
                ).WhereAsync(predicate.ToOption().UnwrapOr(_ => true)).WhereAsync(OwnedBy(owner));
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

    private async Task<User> GetUserOrThrowAsync(ServerCallContext context) {
        var entry = context.RequestHeaders.Get("Authorization") ?? ThrowUnauthenticated<Entry>();
        if (entry.IsBinary) ThrowUnauthenticated<Unit>("Authorization header can't be binary");
        var maybeUser = await _userService.MaybeGetUser(entry.Value);
        return maybeUser.UnwrapOrElse(() => ThrowUnauthenticated<User>());
    }

    public override async Task<EmptyMessage> DeleteFso(DeleteFsoRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        Fso fso = await GetFsoOrFailAsync(request.FsoId, user, context.CancellationToken);
        if (fso is File file && await _io.PathExistsAsync(file.PhysicalPath)) {
            await _io.RemoveAsync(file.PhysicalPath);
        }
        await _fsosRepo.DeleteAsync(fso);
        return new EmptyMessage { };
    }

    public override async Task<SaveFileResponse> SaveFile(SaveFileRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var parentDir = await GetFsoOrFailAsync(request.ParentId, user, fso => fso is Directory, context.CancellationToken) as Directory;
        var path = await GenerateValidPathAsync();
        var file = new File(default, new FsData(parentDir!, request.Name, 1000, 100, Permissions.FileDefault), path);
        var createResult = await _fsosRepo.CreateAsync(file);
        file = createResult switch {
            Err<Fso, DbError> =>
            throw new RpcException(new(StatusCode.Internal, "failed to create file in db")),
            Ok<Fso, DbError>(var fso) => (File)fso,
            _ => throw new InvalidEnumVariantException(nameof(createResult))
        };
        await _io.WriteAsync(path, new MemoryStream(request.Content.ToByteArray()));
        return new SaveFileResponse() { FileId = file.Id.Id.ToString() };
    }

    public override async Task<GetRootResponse> GetRoot(GetRootRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var (sharedData, directoryData) = user.Root.ToRpcResponse();
        return new GetRootResponse() {
            Data = sharedData,
            DirectoryData = directoryData
        };
    }
}
