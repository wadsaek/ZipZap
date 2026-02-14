using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;

namespace ZipZap.Persistence.Repositories;

public interface IFsosRepository : IRepository<Fso, FsoId> {
    // not an `FsoID`, to signify that it's a directory
    public Task<IEnumerable<Fso>> GetAllByDirectory(MaybeEntity<Directory, FsoId> location, CancellationToken token = default);
    public Task<Fso?> GetByDirectoryAndName(MaybeEntity<Directory, FsoId> location, string name, CancellationToken token = default);
    public Task<Fso?> GetByPath(MaybeEntity<Directory, FsoId> root, string path, CancellationToken token = default);

    public Task<Directory?> GetRootDirectory(FsoId id, CancellationToken token = default);
    public Task<IEnumerable<Fso>> GetFullPathTree(FsoId id, CancellationToken token = default);

    ///<returns>The most deeply nested fso that is a parent of <paramref name="fsoId"/> that is shared with the <paramref name="userId"/> user</returns>
    public Task<Fso?> GetDeepestSharedFso(FsoId fsoId, UserId userId, CancellationToken cancellationToken);

}
