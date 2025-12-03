using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;

namespace ZipZap.FileService.Services;

public interface IUserService {
    Task<string?> Login(string username, string password);
    Task<User?> GetUser(string token);
}

