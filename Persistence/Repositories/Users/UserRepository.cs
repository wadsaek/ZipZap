// UserRepository.cs - Part of the ZipZap project for storing files online
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.LangExt.Extensions;
using ZipZap.LangExt.Helpers;
using ZipZap.Persistence.Data;
using ZipZap.Persistence.Extensions;
using ZipZap.Persistence.Models;

namespace ZipZap.Persistence.Repositories;

internal class UserReposirory : IUserRepository {
    private readonly NpgsqlConnection _conn;
    private readonly EntityHelper<UserInner, User, Guid> _userHelper;
    private readonly EntityHelper<FsoInner, Fso, Guid> _fsoHelper;
    private readonly IBasicRepository<User, UserInner, Guid> _basic;
    private readonly IFsosRepository _fsosRepo;

    public UserReposirory(
            NpgsqlConnection conn,
            EntityHelper<UserInner, User, Guid> userHelper,
            EntityHelper<FsoInner, Fso, Guid> fsoHelper,
            IBasicRepository<User, UserInner, Guid> basic,
            IFsosRepository fsosRepo) {
        _conn = conn;
        _userHelper = userHelper;
        _fsoHelper = fsoHelper;
        _basic = basic;
        _fsosRepo = fsosRepo;
    }
    private string UserTableName => _userHelper.TableName;
    private string FileTableName => _fsoHelper.TableName;

    public Task<IEnumerable<User>> GetAll(CancellationToken token = default)
        => _basic.GetAll(token);

    public async Task<User?> GetByIdAsync(UserId id, CancellationToken token = default)
        => await GetByParameter(
            $"{UserTableName}.{_userHelper.GetColumnName(nameof(UserInner.Id))}",
            new NpgsqlParameter<Guid> { Value = id.Value },
            token
        );

    public Task<Result<Unit, DbError>> DeleteAsync(User entity, CancellationToken token = default)
        => _fsosRepo.DeleteAsync(entity.Root, token);

    public async Task<Result<Unit, DbError>> DeleteAsync(UserId id, CancellationToken cancellationToken = default) {
        var deleted = await DeleteRangeAsync([id], cancellationToken);
        return deleted.SelectMany(DbHelper.EnsureSingle);
    }

    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<User> entities, CancellationToken token = default)
        => _fsosRepo.DeleteRangeAsync(entities.Select(u => u.Root.Id), token);

    public async Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<UserId> ids, CancellationToken cancellationToken = default) {
        var idsList = ids.ToArray();
        await using var disposable = await _conn.OpenAsyncDisposable(cancellationToken);
        await using var transaction = await _conn.BeginTransactionAsync(cancellationToken);
        var cmdBuilder = new StringBuilder($"""
            DELETE FROM {FileTableName}
            WHERE {FileTableName}.{_fsoHelper.IdCol} IN
            (SELECT {FileTableName}.{_fsoHelper.IdCol} FROM {UserTableName}
             JOIN {FileTableName} ON
             {UserTableName}.{_userHelper.GetColumnName(nameof(UserInner.Root))}
             = {FileTableName}.{_fsoHelper.IdCol}
             WHERE {UserTableName}.{_userHelper.IdCol} IN
             (
            """);
        var parameters = new NpgsqlParameter<Guid>[idsList.Length];
        foreach (var (i, id) in idsList.Index()) {
            var index = i + 1;
            cmdBuilder.AppendFormat("${0}, ", index);
            parameters[i] = new NpgsqlParameter<Guid> { Value = id.Value };
        }
        cmdBuilder.RemoveLastCharacters(2);
        cmdBuilder.Append("""
             )
            )
            """);
        var cmd = _conn.CreateCommand(cmdBuilder.ToString());
        cmd.Parameters.AddRange(parameters.ToArray());
        var deleted = await cmd.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new Ok<int, DbError>(deleted);
    }

    public Task<Result<Unit, DbError>> UpdateAsync(User entity, CancellationToken token = default)
        => _basic.UpdateAsync(entity, token);

    public Task<Result<User, DbError>> CreateAsync(User createEntity, CancellationToken token = default)
        => _basic.CreateAsync(createEntity, token);

    public Task<Result<IEnumerable<User>, DbError>> CreateRangeAsync(IEnumerable<User> entities, CancellationToken token = default)
        => _basic.CreateRangeAsync(entities, token);

    public async Task<User?> GetUserByUsername(string username, CancellationToken token = default)
        => await GetByParameter(
            $"{UserTableName}.{_userHelper.GetColumnName(nameof(UserInner.Username))}",
            new NpgsqlParameter<string> { Value = username },
            token
        );

    private async Task<User?> GetByParameter<T>(string filterColumn, NpgsqlParameter<T> npgsqlParameter, CancellationToken cancellationToken = default) {
        await using var disposable = await _conn.OpenAsyncDisposable(cancellationToken);
        var cmdBuilder = new StringBuilder($"""
                    SELECT {_userHelper.SqlFieldsInOrder}, {_fsoHelper.SqlFieldsInOrder} FROM {_userHelper.TableName}
                    LEFT JOIN {FileTableName} ON
                    {_userHelper.TableName}.{_userHelper.GetColumnName(nameof(UserInner.Root))} =
                    {_fsoHelper.TableName}.{_fsoHelper.GetColumnName(nameof(FsoInner.Id))}
                    WHERE {filterColumn} = $1
                    LIMIT 1
                    """);
        var cmd = _conn.CreateCommand(cmdBuilder.ToString());
        cmd.Parameters.Add(npgsqlParameter);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        User? user = null;
        while (await reader.ReadAsync(cancellationToken)) {
            if (user is not null)
                throw new System.IO.InvalidDataException();
            user = await _userHelper.Parse(reader, cancellationToken);
            user = user with { Root = (await _fsoHelper.Parse(reader, cancellationToken) as Directory)! };
        }
        return user;
    }

}
