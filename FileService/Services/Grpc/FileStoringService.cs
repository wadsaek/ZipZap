using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.FileService.Helpers;
using ZipZap.Grpc;
using ZipZap.Persistance.Models;
using ZipZap.Persistance.Repositories;

using static Grpc.Core.Metadata;
using static ZipZap.Classes.Helpers.Constructors;

using Directory = ZipZap.Classes.Directory;
using File = ZipZap.Classes.File;

namespace ZipZap.FileService.Services;

public class FilesStoringServiceImpl : FilesStoringService.FilesStoringServiceBase {
    private readonly ILogger<FilesStoringServiceImpl> _logger;
    private readonly IIO _io;
    private readonly IFsosRepository _fsosRepo;
    private readonly IUserService _userService;

    public FilesStoringServiceImpl(
            ILogger<FilesStoringServiceImpl> logger,
            IIO io,
            IFsosRepository fsosRepo,
            IUserService userService
) {
        _logger = logger;
        _io = io;
        _fsosRepo = fsosRepo;
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
        var parentDir = (await GetFsoOrFailAsync(request.ParentId, user, fso => fso is Directory, context.CancellationToken) as Directory)!;
        var file = new File(default, new FsData(parentDir.AsMaybe(), Permissions.FileDefault, request.Name, 1000, 100));
        var createResult = await _fsosRepo.CreateAsync(file);
        file = createResult switch {
            Err<Fso, DbError> =>
                throw new RpcException(new(StatusCode.Internal, "failed to create file in db")),
            Ok<Fso, DbError>(var fso) => (File)fso,
            _ => throw new InvalidEnumArgumentException(nameof(createResult))
        };
        return new SaveFileResponse() { FileId = file.Id.Value.ToString() };
        await _io.WriteAsync(file.PhysicalPath, new MemoryStream(request.Content.ToByteArray()));
    }

    public override async Task<GetRootResponse> GetRoot(GetRootRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var root = user.Root switch {
            OnlyId<Directory, FsoId> => throw new RpcException(new(StatusCode.Internal, "failed to get root")),
            ExistsEntity<Directory, FsoId>(var dir) => dir,
            _ => throw new InvalidEnumArgumentException(nameof(user.Root))
        };
        root = root with { MaybeChildren = Some(await _fsosRepo.GetAllByDirectory(root)) };
        var (sharedData, directoryData) = root.ToRpcResponse();
        return new GetRootResponse() {
            FsoId = root.Id.Value.ToString(),
            Data = sharedData,
            DirectoryData = directoryData
        };
    }
}
