using System.Threading;
using System.Threading.Tasks;

using Google.Protobuf;

using Grpc.Core;

using ZipZap.Classes;
using static ZipZap.Classes.Helpers.Constructors;
using ZipZap.Classes.Helpers;
using ZipZap.Grpc;

using PathData = ZipZap.Classes.PathData;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Adapters;
using User = ZipZap.Classes.User;
using System;

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
            await _filesStoringService.RemoveFrenchLanguagePackAsync(new EmptyMessage(), _configuration.ToMetadata());
            return Ok<Unit, ServiceError>(new Unit());
        } catch (RpcException exception) {
            return Err<Unit, ServiceError>(_exceptionConverter.Convert(exception));
        }
    }

    public async Task<Result<Unit, ServiceError>> DeleteFso(FsoId fsoId, DeleteFlags flags, CancellationToken token = default) {
        try {
            await _filesStoringService.DeleteFsoAsync(new DeleteFsoRequest() { FsoId = fsoId.Value.ToGrpcGuid() }, _configuration.ToMetadata(), cancellationToken: token);
            return Ok<Unit, ServiceError>(new Unit());
        } catch (RpcException exception) {
            return Err<Unit, ServiceError>(_exceptionConverter.Convert(exception));
        }
    }

    public Task<Result<Fso, ServiceError>> GetFsoByIdAsync(FsoId fsoId, CancellationToken cancellationToken = default)
        => Wrap(async () => (await _filesStoringService.GetFsoAsync(
                        new GetFsoRequest { FsoId = fsoId.Value.ToGrpcGuid() },
                        _configuration.ToMetadata(),
                        cancellationToken: cancellationToken)
                    ).ToFso());

    public Task<Result<Fso, ServiceError>> GetFsoByPathAsync(PathData pathData, CancellationToken cancellationToken = default)
        => Wrap(async () => (await _filesStoringService.GetFsoAsync(
                        new GetFsoRequest { Path = pathData.ToRpcPathData() },
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
        try {
            var response = await _filesStoringService.GetRootAsync(new EmptyMessage(), _configuration.ToMetadata(), cancellationToken: cancellationToken);
            return Ok<Directory, ServiceError>(new(response.FsoId.ToGuid().ToFsoId(), response.Data.ToFsData()) { MaybeChildren = response.DirectoryData.ToFsos() });
        } catch (RpcException exception) {
            return Err<Directory, ServiceError>(_exceptionConverter.Convert(exception));
        }
    }

    public async Task<Result<User, ServiceError>> GetSelf(CancellationToken cancellationToken = default) {
        try {
            var user = await _filesStoringService.GetSelfAsync(new EmptyMessage(), _configuration.ToMetadata(), cancellationToken: cancellationToken);
            return Ok<User, ServiceError>(user.ToUser());
        } catch (RpcException exception) {
            return Err<User, ServiceError>(_exceptionConverter.Convert(exception));
        }
    }


    public Task<Result<Directory, ServiceError>> MakeDirectory(PathData pathData, Directory dir, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }

    public Task<Result<Symlink, ServiceError>> MakeLink(PathData pathData, Symlink link, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }

    public Task<Result<Unit, ServiceError>> ReplaceFileById(FsoId id, ByteString bytes, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }

    public Task<Result<Unit, ServiceError>> ReplaceFileByPath(PathData pathData, ByteString bytes, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }

    public Task<Result<File, ServiceError>> SaveFile(ByteString bytes, PathData pathData, File file, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }
}
public record BackendConfiguration(string AuthToken);

public static class BackendConfigurationExt {
    extension(BackendConfiguration configuration) {
        public Metadata ToMetadata() {
            return new Metadata {
                { Constants.AUTHORIZATION, configuration.AuthToken }
            };
        }
    }
}
