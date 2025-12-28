using System;
using System.Threading;
using System.Threading.Tasks;

using Google.Protobuf;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;

namespace ZipZap.Front.Services;

public interface IBackend {
    public Task<Result<File, ServiceError>> SaveFile(ByteString bytes, File file, CancellationToken cancellationToken = default);
    public Task<Result<File, ServiceError>> SaveFile(ByteString bytes, File file, string path, CancellationToken cancellationToken = default);
    public Task<Result<Directory, ServiceError>> MakeDirectory(Directory dir, CancellationToken cancellationToken = default);
    public Task<Result<Directory, ServiceError>> MakeDirectory(Directory dir, string path, CancellationToken cancellationToken = default);
    public Task<Result<Symlink, ServiceError>> MakeLink(Symlink link, CancellationToken cancellationToken = default);
    public Task<Result<Symlink, ServiceError>> MakeLink(Symlink link, string path, CancellationToken cancellationToken = default);
    public Task<Result<Fso, ServiceError>> GetFsoByIdAsync(FsoId fsoId, CancellationToken cancellationToken = default);
    public Task<Result<Fso, ServiceError>> GetFsoByPathAsync(PathData pathData, CancellationToken cancellationToken = default);
    public Task<Result<Directory, ServiceError>> GetRoot(CancellationToken cancellationToken = default);
    public Task<Result<Unit, ServiceError>> DeleteFso(FsoId fsoId, DeleteFlags flags, CancellationToken token = default);
    // NOTE: notice the lack of CancellationToken :))
    public Task<Result<Unit, ServiceError>> DeleteFrenchLanguagePack();
    public Task<Result<Unit, ServiceError>> ReplaceFileById(FsoId id, ByteString bytes, CancellationToken cancellationToken = default);
    public Task<Result<Unit, ServiceError>> ReplaceFileByPath(PathData pathData, ByteString bytes, CancellationToken cancellationToken = default);

    public Task<Result<User, ServiceError>> GetSelf(CancellationToken cancellationToken = default);
}

public abstract record LoginError;
public sealed record WrongCredentials : LoginError {
    public override string ToString() => "One or more fields is wrong";
}
public sealed record EmptyCredentials : LoginError {
    public override string ToString() => "One or more fields is empty";
}
public sealed record LoginServiceError(ServiceError ServiceError) : LoginError;


[Flags]
public enum DeleteFlags {
    Empty = 0

}
