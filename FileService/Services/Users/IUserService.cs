// IUserService.cs - Part of the ZipZap project for storing files online
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
    Task<Result<User, DbError>> CreateAsync(User user, CancellationToken cancellationToken);
}

