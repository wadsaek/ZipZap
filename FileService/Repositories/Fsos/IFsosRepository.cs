using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;

namespace ZipZap.FileService.Repositories;

public interface IFsosRepository : IRepository<Fso, FsoId> {
    public Task<IEnumerable<Fso>> GetAllByDirectory(Directory location, CancellationToken token = default);
    public Task<Option<Directory>> GetRootDirectory(FsoId id, CancellationToken token = default);
}
