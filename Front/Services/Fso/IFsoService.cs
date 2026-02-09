using System.Threading;
using System.Threading.Tasks;

using ZipZap.Front.Handlers;

namespace ZipZap.Front.Services;

public interface IFsoService {
    public Task<FsoStatus> GetFsoBySpecificationAsync(FileSpecification specification, BackendConfiguration backendConfiguration, CancellationToken cancellationToken);
}
