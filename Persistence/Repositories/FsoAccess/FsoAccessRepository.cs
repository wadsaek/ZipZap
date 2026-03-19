// FsoAccessRepository.cs - Part of the ZipZap project for storing files online
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

internal class FsoAccessesRepository : IFsoAccessesRepository {
    private readonly NpgsqlConnection _conn;
    private readonly EntityHelper<FsoAccessInner, FsoAccessRaw, Guid> _fsoAccessHelper;
    private readonly EntityHelper<UserInner, User, Guid> _userHelper;
    private readonly EntityHelper<FsoInner, Fso, Guid> _fsoHelper;
    private readonly ExceptionConverter<DbError> _converter;
    private readonly IBasicRepository<FsoAccessRaw, FsoAccessInner, Guid> _basic;

    public FsoAccessesRepository(
        EntityHelper<FsoInner, Fso, Guid> fsoHelper,
        ExceptionConverter<DbError> converter,
        IBasicRepository<FsoAccessRaw, FsoAccessInner, Guid> basic,
        NpgsqlConnection conn,
        EntityHelper<FsoAccessInner, FsoAccessRaw, Guid> fsoAccessHelper,
        EntityHelper<UserInner, User, Guid> userHelper) {
        _fsoHelper = fsoHelper;
        _converter = converter;
        _basic = basic;
        _conn = conn;
        _fsoAccessHelper = fsoAccessHelper;
        _userHelper = userHelper;
    }

    // TODO: make a base function for this and GetByParameter
    public async Task<IEnumerable<FsoAccess>> GetAllFull(CancellationToken cancellationToken = default) {
        await using var disposable = await _conn.OpenAsyncDisposable(cancellationToken);
        var cmdBuilder = new StringBuilder($"""
                    SELECT {_fsoAccessHelper.SqlFieldsInOrder}, {_fsoHelper.SqlFieldsInOrder}, {_userHelper.SqlFieldsInOrder} FROM {_fsoAccessHelper.TableName}
                    LEFT JOIN {_fsoHelper.TableName} ON
                    {_fsoAccessHelper.TableName}.{_fsoAccessHelper.GetColumnName(nameof(FsoAccessInner.FsoId))} =
                    {_fsoHelper.TableName}.{_fsoHelper.GetColumnName(nameof(FsoInner.Id))}
                    LEFT JOIN {_userHelper.TableName} ON
                    {_fsoAccessHelper.TableName}.{_fsoAccessHelper.GetColumnName(nameof(FsoAccessInner.UserId))} =
                    {_userHelper.TableName}.{_userHelper.GetColumnName(nameof(UserInner.Id))};
                    """);
        var cmd = _conn.CreateCommand(cmdBuilder.ToString());
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        List<FsoAccess> fsoAccesses = [];
        while (await reader.ReadAsync(cancellationToken)) {
            var raw = await _fsoAccessHelper.Parse(reader, cancellationToken);
            var fso = await _fsoHelper.Parse(reader, cancellationToken);
            var user = await _userHelper.Parse(reader, cancellationToken);
            fsoAccesses.Add(new(raw.Id, fso, user));
        }
        return fsoAccesses;
    }
    public Task<IEnumerable<FsoAccessRaw>> GetAll(CancellationToken cancellationToken = default) {
        return _basic.GetAll(cancellationToken);
    }

    public Task<Result<FsoAccessRaw, DbError>> CreateAsync(FsoAccessRaw createEntity, CancellationToken token = default) {
        return _basic.CreateAsync(createEntity, token);
    }

    public Task<Result<IEnumerable<FsoAccessRaw>, DbError>> CreateRangeAsync(IEnumerable<FsoAccessRaw> entities, CancellationToken token = default) {
        return _basic.CreateRangeAsync(entities, token);
    }

    public Task<Result<Unit, DbError>> DeleteAsync(FsoAccessRaw entity, CancellationToken token = default) {
        return DeleteAsync(entity.Id, token);
    }

    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<FsoAccessRaw> entities, CancellationToken token = default) {
        return DeleteRangeAsync(entities.Select(e => e.Id), token);
    }

    public Task<Result<Unit, DbError>> UpdateAsync(FsoAccessRaw entity, CancellationToken token = default) {
        return _basic.UpdateAsync(entity, token);
    }

    public async Task<FsoAccess?> GetByIdAsyncFull(FsoAccessId id, CancellationToken token = default) {
        var accesses = await GetByParameter(
            $"{_fsoAccessHelper.TableName}.{_fsoAccessHelper.IdCol}",
            new NpgsqlParameter<Guid> { Value = id.Value },
            token
        );
        return accesses switch {
            [var access] => access,
            [] => null,
            _ => throw new InvalidDataException("Got more accesses than one by id")
        };
    }
    public Task<FsoAccessRaw?> GetByIdAsync(FsoAccessId id, CancellationToken token = default) {
        return _basic.GetByIdAsync(id.Value, token);
    }

    public Task<Result<Unit, DbError>> DeleteAsync(FsoAccessId id, CancellationToken token = default) {
        return _basic.DeleteAsync(id.Value, token);
    }

    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<FsoAccessId> ids, CancellationToken token = default) {
        return _basic.DeleteRangeAsync(ids.Select(i => i.Value), token);
    }

    public async Task<IEnumerable<FsoAccess>> GetForFsoId(FsoId userId, CancellationToken token = default)
        => await GetByParameter(
            $"{_fsoHelper.TableName}.{_fsoHelper.GetColumnName(nameof(FsoInner.Id))}",
            new NpgsqlParameter<Guid> { Value = userId.Value },
            token
        );

    public async Task<IEnumerable<FsoAccess>> GetForUserId(UserId userId, CancellationToken token = default)
        => await GetByParameter(
            $"{_userHelper.TableName}.{_userHelper.GetColumnName(nameof(UserInner.Id))}",
            new NpgsqlParameter<Guid> { Value = userId.Value },
            token
        );

    private async Task<List<FsoAccess>> GetByParameter<T>(string filterColumn, NpgsqlParameter<T> npgsqlParameter, CancellationToken cancellationToken = default) {
        await using var disposable = await _conn.OpenAsyncDisposable(cancellationToken);
        var cmdBuilder = new StringBuilder($"""
                    SELECT {_fsoAccessHelper.SqlFieldsInOrder}, {_fsoHelper.SqlFieldsInOrder}, {_userHelper.SqlFieldsInOrder} FROM {_fsoAccessHelper.TableName}
                    LEFT JOIN {_fsoHelper.TableName} ON
                    {_fsoAccessHelper.TableName}.{_fsoAccessHelper.GetColumnName(nameof(FsoAccessInner.FsoId))} =
                    {_fsoHelper.TableName}.{_fsoHelper.GetColumnName(nameof(FsoInner.Id))}
                    LEFT JOIN {_userHelper.TableName} ON
                    {_fsoAccessHelper.TableName}.{_fsoAccessHelper.GetColumnName(nameof(FsoAccessInner.UserId))} =
                    {_userHelper.TableName}.{_userHelper.GetColumnName(nameof(UserInner.Id))}
                    WHERE {filterColumn} = $1;
                    """);
        var cmd = _conn.CreateCommand(cmdBuilder.ToString());
        cmd.Parameters.Add(npgsqlParameter);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        List<FsoAccess> fsoAccesses = [];
        while (await reader.ReadAsync(cancellationToken)) {
            var raw = await _fsoAccessHelper.Parse(reader, cancellationToken);
            var fso = await _fsoHelper.Parse(reader, cancellationToken);
            var user = await _userHelper.Parse(reader, cancellationToken);
            fsoAccesses.Add(new(raw.Id, fso, user));
        }
        return fsoAccesses;
    }
}
