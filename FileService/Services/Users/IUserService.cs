using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;

namespace ZipZap.FileService.Services;

public interface IUserService {
    Task<Option<string>> Login(string username, string password);
    Task<Option<User>> MaybeGetUser(string token);
}

