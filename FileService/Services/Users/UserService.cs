using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.LangExt.Extensions;
using ZipZap.Persistence.Repositories;

namespace ZipZap.FileService.Services;

public class UserService : IUserService {
    private readonly IUserRepository _repo;
    private readonly RsaSecurityKey _key;

    private static byte[] HashPassword(string password) => SHA512.HashData(Encoding.UTF8.GetBytes(password));

    public UserService(IUserRepository repo, RsaSecurityKey key) {
        _repo = repo;
        _key = key;
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

        var expiration = DateTime.UtcNow.AddHours(5);
        Claim[] claims = [
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
            new Claim("role",user.Role.ToString())
        ];
        var creds = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "http://localhost:5210",
            audience: "http://localhost:5210",
            claims,
            expires: expiration,
            signingCredentials: creds
        );
        string tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return $"Bearer {tokenString}";
    }

    public async Task<User?> GetUser(string token) {
        var split = token.Split(' ');
        if (split.Length != 2 || split[0] != "Bearer") return null;
        var jwt = new JwtSecurityToken(split[1]);

        if (!Guid.TryParse(jwt.Subject, out var guid)) return null;
        var id = guid.ToUserId();
        var user = await _repo.GetByIdAsync(id);
        return user;
    }
}

