using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.Persistence.Extensions;
using ZipZap.Persistence.Data;
using ZipZap.Persistence.Models;

namespace ZipZap.Persistence.Repositories;

internal interface IBasicRepository<TEntity, in TInner, in TId>
where TInner : ITranslatable<TEntity>, ISqlRetrievable
where TId : struct {
    Task<Result<TEntity, DbError>> CreateAsync(TEntity createEntity, CancellationToken token = default);
    Task<Result<TEntity, DbError>> CreateAsync(TInner createEntity, CancellationToken token = default);
    Task<Result<IEnumerable<TEntity>, DbError>> CreateRangeAsync(IEnumerable<TInner> entities, CancellationToken token = default);
    Task<Result<IEnumerable<TEntity>, DbError>> CreateRangeAsync(IEnumerable<TEntity> entities, CancellationToken token = default);
    Task<Result<Unit, DbError>> DeleteAsync(TId id, CancellationToken token = default);
    Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<TId> ids, CancellationToken token = default);
    Task<Result<int, DbError>> DeleteRangeAsyncWithOpenConn(IEnumerable<TId> ids, CancellationToken token = default);
    Task<IEnumerable<TEntity>> Get(string? condition, string? postCondition, Action<NpgsqlCommand>? commandCallback, CancellationToken token = default);
    Task<IEnumerable<TEntity>> Get(Func<NpgsqlConnection, NpgsqlCommand> commandInitalizer, CancellationToken token = default);
    Task<IEnumerable<TEntity>> GetAll(CancellationToken token = default);
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken token = default);
    Task<Result<Unit, DbError>> UpdateAsync(TEntity entity, CancellationToken token = default);
}

internal class BasicRepository<TEntity, TInner, TId> : IBasicRepository<TEntity, TInner, TId>
where TInner : ITranslatable<TEntity>, ISqlRetrievable
where TId : struct {
    private readonly EntityHelper<TInner, TEntity, TId> _helper;


    private readonly NpgsqlConnection _conn;

    public BasicRepository(NpgsqlConnection conn, EntityHelper<TInner, TEntity, TId> helper) {
        _helper = helper;
        _conn = conn;
    }

    private string TableName => _helper.TableName;
    private NpgsqlCommand BuildCommand(Action<NpgsqlCommand>? commandCallback, string? condition, string? postCondition) {
        var cmdBuilder = new StringBuilder();
        cmdBuilder.AppendLine($"SELECT {_helper.SqlFieldsInOrder} FROM {TableName} ");
        if (condition is not null) cmdBuilder.AppendLine($"WHERE {condition}");
        if (postCondition is not null) cmdBuilder.AppendLine(postCondition);
        cmdBuilder.Append(';');

        var cmdText = cmdBuilder.ToString();
        var cmd = _conn.CreateCommand(cmdText);
        commandCallback?.Invoke(cmd);
        return cmd;
    }

    public async Task<IEnumerable<TEntity>> Get(
            string? condition, string? postCondition,
            Action<NpgsqlCommand>? commandCallback, CancellationToken token = default
        )
        => await Get(_ => BuildCommand(commandCallback, condition, postCondition), token);

    public async Task<IEnumerable<TEntity>> Get(Func<NpgsqlConnection, NpgsqlCommand> commandInitalizer, CancellationToken token = default) {
        await using var cmd = commandInitalizer(_conn);
        await using var disposable = await _conn.OpenAsyncDisposable(token);
        await using var reader = await cmd.ExecuteReaderAsync(token);
        var fsos = new List<TEntity>();
        while (await reader.ReadAsync(token)) {
            var fso = await _helper.Parse(reader, token);
            fsos.Add(fso);
        }
        return fsos;
    }
    public async Task<IEnumerable<TEntity>> GetAll(CancellationToken token = default)
        => await Get(null, null, null, token);


    public Task<Result<TEntity, DbError>> CreateAsync(TEntity createEntity, CancellationToken token = default) => CreateAsync((TInner)TInner.From(createEntity), token);
    public async Task<Result<TEntity, DbError>> CreateAsync(TInner createEntity, CancellationToken token = default) {
        var fields = _helper.SqlFieldsList.Where(field => field.sqlName != _helper.IdCol).ToList();
        var parameters = fields.Select((_, index) => index + 1)
            .Select(index => $"${index}")
            .ConcatenateWith(", ");
        await using var cmd = _conn.CreateCommand($"""
                INSERT INTO {TableName}
                ({fields.Select(f => f.sqlName).ConcatenateWith(", ")})
                VALUES ( {parameters} )
                RETURNING {_helper.IdCol};
                """);
        EntityHelper<TInner, TEntity, TId>.FillParameters(cmd, createEntity, fields);
        await using var disposable = await _conn.OpenAsyncDisposable(token);
        try {
            if (await cmd.ExecuteScalarAsync(token) is not TId id)
                return new Err<TEntity, DbError>(new DbError.ScalarNotReturned());
            return new Ok<TEntity, DbError>(_helper.CloneWithId(createEntity, id).Into());
        } catch (PostgresException exception) {
            return exception switch {
                { SqlState: PostgresErrorCodes.UniqueViolation }
                    => new(new DbError.UniqueViolation()),
                _ => new Err<TEntity, DbError>(new DbError.Unknown())
            };
        }
    }
    public async Task<Result<IEnumerable<TEntity>, DbError>> CreateRangeAsync(IEnumerable<TInner> entities, CancellationToken token = default) {
        var fields = _helper.SqlFieldsList.Where(field => field.sqlName != _helper.IdCol).ToList();
        var entityList = entities.ToList();
        var cmdBuilder = new StringBuilder(
                $"""
                INSERT INTO {TableName}
                ({fields.Select(f => f.sqlName).ConcatenateWith(", ")})
                VALUES

                """
                );
        await using var cmd = _conn.CreateCommand();
        foreach (var (i, fso) in entityList.Index()) {
            var offset = i * fields.Count;
            cmdBuilder.AppendFormat("( ${0} ),",
                    fields
                    .Select((_, index) => index + offset + 1)
                    .Select(ind => $"${ind}")
                    .ConcatenateWith(", "));
            EntityHelper<TInner, TEntity, TId>.FillParameters(cmd, fso, fields);
        }
        cmdBuilder.RemoveLastCharacters(2);
        cmdBuilder.AppendLine($"RETURNING {_helper.IdCol};");
        cmd.CommandText = cmdBuilder.ToString();
        await using var _ = await _conn.OpenAsyncDisposable(token);
        var reader = await cmd.ExecuteReaderAsync(token);
        var j = 0; // `i` is used in the foreach loop
        while (await reader.ReadAsync(token)) {
            var id = await reader.GetFieldValueAsync<TId>(0, token);
            entityList[j] = _helper.CloneWithId(entityList[j], id);
            j++;
        }
        await using var disposable = await _conn.OpenAsyncDisposable(token);
        return new Ok<IEnumerable<TEntity>, DbError>(entityList.Select(e => e.Into()));
    }
    public Task<Result<IEnumerable<TEntity>, DbError>> CreateRangeAsync(IEnumerable<TEntity> entities, CancellationToken token = default) => CreateRangeAsync(entities.Select(e => (TInner)TInner.From(e)), token);

    public async Task<Result<Unit, DbError>> UpdateAsync(TEntity entity, CancellationToken token = default) {

        await using var disp = await _conn.OpenAsyncDisposable(token);
        await using var transaction = await _conn.BeginTransactionAsync(token);
        await using var cmd = _conn.CreateCommand($"""
                UPDATE {TableName} SET
                {_helper.SqlFieldsList.Select((field, ind) => $"{field.sqlName}={ind + 1}")}
                WHERE 
                RETURNING {_helper.IdCol};
                """);
        _helper.FillParameters(cmd, (TInner)TInner.From(entity));
        var count = await cmd.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
        return DbHelper.EnsureSingle(count);
    }

    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken token = default)
        =>
            (await Get($"{TableName}.{_helper.IdCol} = $1", "LIMIT 1",
                cmd => cmd.Parameters.Add(new NpgsqlParameter<TId> { Value = id }), token))
            .FirstOrDefault();

    public async Task<Result<int, DbError>> DeleteRangeAsyncWithOpenConn(IEnumerable<TId> ids, CancellationToken token = default) {
        var cmdBuilder = new StringBuilder();
        cmdBuilder.AppendLine($"DELETE FROM {TableName}");
        cmdBuilder.Append($"WHERE {TableName}.{_helper.IdCol} IN (");
        List<NpgsqlParameter> parameters = [];
        foreach (var (i, id) in ids.Index()) {
            cmdBuilder.Append($"${i + 1}, ");
            parameters.Add(new NpgsqlParameter<TId> { Value = id });

        }

        var len = cmdBuilder.Length;
        cmdBuilder.Remove(len - 2, 2);
        cmdBuilder.Append(");");
        await using var cmd = _conn.CreateCommand(cmdBuilder.ToString());
        cmd.Parameters.AddRange(parameters.ToArray());
        var result = await cmd.ExecuteNonQueryAsync(token);
        return new Ok<int, DbError>(result);
    }
    public async Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<TId> ids, CancellationToken token = default) {
        await using var disp = await _conn.OpenAsyncDisposable(token);
        var result = await DeleteRangeAsyncWithOpenConn(ids, token);
        return result;
    }

    public async Task<Result<Unit, DbError>> DeleteAsync(TId id, CancellationToken token = default) {
        await using var disp = await _conn.OpenAsyncDisposable(token);
        await using var transaction = await _conn.BeginTransactionAsync(token);
        var deleteResult = await DeleteRangeAsyncWithOpenConn([id], token);
        await transaction.CommitAsync(token);
        return deleteResult.SelectMany(DbHelper.EnsureSingle);
    }

}
