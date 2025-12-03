using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Adapters;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.FileService.Extensions;
using ZipZap.FileService.Helpers;
using ZipZap.Grpc;
using ZipZap.Persistance.Models;
using ZipZap.Persistance.Repositories;

using static Grpc.Core.Metadata;

using Directory = ZipZap.Classes.Directory;
using File = ZipZap.Classes.File;
using Guid = System.Guid;
using PathData = ZipZap.Classes.PathData;
using User = ZipZap.Classes.User;

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

    private static void ParseGuidOrThrow(Grpc.Guid grpcGuid, out Guid guid) {
        if (!grpcGuid.TryToGuid(out guid))
            throw new RpcException(new(StatusCode.InvalidArgument, "Invalid guid"));
    }

    private static T ThrowNotFoundIfNull<T>(T? obj, string message = "Resource not found")
        => obj ?? throw new RpcException(new(StatusCode.NotFound, message));

    private Func<Fso, Task<bool>> OwnedBy(User owner) =>
            async fso => (await _fsosRepo.GetRootDirectory(fso.Id))?.Id == owner.Root.Id;


    private Task<Fso> GetFsoOrFailAsync(Grpc.Guid key, User owner, CancellationToken cancellationToken = default)
        => GetFsoOrFailAsync(key, owner, null, cancellationToken);

    private Task<Fso> GetFsoOrFailAsync(PathData pathData, User owner, CancellationToken cancellationToken = default)
        => GetFsoOrFailAsync(pathData, owner, null, cancellationToken);

    private async Task<Fso> GetFsoOrFailAsync(Grpc.Guid key, User owner, Func<Fso, bool>? predicate = null, CancellationToken cancellationToken = default) {
        ParseGuidOrThrow(key, out var guid);
        var file = await _fsosRepo.GetByIdAsync(
                guid.ToFsoId(),
                cancellationToken
                );
        file = file.Where(predicate ?? Predicates.AlwaysTrue);
        file = await file.WhereAsync(OwnedBy(owner));
        // .WhereAsync((predicate ?? (_ => true)).WhereAsync(OwnedBy(owner));
        var fileInner = ThrowNotFoundIfNull(file, "Fso not found for this owner id");
        return fileInner;
    }
    private async Task<Fso> GetFsoOrFailAsync(PathData pathData, User owner, Func<Fso, bool>? predicate = null, CancellationToken cancellationToken = default) =>
        ThrowNotFoundIfNull((pathData switch {
            PathDataWithId { ParentId: var parentId, Name: var name }
                => await _fsosRepo.GetByDirectoryAndName(parentId, name, cancellationToken),
            PathDataWithPath { Path: var path }
                => await _fsosRepo.GetByPath(owner.Root, path, cancellationToken),
            _
                => throw new InvalidEnumArgumentException(nameof(pathData))
        }).Where(predicate ?? (_ => true)));

    private async Task<User> GetUserOrThrowAsync(ServerCallContext context) {
        var entry = context.RequestHeaders.Get(Constants.AUTHORIZATION) ?? ThrowUnauthenticated<Entry>($"No {Constants.AUTHORIZATION} header");
        if (entry.IsBinary) ThrowUnauthenticated<Unit>("Authorization header can't be binary");
        var user = await _userService.GetUser(entry.Value);
        return user ?? ThrowUnauthenticated<User>();
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
        var parentDir = (await GetFsoOrFailAsync(request.Path.ToPathData(user.Root.Id), user, fso => fso is Directory, context.CancellationToken) as Directory)!;
        var file = new File(default, new FsData(parentDir.AsMaybe(), Permissions.FileDefault, request.Path.Name, 1000, 100));
        var createResult = await _fsosRepo.CreateAsync(file);
        file = createResult switch {
            Err<Fso, DbError> =>
                throw new RpcException(new(StatusCode.Internal, "failed to create file in db")),
            Ok<Fso, DbError>(var fso) => (File)fso,
            _ => throw new InvalidEnumArgumentException(nameof(createResult))
        };
        await _io.WriteAsync(file.PhysicalPath, new MemoryStream(request.Content.ToByteArray()));
        return new SaveFileResponse() { FileId = file.Id.Value.ToGrpcGuid() };
    }

    public override async Task<GetRootResponse> GetRoot(EmptyMessage request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var root = user.Root switch {
            OnlyId<Directory, FsoId> => throw new RpcException(new(StatusCode.Internal, "failed to get root")),
            ExistsEntity<Directory, FsoId>(var dir) => dir,
            _ => throw new InvalidEnumArgumentException(nameof(user.Root))
        };
        root = root with { MaybeChildren = await _fsosRepo.GetAllByDirectory(root) };
        var (sharedData, directoryData) = root.ToRpcResponse();
        return new GetRootResponse() {
            FsoId = root.Id.Value.ToGrpcGuid(),
            Data = sharedData,
            DirectoryData = directoryData
        };
    }
    public override async Task<GetFsoResponse> GetFso(GetFsoRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        Fso fso = request.IdentifierCase switch {
            GetFsoRequest.IdentifierOneofCase.FsoId
                => await GetFsoOrFailAsync(request.FsoId, user, context.CancellationToken),
            GetFsoRequest.IdentifierOneofCase.Path
                => await GetFsoOrFailAsync(request.Path.ToPathData(user.Root.Id), user, context.CancellationToken),
            _ or GetFsoRequest.IdentifierOneofCase.None
                => throw new RpcException(new Status(StatusCode.InvalidArgument, nameof(request.IdentifierCase)))
        };
        return fso switch {
            File file => await GetFsoResponse.FromFileAsync(file, await _io.ReadAsync(file.PhysicalPath)),
            Directory dir => GetFsoResponse.FromDirectory(dir with {
                MaybeChildren = await _fsosRepo.GetAllByDirectory(dir)
            }),
            Symlink link => GetFsoResponse.FromSymlink(link),
            _ => throw new InvalidEnumArgumentException(nameof(fso))
        };
    }

    public override async Task<SaveFileResponse> MakeDirectory(MakeDirectoryRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var parentDir = (await GetFsoOrFailAsync(request.Path.ToPathData(user.Root.Id), user, fso => fso is Directory, context.CancellationToken) as Directory)!;
        var dir = new Directory(default, new(parentDir.AsMaybe(), Permissions.DirectoryDefault, request.Path.Name, 1000, 100));
        dir = await _fsosRepo.CreateAsync(dir, context.CancellationToken) switch {
            Ok<Fso, DbError>(Directory d) => d,
            Err<Fso, DbError> => throw new RpcException(new(StatusCode.Internal, "unable to create directory")),
            Ok<Fso, DbError> => throw new RpcException(new(StatusCode.Internal, "CREATED FSO WAS A NOT A DIRECTORY")),
            _ => throw new InvalidEnumArgumentException()
        };

        return new SaveFileResponse { FileId = dir.Id.Value.ToGrpcGuid() };
    }
    public override async Task<SaveFileResponse> MakeLink(MakeLinkRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var parentDir = (await GetFsoOrFailAsync(request.Path.ToPathData(user.Root.Id), user, fso => fso is Directory, context.CancellationToken) as Directory)!;
        var link = new Symlink(default, new(parentDir.AsMaybe(), Permissions.SymlinkDefault, request.Path.Name, 1000, 100), request.Target);
        link = await _fsosRepo.CreateAsync(link, context.CancellationToken) switch {
            Ok<Fso, DbError>(Symlink l) => l,
            Err<Fso, DbError> => throw new RpcException(new(StatusCode.Internal, "unable to create directory")),
            Ok<Fso, DbError> => throw new RpcException(new(StatusCode.Internal, "CREATED FSO WAS A NOT A SYMLINK")),
            _ => throw new InvalidEnumArgumentException()
        };

        return new SaveFileResponse { FileId = link.Id.Value.ToGrpcGuid() };
    }
    public override async Task<EmptyMessage> RemoveFrenchLanguagePack(EmptyMessage message, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var entries = await _fsosRepo.GetAllByDirectory(user.Root, context.CancellationToken);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("deleting {user}'s [{id}] root :)", user.Username, user.Id);
        await _fsosRepo.DeleteRangeAsync(entries, context.CancellationToken);
        return new EmptyMessage { };
    }

    public override async Task<EmptyMessage> ReplaceFile(ReplaceFileRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var file = (File)await GetFsoOrFailAsync(request.Path.ToPathData(user.Root.Id), user, fso => fso is File, context.CancellationToken);
        using var content = new MemoryStream();
        request.Content.WriteTo(content);
        content.Position = 0;
        await _io.WriteAsync(file.PhysicalPath, content);
        return new EmptyMessage();
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context) {
        var token = await _userService.Login(request.Username, request.Password);
        return new LoginResponse { Token = token ?? ThrowUnauthenticated<string>("Wrong credentials") };
    }
    public override async Task<Grpc.User> GetSelf(EmptyMessage message, ServerCallContext context) {
        return (await GetUserOrThrowAsync(context)).ToGrpcUser();
    }
}

internal class Predicates {
    public static Func<object, bool> AlwaysTrue => _ => true;
}
