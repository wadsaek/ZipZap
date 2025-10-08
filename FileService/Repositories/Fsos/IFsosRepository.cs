using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZipZap.Classes;

namespace ZipZap.FileService.Repositories;

public interface IFsosRepository : IRepository<Fso, FsoId> {
    public Task<IEnumerable<Fso>> GetAllByDirectory(Directory location, CancellationToken token = default);
}
