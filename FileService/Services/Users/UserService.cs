using System;
using System.Linq;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.FileService.Repositories;

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
        return await _repo.GetByIdAsync(uId);
    }
}

