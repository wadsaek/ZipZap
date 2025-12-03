using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;

namespace ZipZap.Persistance.Repositories;

public interface IUserRepository : IRepository<User, UserId> {
    public Task<User?> GetUserByUsername(string username, CancellationToken cancellationToken = default);
}
