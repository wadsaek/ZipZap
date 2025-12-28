using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Persistence.Repositories;

namespace ZipZap.FileService.Services;

public class UserService : IUserService {
    private readonly IUserRepository _repo;

    private static byte[] HashPassword(string password) => SHA512.HashData(Encoding.UTF8.GetBytes(password));

    public UserService(IUserRepository repo) {
        _repo = repo;
    }

    private static bool UserHasPassword(User user, string password) {
        var passwordHash = HashPassword(password);
        return user.PasswordHash.SequenceCompareTo(passwordHash) == 0;
    }

    public async Task<string?> Login(string username, string password) {
        var user = await _repo.GetUserByUsername(username);
        user = user.Where(u => UserHasPassword(u, password));
        if (user is null)
            return null;

        return $"Bearer {user.Id} {password}";
    }

    public async Task<User?> GetUser(string token) {
        var split = token.Split(' ');
        if (split.Length < 3 || split[0] != "Bearer") return null;
        if (!Guid.TryParse(split[1], out var id)) return null;
        var uId = new UserId(id);
        var password = split[2];
        return (await _repo.GetByIdAsync(uId))
            .Where(user => UserHasPassword(user, password));
    }
}

