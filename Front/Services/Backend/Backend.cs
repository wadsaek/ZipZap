// Backend.cs - Part of the ZipZap project for storing files online
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Google.Protobuf;

using Grpc.Core;

using ZipZap.Classes;
using ZipZap.Classes.Adapters;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.Grpc;
using ZipZap.LangExt.Helpers;

using static ZipZap.LangExt.Helpers.ResultConstructor;

using PathData = ZipZap.Classes.PathData;
using User = ZipZap.Classes.User;

namespace ZipZap.Front.Services;

public class Backend : IBackend {
    private readonly FilesStoringService.FilesStoringServiceClient _filesStoringService;
    private readonly BackendConfiguration _configuration;
    private readonly ExceptionConverter<ServiceError> _exceptionConverter;

    public Backend(FilesStoringService.FilesStoringServiceClient filesStoringService, BackendConfiguration configuration, ExceptionConverter<ServiceError> exceptionConverter) {
        _filesStoringService = filesStoringService;
        _configuration = configuration;
        _exceptionConverter = exceptionConverter;
    }

    public async Task<Result<Unit, ServiceError>> DeleteFrenchLanguagePack() {
        try {
            await _filesStoringService.RemoveFrenchLanguagePackAsync(new(), _configuration.ToMetadata());
            return Ok<Unit, ServiceError>(new());
        } catch (RpcException exception) {
            return Err<Unit, ServiceError>(_exceptionConverter.Convert(exception));
        }
    }

    public async Task<Result<Unit, ServiceError>> DeleteFso(FsoId fsoId, DeleteFlags flags, CancellationToken token = default) {
        try {
            await _filesStoringService.DeleteFsoAsync(new() { FsoId = fsoId.Value.ToGrpcGuid() }, _configuration.ToMetadata(), cancellationToken: token);
            return Ok<Unit, ServiceError>(new());
        } catch (RpcException exception) {
            return Err<Unit, ServiceError>(_exceptionConverter.Convert(exception));
        }
    }

    public Task<Result<Fso, ServiceError>> GetFsoByIdAsync(FsoId fsoId, CancellationToken cancellationToken = default)
        => Wrap(async () => (await _filesStoringService.GetFsoAsync(
                        new() { FsoId = fsoId.Value.ToGrpcGuid() },
                        _configuration.ToMetadata(),
                        cancellationToken: cancellationToken)
                    ).ToFso());

    public Task<Result<Fso, ServiceError>> GetFsoByPathAsync(PathData pathData, CancellationToken cancellationToken = default)
        => Wrap(async () => (await _filesStoringService.GetFsoAsync(
                        new() { Path = pathData.ToRpcPathData() },
                        _configuration.ToMetadata(),
                        cancellationToken: cancellationToken)
                    ).ToFso()
                );

    private async Task<Result<T, ServiceError>> Wrap<T>(Func<Task<T>> func) {
        try {
            var result = await func();
            return Ok<T, ServiceError>(result);
        } catch (RpcException exception) {
            return Err<T, ServiceError>(_exceptionConverter.Convert(exception));
        }
    }


    public async Task<Result<Directory, ServiceError>> GetRoot(CancellationToken cancellationToken = default) {
        return await Wrap<Directory>(async () => {
            var response = await _filesStoringService.GetRootAsync(
                new(),
                _configuration.ToMetadata(),
                cancellationToken: cancellationToken);
            return new(response.FsoId.ToGuid().ToFsoId(), response.Data.ToFsData()) { MaybeChildren = response.DirectoryData.ToFsos() };
        });
    }


    public Task<Result<Unit, ServiceError>> UpdateFso(Fso fso, CancellationToken cancellationToken = default) {
        return Wrap(async () => {
            await _filesStoringService.UpdateFsoAsync(new() {
                FsoId = fso.Id.Value.ToGrpcGuid(),
                Data = fso.ToRpcSharedData()
            }, _configuration.ToMetadata());
            return new Unit();
        });
    }

    public async Task<Result<User, ServiceError>> GetSelf(CancellationToken cancellationToken = default) {
        try {
            var user = await _filesStoringService.GetSelfAsync(new(), _configuration.ToMetadata(), cancellationToken: cancellationToken);
            return Ok<User, ServiceError>(user.ToUser());
        } catch (RpcException exception) {
            return Err<User, ServiceError>(_exceptionConverter.Convert(exception));
        }
    }

    public Task<Result<Unit, ServiceError>> ReplaceFileById(FsoId id, ByteString bytes, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }

    public Task<Result<Unit, ServiceError>> ReplaceFileByPath(PathData pathData, ByteString bytes, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }

    private async Task<Grpc.Guid> SaveFileRaw(ByteString bytes, File file, string? path, CancellationToken cancellationToken) {
        path = path?.NormalizePath();
        var request = new SaveFsoRequest() {
            Data = file.ToRpcSharedData(),
            FileData = new() { Content = bytes }
        };
        if (!string.IsNullOrWhiteSpace(path))
            request.FilePath = path;
        var result = await _filesStoringService.SaveFsoAsync(
            request, _configuration.ToMetadata(), cancellationToken: cancellationToken
        );
        return result.FileId;
    }
    public async Task<Result<File, ServiceError>> SaveFile(ByteString bytes, File file, CancellationToken cancellationToken = default)
    => await SaveFile(bytes, file, "", cancellationToken);

    public async Task<Result<File, ServiceError>> SaveFile(ByteString bytes, File file, string path, CancellationToken cancellationToken = default) {
        var fso = await Wrap(() => SaveFileRaw(bytes, file, path, cancellationToken));
        return fso.SelectMany(grpcGuid =>
            grpcGuid.TryToGuid(out var guid)
            ? Ok<File, ServiceError>(file with { Id = guid.ToFsoId() })
            : Err<File, ServiceError>(new ServiceError.BadResult())
        );
    }

    private async Task<Grpc.Guid> MkDirRaw(Directory dir, string? path, CancellationToken cancellationToken) {
        path = path?.NormalizePath();
        var request = new SaveFsoRequest() {
            Data = dir.ToRpcSharedData(),
            DirectoryData = dir.ToRpcDirectoryData()
        };
        if (!string.IsNullOrWhiteSpace(path))
            request.FilePath = path;
        var result = await _filesStoringService.SaveFsoAsync(
            request, _configuration.ToMetadata(),
            cancellationToken: cancellationToken
        );
        return result.FileId;
    }
    public async Task<Result<Directory, ServiceError>> MakeDirectory(Directory dir, CancellationToken cancellationToken = default)
    => await MakeDirectory(dir, "", cancellationToken);


    public async Task<Result<Directory, ServiceError>> MakeDirectory(Directory dir, string path, CancellationToken cancellationToken = default) {
        var fso = await Wrap(() => MkDirRaw(dir, path, cancellationToken));
        return fso.SelectMany(grpcGuid =>
            grpcGuid.TryToGuid(out var guid)
            ? Ok<Directory, ServiceError>(dir with { Id = guid.ToFsoId() })
            : Err<Directory, ServiceError>(new ServiceError.BadResult())
        );
    }

    private async Task<Grpc.Guid> MkLinkRaw(Symlink link, string? path, CancellationToken cancellationToken) {
        path = path?.NormalizePath();
        var request = new SaveFsoRequest() {
            Data = link.ToRpcSharedData(),
            SymlinkData = link.ToRpcLinkData()
        };
        if (!string.IsNullOrWhiteSpace(path))
            request.FilePath = path;
        var result = await _filesStoringService.SaveFsoAsync(
            request, _configuration.ToMetadata(),
            cancellationToken: cancellationToken
        );
        return result.FileId;
    }
    public async Task<Result<Symlink, ServiceError>> MakeLink(Symlink link, CancellationToken cancellationToken = default)
    => await MakeLink(link, "", cancellationToken);


    public async Task<Result<Symlink, ServiceError>> MakeLink(Symlink link, string path, CancellationToken cancellationToken = default) {
        var fso = await Wrap(() => MkLinkRaw(link, path, cancellationToken));
        return fso.SelectMany(grpcGuid =>
            grpcGuid.TryToGuid(out var guid)
            ? Ok<Symlink, ServiceError>(link with { Id = guid.ToFsoId() })
            : Err<Symlink, ServiceError>(new ServiceError.BadResult())
        );
    }

    public async Task<Result<User, ServiceError>> RemoveSelf(CancellationToken cancellationToken = default)
    => await Wrap(async () =>
        (await _filesStoringService.RemoveSelfAsync(
            new EmptyMessage(),
            _configuration.ToMetadata(),
            cancellationToken: cancellationToken)
        ).ToUser());

    public async Task<Result<IEnumerable<User>, ServiceError>> AdminGetUsers(CancellationToken cancellationToken = default) {
        var userList = await Wrap(async () => await _filesStoringService.AdminGetUserListAsync(new EmptyMessage(), _configuration.ToMetadata(), cancellationToken: cancellationToken));
        return userList.Select(list =>
            list.User
                .AsEnumerable()
                .Select(u => u.ToUser())
        );
    }

    public async Task<Result<Unit, ServiceError>> AdminRemoveUser(UserId id, CancellationToken cancellationToken = default) {
        var result = await Wrap(async () => await _filesStoringService.AdminRemoveUserAsync(id.Value.ToGrpcGuid(), _configuration.ToMetadata(), cancellationToken: cancellationToken));
        return result.Select(_ => new Unit());
    }

    public async Task<Result<IEnumerable<string>, ServiceError>> GetFullPath(FsoId id, CancellationToken cancellationToken = default) {
        var result = await Wrap(async () =>
            await _filesStoringService.GetFullPathAsync(
                id.Value.ToGrpcGuid(),
                _configuration.ToMetadata(),
                cancellationToken: cancellationToken));
        return result.Select(res => res.Path.AsEnumerable());
    }

    public async Task<Result<Fso, ServiceError>> GetFsoWithRootAsync(PathData pathData, FsoId anchor, CancellationToken cancellationToken = default) {
        var result = await Wrap(async () =>
            await _filesStoringService.GetFsoWithRootAsync(new() {
                Path = pathData.ToRpcPathData(),
                AnchorId = anchor.Value.ToGrpcGuid()
            },
            _configuration.ToMetadata(),
            cancellationToken: cancellationToken)
        );
        return result.Select(f => f.ToFso());
    }
}
public record BackendConfiguration(string AuthToken);

public static class BackendConfigurationExt {
    extension(BackendConfiguration configuration) {
        public Metadata ToMetadata() {
            return new() {
                { Constants.AUTHORIZATION, configuration.AuthToken }
            };
        }
    }
}
