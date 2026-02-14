using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Front.Handlers;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Services;

public interface IFsoService {
    public Task<FsoStatus> GetFsoBySpecificationAsync(FileSpecification specification, BackendConfiguration backendConfiguration, CancellationToken cancellationToken);
    public Task<Result<IEnumerable<string>, ServiceError>> GetFullPath(FsoId fsoId, BackendConfiguration backendConfiguration, CancellationToken cancellationToken);
    public Task<FsoStatus> GetFsoWithRoot(PathData path, FsoId anchor, BackendConfiguration backendConfiguration, CancellationToken cancellationToken);
}
