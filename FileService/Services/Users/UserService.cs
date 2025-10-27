using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Persistance.Repositories;

using static ZipZap.Classes.Helpers.Constructors;

namespace ZipZap.FileService.Services;

public class UserService : IUserService {
    private readonly IUserRepository _repo;

    public UserService(IUserRepository repo) {
        _repo = repo;
    }

    public async Task<Option<User>> MaybeGetUser(string token) {
        var split = token.Split(' ');
        if (split.Length < 3 || split[0] != "Bearer") return None<User>();
        if (!Guid.TryParse(split[1], out var id)) return None<User>();
        var uId = new UserId(id);
        var hash = SHA512.HashData(Encoding.UTF8.GetBytes(split[2]));
        return (await _repo.GetByIdAsync(uId))
            .Where(user =>
                user.PasswordHash.SequenceCompareTo(hash) == 0
            );
    }
}

