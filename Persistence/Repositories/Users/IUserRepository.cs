using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;

namespace ZipZap.Persistence.Repositories;

public interface IUserRepository : IRepository<User, UserId> {
    public Task<User?> GetUserByUsername(string username, CancellationToken cancellationToken = default);
}
