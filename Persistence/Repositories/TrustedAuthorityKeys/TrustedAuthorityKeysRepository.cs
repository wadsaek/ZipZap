// TrustedAuthorityKeysRepository.cs - Part of the ZipZap project for storing files online
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.LangExt.Helpers;
using ZipZap.Persistence.Data;
using ZipZap.Persistence.Extensions;
using ZipZap.Persistence.Models;

namespace ZipZap.Persistence.Repositories;

using Key = TrustedAuthorityKey;
using KeyId = TrustedAuthorityKeyId;
using KeyInner = TrustedAuthorityKeyInner;

internal class TrustedAuthorityKeysRepository : ITrustedAuthorityKeysRepository {
    private readonly NpgsqlConnection _conn;
    private readonly EntityHelper<KeyInner, Key, Guid> _helper;
    private readonly EntityHelper<UserInner, User, Guid> _userHelper;
    private readonly ExceptionConverter<DbError> _converter;
    private readonly IBasicRepository<Key, KeyInner, Guid> _basic;

    public TrustedAuthorityKeysRepository(
        ExceptionConverter<DbError> converter,
        NpgsqlConnection conn,
        EntityHelper<UserInner, User, Guid> userHelper,
        EntityHelper<KeyInner, Key, Guid> helper,
        IBasicRepository<Key, KeyInner, Guid> basic) {
        _converter = converter;
        _basic = basic;
        _conn = conn;
        _userHelper = userHelper;
        _helper = helper;
    }

    public Task<IEnumerable<Key>> GetAll(CancellationToken token = default) {
        return _basic.GetAll(token);
    }

    public Task<Result<Key, DbError>> CreateAsync(Key createEntity, CancellationToken token = default) {
        return _basic.CreateAsync(createEntity, token);
    }

    public Task<Result<IEnumerable<Key>, DbError>> CreateRangeAsync(IEnumerable<Key> entities, CancellationToken token = default) {
        return _basic.CreateRangeAsync(entities, token);
    }

    public Task<Result<Unit, DbError>> DeleteAsync(Key entity, CancellationToken token = default) {
        return DeleteAsync(entity.Id, token);
    }

    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<Key> entities, CancellationToken token = default) {
        return DeleteRangeAsync(entities.Select(e => e.Id), token);
    }

    public Task<Result<Unit, DbError>> UpdateAsync(Key entity, CancellationToken token = default) {
        return _basic.UpdateAsync(entity, token);
    }

    public Task<Result<Unit, DbError>> DeleteAsync(KeyId id, CancellationToken token = default) {
        return _basic.DeleteAsync(id.Id, token);
    }

    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<KeyId> ids, CancellationToken token = default) {
        return _basic.DeleteRangeAsync(ids.Select(i => i.Id), token);
    }

    public async Task<Key?> GetByIdAsync(KeyId id, CancellationToken token = default) {
        var keys = await GetByParameter(
            $"{_helper.TableName}.{_helper.GetColumnName(nameof(KeyInner.Id))}",
            new NpgsqlParameter<Guid> { Value = id.Id },
            token
        );
        return keys switch {
            [] => null,
            [var key] => key,
            _ => throw new InvalidDataException("more than one ssh key for one id")
        };
    }

    public async Task<IEnumerable<Key>> GetForAdminId(UserId userId, CancellationToken token = default)
        => await GetByParameter(
            $"{_userHelper.TableName}.{_userHelper.GetColumnName(nameof(UserInner.Id))}",
            new NpgsqlParameter<Guid> { Value = userId.Value },
            token
        );
    public async Task<IEnumerable<Key>> GetForUsername(string username, CancellationToken cancellationToken = default)
        => await GetByParameter(
            $"{_userHelper.TableName}.{_userHelper.GetColumnName(nameof(UserInner.Username))}",
            new NpgsqlParameter<string> { Value = username },
            cancellationToken
        );

    public async Task<IEnumerable<Key>> GetForServerName(string name, CancellationToken cancellationToken = default)
        => await GetByParameter(
            $"{_helper.GetColumnName(nameof(KeyInner.ServerName))}",
            new NpgsqlParameter<string> { Value = name },
            cancellationToken
        );

    private async Task<List<Key>> GetByParameter<T>(string filterColumn, NpgsqlParameter<T> npgsqlParameter, CancellationToken token)
        => await GetByCondition($"{filterColumn} = $1", npgsqlParameter, token);

    public async Task<IEnumerable<Key>> GetByKey(SshPublicKey key, CancellationToken cancellationToken = default)
        => await GetByCondition(
            $"starts_with({_helper.TableName}.{_helper.GetColumnName(nameof(KeyInner.ServerKey))}, $1)",
            new NpgsqlParameter<string> { Value = key.Value },
            cancellationToken
        );

    private async Task<List<Key>> GetByCondition<T>(string condition, NpgsqlParameter<T> npgsqlParameter, CancellationToken cancellationToken = default) {
        await using var disposable = await _conn.OpenAsyncDisposable(cancellationToken);
        var cmdBuilder = new StringBuilder($"""
                    SELECT {_helper.SqlFieldsInOrder}, {_userHelper.SqlFieldsInOrder} FROM {_helper.TableName}
                    LEFT JOIN {_userHelper.TableName} ON
                    {_helper.TableName}.{_helper.GetColumnName(nameof(KeyInner.AdminId))} =
                    {_userHelper.TableName}.{_userHelper.GetColumnName(nameof(UserInner.Id))}
                    WHERE {condition};
                    """);
        var cmd = _conn.CreateCommand(cmdBuilder.ToString());
        cmd.Parameters.Add(npgsqlParameter);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        List<Key> keys = [];
        while (await reader.ReadAsync(cancellationToken)) {
            var key = await _helper.Parse(reader, cancellationToken);
            key = key with { Admin = await _userHelper.Parse(reader, cancellationToken) };
            keys.Add(key);
        }
        return keys;
    }

}
