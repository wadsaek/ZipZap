// UserService.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes.Helpers;
using ZipZap.LangExt.Extensions;
using ZipZap.Persistence.Models;
using ZipZap.Persistence.Repositories;

namespace ZipZap.FileService.Services;

public class UserService : IUserService {
    private readonly IUserRepository _repo;
    private readonly ITokenService _tokenService;
    private readonly IFsosService _fsosService;

    public byte[] HashPassword(string password) => SHA512.HashData(Encoding.UTF8.GetBytes(password));

    public UserService(IUserRepository repo, ITokenService tokenService, IFsosService fsosService) {
        _repo = repo;
        _tokenService = tokenService;
        _fsosService = fsosService;
    }

    private bool UserHasPassword(User user, string password) {
        var passwordHash = HashPassword(password);
        return user.PasswordHash.SequenceCompareTo(passwordHash) == 0;
    }

    public async Task<string?> Login(string username, string password, CancellationToken cancellationToken) {
        var user = await _repo.GetUserByUsername(username, cancellationToken);
        user = user.Filter(u => UserHasPassword(u, password));
        if (user is null)
            return null;

        return _tokenService.GenerateToken(user);
    }

    public async Task<User?> GetUser(string token, CancellationToken cancellationToken) {
        if (!_tokenService.TryGetUserId(token, out var id)) return null;

        var user = await _repo.GetByIdAsync(id, cancellationToken);
        return user;
    }

    public async Task<Result<Unit, DbError>> RemoveUser(UserId id, CancellationToken cancellationToken) {
        var user = await _repo.GetByIdAsync(id, cancellationToken);
        if (user is null) return Err<Unit,DbError>(new DbError.NothingChanged());

        return await _fsosService.RemoveFso(user.Root,DeleteOptions.All,cancellationToken);
    }

    public Task<IEnumerable<User>> GetAllUsers(CancellationToken token) {
        return _repo.GetAll(token);
    }

    public Task<Result<User, DbError>> CreateAsync(User user, CancellationToken token) {
        return _repo.CreateAsync(user, token);
    }
}

