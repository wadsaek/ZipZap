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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

using Microsoft.Extensions.Logging;

using ZipZap.Classes.Adapters;
using ZipZap.Classes.Helpers;
using ZipZap.FileService.Extensions;
using ZipZap.FileService.Helpers;
using ZipZap.Grpc;
using ZipZap.LangExt.Extensions;
using ZipZap.Persistence.Models;
using ZipZap.Persistence.Repositories;

using static Grpc.Core.Metadata;

using Directory = ZipZap.Classes.Directory;
using File = ZipZap.Classes.File;
using Guid = System.Guid;
using OwnershipStatus = ZipZap.Classes.OwnershipStatus;
using PathData = ZipZap.Classes.PathData;
using SshKey = ZipZap.Grpc.SshKey;
using User = ZipZap.Classes.User;
using UserRole = ZipZap.Classes.UserRole;
using UserSshKey = ZipZap.Classes.UserSshKey;

namespace ZipZap.FileService.Services;

public class FilesStoringServiceImpl : FilesStoringService.FilesStoringServiceBase {
    private readonly ILogger<FilesStoringServiceImpl> _logger;
    private readonly IIO _io;
    private readonly IFsosRepository _fsosRepo;
    private readonly IUserSshKeysRepository _userKeysRepo;
    private readonly IUserService _userService;
    private readonly ISshService _sshService;
    private readonly IUserRepository _usersRepo;
    private readonly ITrustedAuthorityKeysRepository _trustedKeysRepo;
    private readonly IFsoAccessesRepository _fsoAccessesRepo;
    private readonly IFsosService _fsosService;

    public FilesStoringServiceImpl(
        ILogger<FilesStoringServiceImpl> logger,
        IIO io,
        IFsosRepository fsosRepo,
        IUserService userService,
        ISshService sshService,
        IUserSshKeysRepository userKeysRepo,
        ITrustedAuthorityKeysRepository trustedKeysRepo,
        IUserRepository usersRepo,
        IFsoAccessesRepository fsoAccessesRepo,
        IFsosService fsosService
    ) {
        _logger = logger;
        _io = io;
        _fsosRepo = fsosRepo;
        _userService = userService;
        _userKeysRepo = userKeysRepo;
        _sshService = sshService;
        _trustedKeysRepo = trustedKeysRepo;
        _usersRepo = usersRepo;
        _fsoAccessesRepo = fsoAccessesRepo;
        _fsosService = fsosService;
    }

    /// <summary>throws `Unauthenticated` RpcException</summary>
    /// <typeparam name="T"/>
    /// Does not change the behavior in any way
    /// </typeparam>
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

    private Task<FsoWithOwnership> GetFsoOrFailAsync(Grpc.Guid key, User owner, CancellationToken cancellationToken = default)
        => GetFsoOrFailAsync(key, owner, null, cancellationToken);

    private Task<FsoWithOwnership> GetFsoOrFailAsync(PathData pathData, User owner, CancellationToken cancellationToken = default)
        => GetFsoOrFailAsync(pathData, owner, null, cancellationToken);

    private async Task<FsoWithOwnership> GetFsoOrFailAsync(Grpc.Guid key, User owner, Func<Fso, bool>? predicate = null, CancellationToken cancellationToken = default) {
        var guid = ParseGuidOrThrow(key);
        var file = await _fsosRepo.GetByIdAsync(
                guid.ToFsoId(),
                cancellationToken
                );
        file = file.Filter(predicate ?? Predicates.AlwaysTrue);
        var withStatus = (file, owner) switch {
            (null, _) => null,
            (_, { Role: UserRole.Admin }) => file.AdminAccessible(),
            var (f, u)
                when await FindDeepestSharedFso(f.Id, u, cancellationToken) is (not null, var s)
                => new FsoWithOwnership(file, s),
            _ => null
        };
        // .WhereAsync((predicate ?? (_ => true)).WhereAsync(OwnedBy(owner));
        var fileInner = ThrowNotFoundIfNull(withStatus, "Fso not found for this owner id");
        return fileInner;
    }
    private async Task<FsoWithOwnership> GetFsoOrFailAsync(PathData pathData, User owner, Func<Fso, bool>? predicate = null, CancellationToken cancellationToken = default) {
        var fso = ThrowNotFoundIfNull((pathData switch {
            PathDataWithId { ParentId: var parentId, Name: var name }
                => await _fsosRepo.GetByDirectoryAndName(parentId, name, cancellationToken),
            PathDataWithPath { Path: var path }
                => await _fsosRepo.GetByPath(owner.Root, path, cancellationToken),
            _
                => throw new InvalidEnumArgumentException(nameof(pathData))
        }).Filter(predicate ?? (_ => true)));
        return new(fso, OwnershipStatus.Owned);
    }

    private async Task<User> GetUserOrThrowAsync(ServerCallContext context) {
        var entry = context.RequestHeaders.Get(Constants.AUTHORIZATION) ?? ThrowUnauthenticated<Entry>($"No {Constants.AUTHORIZATION} header");
        if (entry.IsBinary) ThrowUnauthenticated<Unit>("Authorization header can't be binary");
        var user = await _userService.GetUser(entry.Value, context.CancellationToken);
        return user ?? ThrowUnauthenticated<User>();
    }
    public static void ThrowIfNotAdmin(User user) {
        if (user.Role != UserRole.Admin)
            throw new RpcException(new(StatusCode.PermissionDenied, "You are not an admin"));
    }

    public override async Task<EmptyMessage> DeleteFso(DeleteFsoRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var (fso, _) = await GetFsoOrFailAsync(request.FsoId, user, context.CancellationToken);
        var options = request.Options.ToOptions();
        return await _fsosService.RemoveFso(fso.Id, options, context.CancellationToken)
        .SelectAsync(_ => new EmptyMessage())
        .UnwrapOrElseAsync(err => err switch {
            DbError.NothingChanged => throw new RpcException(new(StatusCode.NotFound, "Resource not found")),
            _ => throw new RpcException(new(StatusCode.Internal, "Internal error"))
        });
    }

    public async Task<Directory> GetParentFromRequest(ServerCallContext context, SaveFsoRequest request, User user) => request.HasFilePath
                ? ((await GetFsoOrFailAsync(
                    new PathDataWithPath(request.FilePath),
                    user,
                    fso => fso is Directory,
                    context.CancellationToken)).Fso as Directory)!

                : ((await GetFsoOrFailAsync(
                    request.Data.RootId,
                    user,
                    fso => fso is Directory,
                    context.CancellationToken)).Fso as Directory)!;


    public override async Task<SaveFsoResponse> SaveFso(SaveFsoRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var parentDir = await GetParentFromRequest(context, request, user);
        var ownership = request.Data.Ownership.ToOwnership();
        var data = FsData.TryNew(parentDir.AsMaybe(), Permissions.FileDefault, request.Data.Name, ownership)
        .UnwrapOrElse(err => err switch {
            FsDataError.EmptyName => throw new RpcException(new(StatusCode.InvalidArgument, "Name cannot be empty")),
            _ => throw new InvalidEnumArgumentException(),
        });
        Fso fso = request.SpecificDataCase switch {
            SaveFsoRequest.SpecificDataOneofCase.FileData => new File(default, data),
            SaveFsoRequest.SpecificDataOneofCase.DirectoryData => new Directory(default, data),
            SaveFsoRequest.SpecificDataOneofCase.SymlinkData => Symlink.TryCreate(default, data, request.SymlinkData.Target)
                .UnwrapOrElse(err => err switch {
                    SymlinkError.EmptyTarget
                        => throw new RpcException(new(StatusCode.InvalidArgument, "Symlink target cannot be empty")),
                    _ => throw new InvalidEnumArgumentException()
                }),
            _ => throw new InvalidEnumArgumentException()
        };
        var createResult = await _fsosRepo.CreateAsync(fso);
        return await createResult.SelectAsync(async fso => {
            if (fso is File file)
                await _io.WriteAsync(file.PhysicalPath, new MemoryStream(request.FileData.Content.ToByteArray()));
            return new SaveFsoResponse() { FileId = fso.Id.Value.ToGrpcGuid() };

        }).UnwrapOrElseAsync(err =>
            err switch {
                DbError.UniqueViolation
                    => throw new RpcException(new(StatusCode.AlreadyExists, "This fso already exists")),
                _
                    => throw new RpcException(new(StatusCode.Internal, "Failed to create file in db")),
            });
    }

    public override async Task<GetRootResponse> GetRoot(EmptyMessage request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var root = user.Root switch {
            OnlyId<Directory, FsoId> => throw new RpcException(new(StatusCode.Internal, "Failed to get root")),
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

    private async Task<GetFsoResponse> ToGetFsoResponse(Fso fso, OwnershipStatus status)
    => fso switch {
        File file => await GetFsoResponse.FromFileAsync(file, await _io.ReadAsync(file.PhysicalPath), status),
        Directory dir => GetFsoResponse.FromDirectory(dir with {
            MaybeChildren = await _fsosRepo.GetAllByDirectory(dir)
        }, status),
        Symlink link => GetFsoResponse.FromSymlink(link, status),
        _ => throw new InvalidEnumArgumentException(nameof(fso))
    };


    public override async Task<GetFsoResponse> GetFso(GetFsoRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var (fso, status) = request.IdentifierCase switch {
            GetFsoRequest.IdentifierOneofCase.FsoId
                => await GetFsoOrFailAsync(request.FsoId, user, context.CancellationToken),
            GetFsoRequest.IdentifierOneofCase.Path
                => await GetFsoOrFailAsync(request.Path.ToPathData(user.Root.Id), user, context.CancellationToken),
            _ or GetFsoRequest.IdentifierOneofCase.None
                => throw new RpcException(new(StatusCode.InvalidArgument, nameof(request.IdentifierCase)))
        };
        return await ToGetFsoResponse(fso, status);
    }

    public override async Task<EmptyMessage> RemoveFrenchLanguagePack(EmptyMessage message, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var entries = await _fsosRepo.GetAllByDirectory(user.Root, context.CancellationToken);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("deleting {user}'s [{id}] root :)", user.Username, user.Id);
        await _fsosService.RemoveFsoRange(entries.Select(f => f.Id), context.CancellationToken);
        return new();
    }

    public override async Task<EmptyMessage> ReplaceFile(ReplaceFileRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var (fso, _) = request.IdentifierCase switch {
            ReplaceFileRequest.IdentifierOneofCase.FsoId
                => await GetFsoOrFailAsync(request.FsoId, user, fso => fso is File, context.CancellationToken),
            ReplaceFileRequest.IdentifierOneofCase.Path
                => await GetFsoOrFailAsync(request.Path.ToPathData(user.Root.Id), user, fso => fso is File, context.CancellationToken),
            _ or ReplaceFileRequest.IdentifierOneofCase.None
                => throw new RpcException(new(StatusCode.InvalidArgument, nameof(request.IdentifierCase)))
        };
        var file = (File)fso;
        using var content = new MemoryStream();
        request.Content.WriteTo(content);
        content.Position = 0;
        await _io.WriteAsync(file.PhysicalPath, content);
        return new();
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context) {
        var token = await _userService.Login(request.Username, request.Password, context.CancellationToken);
        return new() { Token = token ?? ThrowUnauthenticated<string>("Wrong credentials") };
    }

    public override async Task<Grpc.User> GetSelf(EmptyMessage message, ServerCallContext context) {
        return (await GetUserOrThrowAsync(context)).ToGrpcUser();
    }

    public override async Task<EmptyMessage> UpdateFso(UpdateFsoRequest request, ServerCallContext context) {
        var fsData = request.Data.ToFsData();
        var user = await GetUserOrThrowAsync(context);
        var (fso, _) = request.IdentifierCase switch {
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
        var result = await _fsosRepo.UpdateAsync(updated);
        return result
        .Select(_ => new EmptyMessage())
        .UnwrapOrElse(err => err switch {
            DbError.NothingChanged => throw new RpcException(new(StatusCode.NotFound, $"Was unable to update fso ${fso}")),
            _ => throw new RpcException(new(StatusCode.Internal, $"got a weird issue {err}"))
        });
    }

    public override async Task<EmptyMessage> AdminRemoveUser(Grpc.Guid request, ServerCallContext context) {
        var currentUser = await EnsureAdminOrThrow(context);
        var guid = ParseGuidOrThrow(request);
        var id = new UserId(guid);
        if (currentUser.Id == id) {
            throw new RpcException(new(StatusCode.PermissionDenied, "You can't delete yourself"));
        }
        var result = await _userService.RemoveUser(id, context.CancellationToken);
        return result
        .Select(_ => new EmptyMessage())
        .UnwrapOrElse(err => err switch {
            DbError.NothingChanged => throw new RpcException(new(StatusCode.Internal, $"Was unable to delete user with id {id}")),
            _ => throw new RpcException(new(StatusCode.Internal, $"got a weird issue {err}"))
        });
    }

    public override async Task<Grpc.User> RemoveSelf(EmptyMessage request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var result = await _userService.RemoveUser(user.Id, context.CancellationToken);
        return result
        .Select(_ => user.ToGrpcUser())
        .UnwrapOrElse(err => err switch {
            DbError.NothingChanged => throw new RpcException(new(
                StatusCode.Internal,
                $"Was unable to delete user with id {user.Id}"
            )),
            _ => throw new RpcException(new(
                StatusCode.Internal,
                $"got a weird issue {err}"
            ))
        });
    }

    public override async Task<Grpc.Guid> AddSshKey(SshKey request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var key = new SshPublicKey(request.Key);
        var userKey = new UserSshKey(default, key, user);
        var result = await _userKeysRepo.CreateAsync(userKey, context.CancellationToken);
        return result
        .Select(newKey => newKey.Id.Id.ToGrpcGuid())
        .UnwrapOrElse(err
            => throw new RpcException(new(StatusCode.Internal, err.ToString()))
        );
    }

    public override async Task<UserSshKeyList> GetSshKeys(EmptyMessage request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var keys = await _userKeysRepo.GetForUserId(user.Id);
        return keys.ToSshKeyList();
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
        ThrowIfNotAdmin(user);
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
        return await _fsosRepo.CreateAsync(root, context.CancellationToken)
        .SelectAsync(fso =>
            fso as Directory
                ?? throw new RpcException(new(
                    StatusCode.Internal,
                    "The created root is not a directory"
                ))
        )
        .SelectManyAsync(async root => {
            user = user with { Root = root };
            return await _userService.CreateAsync(user, context.CancellationToken);
        })
        .SelectAsync(async user => {
            var token = await _userService.Login(user.Username, request.Password, context.CancellationToken);
            return new SignUpResponse { Ok = new() { Token = token ?? ThrowUnauthenticated<string>("Wrong credentials") } };
        })
        .UnwrapOrElseAsync(err => err switch {
            DbError.UniqueViolation
                => throw new RpcException(new(StatusCode.AlreadyExists, "This user already exists")),
            _ => throw new RpcException(new(StatusCode.Internal, err.ToString())),
        });
    }

    public override async Task<FullPathMessage> GetFullPath(Grpc.Guid request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var (fso, status) = await GetFsoOrFailAsync(request, user, context.CancellationToken);
        var result = await _fsosRepo.GetFullPathTree(fso.Id);

        var response = new FullPathMessage();
        response.Path.AddRange(result.Select(a => a.Data.Name));
        return response;

    }

    public override async Task<GetFsoResponse> GetFsoWithRoot(GetFsoWithRootRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var id = ParseGuidOrThrow(request.AnchorId).ToFsoId();
        var root = user.Role switch {
            UserRole.Admin => (await _fsosRepo.GetRootDirectory(id, context.CancellationToken))?.AdminAccessible(),
            UserRole.User => await FindDeepestSharedFso(id, user, context.CancellationToken),
            _ => throw new InvalidEnumArgumentException()
        };
        var dir = ThrowNotFoundIfNull(root?.Fso as Directory);
        var pathData = request.Path.ToPathData(dir.Id);
        var fso = ThrowNotFoundIfNull(pathData switch {
            PathDataWithId { ParentId: var parentId, Name: var name }
                => await _fsosRepo.GetByDirectoryAndName(parentId, name, context.CancellationToken),
            PathDataWithPath { Path: var path }
                => await _fsosRepo.GetByPath(dir, path, context.CancellationToken),
            _
                => throw new InvalidEnumArgumentException(nameof(pathData))
        });
        // this is in fact not null because `root.Fso` as `Directory` is not null
        return await ToGetFsoResponse(fso, root!.OwnershipStatus);
    }

    /// <summary>
    /// finds the deepest `fso` that is a parent of <paramref name="id"/>,
    /// such that the <paramref name="user"/> has access to it through `fsoAccess`
    /// </summary>
    private async Task<FsoWithOwnership?> FindDeepestSharedFso(FsoId id, User user, CancellationToken cancellationToken) {
        if (await _fsosRepo.GetRootDirectory(id, cancellationToken)
            is Directory root && root.Id == user.Root.Id)
            return root.Owned();
        var fso = await _fsosRepo.GetDeepestSharedFso(id, user.Id, cancellationToken);
        return fso?.Shared();
    }

    public override async Task<EmptyMessage> AdminAddSshHostKey(AdminAddSshHostKeyRequest request, ServerCallContext context) {
        var user = await EnsureAdminOrThrow(context);
        var sshKey = request.Key.ToPublicKey();
        var hostKey = new TrustedAuthorityKey(default, request.ServerName, sshKey, DateTimeOffset.UtcNow, user);
        var result = await _trustedKeysRepo.CreateAsync(hostKey, context.CancellationToken);
        return result
        .Select(_ => new EmptyMessage())
        .UnwrapOrElse(err => throw new RpcException(new(
            StatusCode.Internal,
            err.ToString()
        )));
    }

    public override async Task<LoginSshResponse> LoginSsh(LoginSshRequest request, ServerCallContext context) {
        var timestamp = request.Timestamp.ToDateTimeOffset();
        if (DateTimeOffset.UtcNow - timestamp > TimeSpan.FromSeconds(5)) return new() { Error = LoginSshError.TimestampTooEarly };
        var result = await _sshService.LoginSsh(
            request.Username,
            request.UserPublicKey.ToPublicKey(),
            request.HostPublicKey.ToPublicKey(),
            request.Timestamp,
            request.Signature.ToArray(),
            context.CancellationToken
        );
        return result
        .Select(token =>
            new LoginSshResponse { Token = token }
        )
        .UnwrapOrElse(err =>
            new LoginSshResponse { Error = err.ToGrpcError() }
        );
    }
    public override async Task<Grpc.User> GetUser(UserSpecification request, ServerCallContext context) {
        var requestedUser = await TryGetUserFromSpecification(request, context.CancellationToken);
        return ThrowNotFoundIfNull(requestedUser).ToGrpcUser();
    }

    private async Task<User?> TryGetUserFromSpecification(UserSpecification request, CancellationToken cancellationToken) {
        return request.IdentifierCase switch {
            UserSpecification.IdentifierOneofCase.Id => await _usersRepo.GetByIdAsync(ParseGuidOrThrow(request.Id).ToUserId(), cancellationToken),
            UserSpecification.IdentifierOneofCase.Username => await _usersRepo.GetUserByUsername(request.Username, cancellationToken),
            UserSpecification.IdentifierOneofCase.None or _ => throw new InvalidEnumArgumentException()
        };
    }

    public override async Task<UserSshKeyList> AdminGetSshKeysForUser(Grpc.Guid request, ServerCallContext context) {
        var id = ParseGuidOrThrow(request).ToUserId();
        await EnsureAdminOrThrow(context);
        var keys = await _userKeysRepo.GetForUserId(id);
        return keys.ToSshKeyList();
    }

    public override async Task<HostKeys> GetSshHostKeys(EmptyMessage request, ServerCallContext context) {
        var keys = await _trustedKeysRepo.GetAllWithUser();
        return keys.ToGrpcHostKeys();
    }
    public override async Task<EmptyMessage> AdminRemoveSshHostKey(Grpc.Guid request, ServerCallContext context) {
        await EnsureAdminOrThrow(context);
        var id = new TrustedAuthorityKeyId(ParseGuidOrThrow(request));
        return await _trustedKeysRepo.DeleteAsync(id)
        .SelectAsync(_ => new EmptyMessage())
        .UnwrapOrElseAsync(err => err switch {
            DbError.NothingChanged => throw new RpcException(new(StatusCode.NotFound, "This key was not found")),
            DbError.Unknown or _ => throw new RpcException(new(StatusCode.Internal, "failed to delete key"))
        });
    }
    public override async Task<Accesses> GetAccessesForFso(Grpc.Guid request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var fso = await GetFsoOrFailAsync(request, user, context.CancellationToken);
        var accesses = await _fsoAccessesRepo.GetForFsoId(fso.Fso.Id);

        return accesses.ToGrpc();
    }
    public override async Task<Accesses> GetAccessible(EmptyMessage request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var accesses = await _fsoAccessesRepo.GetForUserId(user.Id);

        return accesses.ToGrpc();
    }
    public override async Task<Accesses> GetAccessesForUser(UserSpecification request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var targetUser = await TryGetUserFromSpecification(request, context.CancellationToken);
        targetUser = ThrowNotFoundIfNull(targetUser);
        var accesses = await _fsoAccessesRepo.GetForUserId(targetUser.Id);
        List<FsoAccess> belongingToCurrentUser = [];
        foreach (var acc in accesses) {
            var root = await _fsosRepo.GetRootDirectory(acc.Fso.Id);
            if (root?.Id == user.Root.Id)
                belongingToCurrentUser.Add(acc);
        }

        return belongingToCurrentUser.ToGrpc();

    }
    public override async Task<Accesses> GetSharedBySelf(EmptyMessage request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var accesses = await _fsoAccessesRepo.GetAllFull(context.CancellationToken);
        List<FsoAccess> belongingToCurrentUser = [];
        foreach (var acc in accesses) {
            var root = await _fsosRepo.GetRootDirectory(acc.Fso.Id);
            if (root?.Id == user.Root.Id)
                belongingToCurrentUser.Add(acc);
        }

        return belongingToCurrentUser.ToGrpc();
    }

    public override async Task<EmptyMessage> ShareFso(ShareFsoRequest request, ServerCallContext context) {
        var user = await GetUserOrThrowAsync(context);
        var targetUser = await TryGetUserFromSpecification(request.User, context.CancellationToken);
        if (user == targetUser) throw new RpcException(new(StatusCode.InvalidArgument, "Can't share fsos with yourself"));
        targetUser = ThrowNotFoundIfNull(targetUser);
        var fso = await GetFsoOrFailAsync(request.FsoId, user, context.CancellationToken);
        fso = ThrowNotFoundIfNull(fso);
        var access = new FsoAccess(default, fso, targetUser);
        _ = await _fsoAccessesRepo.CreateAsync(new(access), context.CancellationToken)
        .UnwrapOrElseAsync(err => throw new RpcException(err switch {
            DbError.UniqueViolation => new(StatusCode.AlreadyExists, "You have already shared this fso with this user"),
            _ => new(StatusCode.Internal, "Something went wrong when creating an FsoAcess")
        }));
        return new();
    }

    public override async Task<Grpc.User> GetFsoOwner(Grpc.Guid request, ServerCallContext context) {
        var fsoId = ParseGuidOrThrow(request).ToFsoId();
        var user = await GetUserOrThrowAsync(context);
        var fso = await FindDeepestSharedFso(fsoId, user, context.CancellationToken);
        fso = ThrowNotFoundIfNull(fso);
        if (fso.OwnershipStatus is OwnershipStatus.Owned) return user.ToGrpcUser();
        var root = await _fsosRepo.GetRootDirectory(fso.Fso.Id, context.CancellationToken);
        root = ThrowNotFoundIfNull(root);
        var owner = await _usersRepo.GetUserByRootId(root.Id, context.CancellationToken);
        owner = ThrowNotFoundIfNull(owner);
        return owner.ToGrpcUser();
    }
    public override async Task<EmptyMessage> Unshare(Grpc.Guid request, ServerCallContext context) {
        var accessId = ParseGuidOrThrow(request).ToFsoAccessId();
        var user = await GetUserOrThrowAsync(context);
        var access = await _fsoAccessesRepo.GetByIdAsyncFull(accessId, context.CancellationToken);
        access = ThrowNotFoundIfNull(access);
        async Task<bool> IsAuthorOfSharedFso() {
            var maybeFso = await FindDeepestSharedFso(access.Fso.Id, user, context.CancellationToken);
            return maybeFso is not null;
        }
        var canDelete = access.User.Id == user.Id
                          || Predicates.IsAdmin(user)
                          || await IsAuthorOfSharedFso();
        if (!canDelete)
            ThrowNotFoundIfNull(null as object);

        await _fsoAccessesRepo.DeleteAsync(access.Id);
        return new();
    }
}

internal static class Predicates {
    public static Func<object, bool> AlwaysTrue => _ => true;
    public static Func<User, bool> IsAdmin => u => u.Role == UserRole.Admin;
}
