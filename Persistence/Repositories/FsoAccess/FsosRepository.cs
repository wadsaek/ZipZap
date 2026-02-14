using System;
using System.Collections.Generic;
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
    private readonly EntityHelper<FsoAccessInner, FsoAccess, Guid> _fsoAccessHelper;
    private readonly EntityHelper<UserInner, User, Guid> _userHelper;
    private readonly EntityHelper<FsoInner, Fso, Guid> _fsoHelper;
    private readonly ExceptionConverter<DbError> _converter;
    private readonly IBasicRepository<FsoAccess, FsoAccessInner, Guid> _basic;

    public FsoAccessesRepository(
        EntityHelper<FsoInner, Fso, Guid> fsoHelper,
        ExceptionConverter<DbError> converter,
        IBasicRepository<FsoAccess, FsoAccessInner, Guid> basic,
        NpgsqlConnection conn,
        EntityHelper<FsoAccessInner, FsoAccess, Guid> fsoAccessHelper,
        EntityHelper<UserInner, User, Guid> userHelper) {
        _fsoHelper = fsoHelper;
        _converter = converter;
        _basic = basic;
        _conn = conn;
        _fsoAccessHelper = fsoAccessHelper;
        _userHelper = userHelper;
    }

    public Task<IEnumerable<FsoAccess>> GetAll(CancellationToken token = default) {
        return _basic.GetAll(token);
    }

    public Task<Result<FsoAccess, DbError>> CreateAsync(FsoAccess createEntity, CancellationToken token = default) {
        return _basic.CreateAsync(createEntity, token);
    }

    public Task<Result<IEnumerable<FsoAccess>, DbError>> CreateRangeAsync(IEnumerable<FsoAccess> entities, CancellationToken token = default) {
        return _basic.CreateRangeAsync(entities, token);
    }

    public Task<Result<Unit, DbError>> DeleteAsync(FsoAccess entity, CancellationToken token = default) {
        return DeleteAsync(entity.Id, token);
    }

    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<FsoAccess> entities, CancellationToken token = default) {
        return DeleteRangeAsync(entities.Select(e => e.Id), token);
    }

    public Task<Result<Unit, DbError>> UpdateAsync(FsoAccess entity, CancellationToken token = default) {
        return _basic.UpdateAsync(entity, token);
    }

    public Task<FsoAccess?> GetByIdAsync(FsoAccessId id, CancellationToken token = default) {
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
            new NpgsqlParameter<string> { Value = userId.Value },
            token
        );

    public async Task<IEnumerable<FsoAccess>> GetForUserId(UserId userId, CancellationToken token = default)
        => await GetByParameter(
            $"{_userHelper.TableName}.{_userHelper.GetColumnName(nameof(UserInner.Id))}",
            new NpgsqlParameter<string> { Value = userId.Value },
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
            var user = await _fsoAccessHelper.Parse(reader, cancellationToken);
            user = user with { Fso = await _fsoHelper.Parse(reader, cancellationToken), User = await _userHelper.Parse(reader, cancellationToken) };
            fsoAccesses.Add(user);
        }
        return fsoAccesses;
    }
}
