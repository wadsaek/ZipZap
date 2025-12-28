using System.Threading.Tasks;

using ZipZap.Classes;

namespace ZipZap.FileService.Services;

public interface IUserService {
    Task<string?> Login(string username, string password);
    Task<User?> GetUser(string token);
}

