using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.LangExt.Helpers;
using ZipZap.Persistence.Models;

namespace ZipZap.FileService.Services;

public interface IUserService {
    Task<string?> Login(string username, string password);
    Task<User?> GetUser(string token);
    Task<Result<Unit, DbError>> RemoveUser(UserId id);
    Task<IEnumerable<User>> GetAllUsers(CancellationToken token);
    byte[] HashPassword(string password);
    Task<Result<User,DbError>> CreateAsync(User user,CancellationToken cancellationToken);
}

