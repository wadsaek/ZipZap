using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;

namespace ZipZap.Persistence.Repositories;

public interface IFsoAccessesRepository : IRepository<FsoAccess, FsoAccessId> {
    public Task<IEnumerable<FsoAccess>> GetForFsoId(FsoId fsoId, CancellationToken cancellationToken = default);
    public Task<IEnumerable<FsoAccess>> GetForUserId(UserId userId, CancellationToken cancellationToken = default);
}
