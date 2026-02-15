// FileStoringService.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

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
using ZipZap.LangExt.Extensions;
using ZipZap.LangExt.Helpers;
using ZipZap.Persistence.Models;
using ZipZap.Persistence.Repositories;

using static Grpc.Core.Metadata;

using Directory = ZipZap.Classes.Directory;
using File = ZipZap.Classes.File;
using Guid = System.Guid;
using PathData = ZipZap.Classes.PathData;
using User = ZipZap.Classes.User;
using UserRole = ZipZap.Classes.UserRole;

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
    /// Does not change the behavior in any way
    [DoesNotReturn]
    private static T ThrowUnauthenticated<T>(string detail = "Unauthenticated")
        => throw new RpcException(new(StatusCode.Unauthenticated, detail));

    private static Guid ParseGuidOrThrow(Grpc.Guid grpcGuid) {
        return grpcGuid.TryToGuid(out var guid)
            ? guid
            : throw new RpcException(new(StatusCode.InvalidArgument, "Invalid guid"));
    }

    private static T ThrowNotFoundIfNull<T>(T? obj, string message = "Resource not found")
        => obj ?? throw new RpcException(new(StatusCode.NotFound, message));

    private Func<Fso, Task<bool>> OwnedBy(User owner) =>
            async fso => (owner.Role == UserRole.Admin) || (await _fsosRepo.GetRootDirectory(fso.Id))?.Id == owner.Root.Id;


    private Task<Fso> GetFsoOrFailAsync(Grpc.Guid key, User owner, CancellationToken cancellationToken = default)
        => GetFsoOrFailAsync(key, owner, null, cancellationToken);

    private Task<Fso> GetFsoOrFailAsync(PathData pathData, User owner, CancellationToken cancellationToken = default)
        => GetFsoOrFailAsync(pathData, owner, null, cancellationToken);

    private async Task<Fso> GetFsoOrFailAsync(Grpc.Guid key, User owner, Func<Fso, bool>? predicate = null, CancellationToken cancellationToken = default) {
        var guid = ParseGuidOrThrow(key);
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
        var fso = await GetFsoOrFailAsync(request.FsoId, user, context.CancellationToken);
        if (fso is File file && await _io.PathExistsAsync(file.PhysicalPath)) {
            await _io.RemoveAsync(file.PhysicalPath);
        }
        await _fsosRepo.DeleteAsync(fso);
        return new();
    }

    public async Task<Directory> GetParentFromRequest(ServerCallContext context, SaveFsoRequest request, User user) => request.HasFilePath
                ? (await GetFsoOrFailAsync(new PathDataWithPath(request.FilePath), user, fso => fso is Directory, context.CancellationToken) as Directory)!
                : (await GetFsoOrFailAsync(request.Data.RootId, user, fso => fso is Directory, context.CancellationToken) as Directory)!;


    public override async Task<SaveFsoResponse> SaveFso(SaveFsoRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var parentDir = await GetParentFromRequest(context, request, user);
        var ownership = request.Data.Ownership.ToOwnership();
        var data = new FsData(parentDir.AsMaybe(), Permissions.FileDefault, request.Data.Name, ownership);
        Fso fso = request.SpecificDataCase switch {
            SaveFsoRequest.SpecificDataOneofCase.FileData => new File(default, data),
            SaveFsoRequest.SpecificDataOneofCase.DirectoryData => new Directory(default, data),
            SaveFsoRequest.SpecificDataOneofCase.SymlinkData => new Symlink(default, data, request.SymlinkData.Target),
            _ => throw new InvalidEnumArgumentException()
        };
        var createResult = await _fsosRepo.CreateAsync(fso);
        fso = createResult switch {
            Err<Fso, DbError>(DbError.UniqueViolation)
                => throw new RpcException(new(StatusCode.AlreadyExists, "This fso already exists")),
            Err<Fso, DbError>
                => throw new RpcException(new(StatusCode.Internal, "failed to create file in db")),
            Ok<Fso, DbError>(var fsoInner) => fsoInner,
            _ => throw new InvalidEnumArgumentException(nameof(createResult))
        };
        if (fso is File file)
            await _io.WriteAsync(file.PhysicalPath, new MemoryStream(request.FileData.Content.ToByteArray()));
        return new() { FileId = fso.Id.Value.ToGrpcGuid() };
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
        return new() {
            FsoId = root.Id.Value.ToGrpcGuid(),
            Data = sharedData,
            DirectoryData = directoryData
        };
    }

    private async Task<GetFsoResponse> ToGetFsoResponse(Fso fso)
    => fso switch {
        File file => await GetFsoResponse.FromFileAsync(file, await _io.ReadAsync(file.PhysicalPath)),
        Directory dir => GetFsoResponse.FromDirectory(dir with {
            MaybeChildren = await _fsosRepo.GetAllByDirectory(dir)
        }),
        Symlink link => GetFsoResponse.FromSymlink(link),
        _ => throw new InvalidEnumArgumentException(nameof(fso))
    };


    public override async Task<GetFsoResponse> GetFso(GetFsoRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var fso = request.IdentifierCase switch {
            GetFsoRequest.IdentifierOneofCase.FsoId
                => await GetFsoOrFailAsync(request.FsoId, user, context.CancellationToken),
            GetFsoRequest.IdentifierOneofCase.Path
                => await GetFsoOrFailAsync(request.Path.ToPathData(user.Root.Id), user, context.CancellationToken),
            _ or GetFsoRequest.IdentifierOneofCase.None
                => throw new RpcException(new(StatusCode.InvalidArgument, nameof(request.IdentifierCase)))
        };
        return await ToGetFsoResponse(fso);
    }

    public override async Task<EmptyMessage> RemoveFrenchLanguagePack(EmptyMessage message, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var entries = await _fsosRepo.GetAllByDirectory(user.Root, context.CancellationToken);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("deleting {user}'s [{id}] root :)", user.Username, user.Id);
        await _fsosRepo.DeleteRangeAsync(entries, context.CancellationToken);
        return new();
    }

    public override async Task<EmptyMessage> ReplaceFile(ReplaceFileRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var file = (File)await GetFsoOrFailAsync(request.Path.ToPathData(user.Root.Id), user, fso => fso is File, context.CancellationToken);
        using var content = new MemoryStream();
        request.Content.WriteTo(content);
        content.Position = 0;
        await _io.WriteAsync(file.PhysicalPath, content);
        return new();
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context) {
        var token = await _userService.Login(request.Username, request.Password);
        return new() { Token = token ?? ThrowUnauthenticated<string>("Wrong credentials") };
    }

    public override async Task<Grpc.User> GetSelf(EmptyMessage message, ServerCallContext context) {
        return (await GetUserOrThrowAsync(context)).ToGrpcUser();
    }

    public override async Task<EmptyMessage> UpdateFso(UpdateFsoRequest request, ServerCallContext context) {
        var fsData = request.Data.ToFsData();
        var user = await GetUserOrThrowAsync(context);
        var fso = request.IdentifierCase switch {
            UpdateFsoRequest.IdentifierOneofCase.FsoId
                => await GetFsoOrFailAsync(request.FsoId, user, context.CancellationToken),
            UpdateFsoRequest.IdentifierOneofCase.Path
                => await GetFsoOrFailAsync(request.Path.ToPathData(user.Root.Id), user, context.CancellationToken),
            _ or UpdateFsoRequest.IdentifierOneofCase.None
                => throw new RpcException(new(StatusCode.InvalidArgument, nameof(request.IdentifierCase)))
        };
        if (fso.Data.VirtualLocation is null)
            fsData = fsData with { VirtualLocation = null };
        var updated = fso with { Data = fsData };
        var result = await _fsosRepo.UpdateAsync(updated) switch {
            Ok<Unit, DbError> => new EmptyMessage(),
            Err<Unit, DbError>(var err) => err switch {
                DbError.NothingChanged => throw new RpcException(new(StatusCode.NotFound, $"Was unable to update fso ${fso}")),
                _ => throw new RpcException(new(StatusCode.Internal, $"got a weird issue {err}"))
            },
            _ => throw new InvalidEnumArgumentException()
        };
        return result;
    }

    public override async Task<EmptyMessage> AdminRemoveUser(Grpc.Guid request, ServerCallContext context) {
        await EnsureAdminOrThrow(context);
        var guid = ParseGuidOrThrow(request);
        var id = new UserId(guid);
        return await _userService.RemoveUser(id) switch {
            Ok<Unit, DbError> => new EmptyMessage(),
            Err<Unit, DbError>(var err) => err switch {
                DbError.NothingChanged => throw new RpcException(new(StatusCode.Internal, $"Was unable to delete user with id {id}")),
                _ => throw new RpcException(new(StatusCode.Internal, $"got a weird issue {err}"))
            },
            _ => throw new InvalidEnumArgumentException()
        };
    }

    public override async Task<Grpc.User> RemoveSelf(EmptyMessage request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var result = await _userService.RemoveUser(user.Id);
        return result switch {
            Ok<Unit, DbError> => user.ToGrpcUser(),
            Err<Unit, DbError>(var err) => err switch {
                DbError.NothingChanged => throw new RpcException(new(StatusCode.Internal, $"Was unable to delete user with id {user.Id}")),
                _ => throw new RpcException(new(StatusCode.Internal, $"got a weird issue {err}"))
            },
            _ => throw new InvalidEnumArgumentException()
        };
    }

    public override Task<Grpc.Guid> AddSshKey(Grpc.SshKey request, ServerCallContext context) {
        return base.AddSshKey(request, context);
    }

    public override async Task<UserList> AdminGetUserList(EmptyMessage request, ServerCallContext context) {
        await EnsureAdminOrThrow(context);
        var users = await _userService.GetAllUsers(context.CancellationToken);
        return users.ToUserList();
    }

    private async Task<User> EnsureAdminOrThrow(ServerCallContext context) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("{Peer}", context.Peer);
        var user = await GetUserOrThrowAsync(context);
        if (user.Role != UserRole.Admin) throw new RpcException(new(StatusCode.PermissionDenied, "You are not an admin"));
        return user;
    }

    public override async Task<SignUpResponse> SignUp(SignUpRequest request, ServerCallContext context) {
        var ownership = request.DefaultOwnership?.ToOwnership() ?? new(1000, 100);
        var user = new User(
            default,
            request.Username,
            _userService.HashPassword(request.Password),
            request.Email,
            UserRole.User,
            ownership,
            null!
        );
        var root = new Directory(default, new(null, Permissions.DirectoryDefault, "/", ownership));
        root = await _fsosRepo.CreateAsync(root, context.CancellationToken) switch {
            Ok<Fso, DbError>(var fso) => fso as Directory ?? throw new RpcException(new(StatusCode.Internal, "The created root is not a directory")),
            Err<Fso, DbError>(var err) => throw new RpcException(new(StatusCode.Internal, err.ToString())),
            _ => throw new InvalidEnumArgumentException()
        };
        user = user with { Root = root };
        user = await _userService.CreateAsync(user, context.CancellationToken) switch {
            Ok<User, DbError>(var returned) => returned,
            Err<User, DbError>(var err) => err switch {
                DbError.UniqueViolation
                    => throw new RpcException(new(StatusCode.AlreadyExists, "This user already exists")),
                _ => throw new RpcException(new(StatusCode.Internal, err.ToString())),
            },
            _ => throw new InvalidEnumArgumentException()
        };
        var token = await _userService.Login(user.Username, request.Password);
        return new() { Ok = new() { Token = token ?? ThrowUnauthenticated<string>("Wrong credentials") } };
    }

    public override async Task<FullPathMessage> GetFullPath(Grpc.Guid request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var fso = await GetFsoOrFailAsync(request, user, context.CancellationToken);
        var result = await _fsosRepo.GetFullPathTree(fso.Id);

        var response = new FullPathMessage();
        response.Path.AddRange(result.Select(a => a.Data.Name));
        return response;

    }

    public override async Task<GetFsoResponse> GetFsoWithRoot(GetFsoWithRootRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var id = ParseGuidOrThrow(request.AnchorId).ToFsoId();
        var root = user.Role switch {
            UserRole.Admin => await _fsosRepo.GetRootDirectory(id, context.CancellationToken),
            UserRole.User => await HandleRootAnchorShenanigansRegularUser(id, user, context.CancellationToken),
            _ => throw new InvalidEnumArgumentException()


        };
        root = ThrowNotFoundIfNull(root);
        var pathData = request.Path.ToPathData(root.Id);
        var fso = ThrowNotFoundIfNull(pathData switch {
            PathDataWithId { ParentId: var parentId, Name: var name }
                => await _fsosRepo.GetByDirectoryAndName(parentId, name, context.CancellationToken),
            PathDataWithPath { Path: var path }
                => await _fsosRepo.GetByPath(root, path, context.CancellationToken),
            _
                => throw new InvalidEnumArgumentException(nameof(pathData))
        });
        return await ToGetFsoResponse(fso);
    }

    private async Task<Directory?> HandleRootAnchorShenanigansRegularUser(FsoId id, User user, CancellationToken cancellationToken) {
        if (await _fsosRepo.GetRootDirectory(id, cancellationToken)
            is Directory root && root.Id == user.Root.Id)
            return root;
        if (await _fsosRepo.GetDeepestSharedFso(id, user.Id, cancellationToken) is not Directory dir)
            return null;
        return dir;
    }
}

internal static class Predicates {
    public static Func<object, bool> AlwaysTrue => _ => true;
}
