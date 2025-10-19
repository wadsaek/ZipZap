using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.FileService.Data;
using ZipZap.FileService.Extensions;
using ZipZap.FileService.Models;

using static ZipZap.Classes.Helpers.Constructors;

namespace ZipZap.FileService.Repositories;

class BasicRepository<TEntity, TInner, TId>
where TInner : ITranslatable<TEntity>, ISqlRetrievable
where TId : struct {
    private readonly EntityHelper<TInner, TEntity, TId> _helper;


    private readonly NpgsqlConnection _conn;

    public BasicRepository(NpgsqlConnection conn, EntityHelper<TInner, TEntity, TId> helper) {
        _helper = helper;
        _conn = conn;
    }

    private string TName => _helper.TableName;
    private NpgsqlCommand BuildCommand(Option<Action<NpgsqlCommand>> commandCallback, Option<string> condition, Option<string> postCondition) {
        var cmdBuilder = new StringBuilder();
        cmdBuilder.AppendLine($"SELECT {_helper.SqlFieldsInOrder} FROM {TName} ");
        condition.Select(condition => cmdBuilder.AppendLine($"WHERE {condition}"));
        postCondition.Select(cmdBuilder.AppendLine);
        cmdBuilder.Append(';');

        var cmdText = cmdBuilder.ToString();
        var cmd = _conn.CreateCommand(cmdText);
        commandCallback.Select(callback => { callback(cmd); return new Unit(); });
        return cmd;
    }
    public async Task<IEnumerable<TEntity>> Get(
            Option<string> condition, Option<string> postCondition,
            Option<Action<NpgsqlCommand>> commandCallback, CancellationToken token = default
        ) {
        await using var cmd = BuildCommand(commandCallback, condition, postCondition);
        await using var _disposable = await _conn.OpenAsyncDisposable(token);
        await using var reader = await cmd.ExecuteReaderAsync(token);
        var fsos = new List<TEntity>();
        while (await reader.ReadAsync(token)) {
            var fso = await _helper.Parse(reader, token);
            fsos.Add(fso);
        }
        return fsos;
    }
    public async Task<IEnumerable<TEntity>> GetAll(CancellationToken token = default)
        => await Get(None<string>(), None<string>(), None<Action<NpgsqlCommand>>(), token);


    public Task<Result<TEntity, DbError>> CreateAsync(TEntity createEntity, CancellationToken token = default) => CreateAsync((TInner)TInner.From(createEntity), token);
    public async Task<Result<TEntity, DbError>> CreateAsync(TInner createEntity, CancellationToken token = default) {
        var fields = _helper.SqlFieldsList.Where(field => field.sqlName != _helper.IdCol).ToList();
        var parameters = fields.Select((_, index) => index + 1)
            .Select(index => $"${index}")
            .ConcatenateWith(", ");
        await using var cmd = _conn.CreateCommand($"""
                INSERT INTO {TName}
                ({fields.Select(f => f.sqlName).ConcatenateWith(", ")})
                VALUES ( {parameters} )
                RETURNING {_helper.IdCol};
                """);
        EntityHelper<TInner, TEntity, TId>.FillParameters(cmd, createEntity, fields);
        await using var _disposable = await _conn.OpenAsyncDisposable(token);
        var id = await cmd.ExecuteScalarAsync(token) as TId?;
        if (id is null) return new Err<TEntity, DbError>(new DbError());
        return new Ok<TEntity, DbError>(_helper.CloneWithId(createEntity, id.Value).Into());
    }
    public async Task<Result<IEnumerable<TEntity>, DbError>> CreateRangeAsync(IEnumerable<TInner> entities, CancellationToken token = default) {
        var fields = _helper.SqlFieldsList.Where(field => field.sqlName != _helper.IdCol).ToList();
        var entityList = entities.ToList();
        var cmdBuilder = new StringBuilder(
                $"""
                INSERT INTO {TName}
                ({fields.Select(f => f.sqlName).ConcatenateWith(", ")})
                VALUES

                """
                );
        await using var cmd = _conn.CreateCommand();
        foreach ((var i, var fso) in entityList.Index()) {
            var offset = i * fields.Count;
            cmdBuilder.AppendFormat("""
                ( ${0} ),
                """,
                    fields
                    .Select((_, index) => index + offset + 1)
                    .Select(ind => $"${ind}")
                    .ConcatenateWith(", "));
            EntityHelper<TInner, TEntity, TId>.FillParameters(cmd, fso, fields);
        }
        cmdBuilder.Remove(cmdBuilder.Length - 2, 2);
        cmdBuilder.AppendLine($"RETURNING {_helper.IdCol};");
        cmd.CommandText = cmdBuilder.ToString();
        await using var _ = await _conn.OpenAsyncDisposable(token);
        var reader = await cmd.ExecuteReaderAsync(token);
        int j = 0; // i is used in the foreach loop
        while (await reader.ReadAsync(token)) {
            var id = await reader.GetFieldValueAsync<TId>(0);
            entityList[j] = _helper.CloneWithId(entityList[j], id);
            j++;
        }
        await using var _disposable = await _conn.OpenAsyncDisposable(token);
        return new Ok<IEnumerable<TEntity>, DbError>(entityList.Select(e => e.Into()));
    }
    public Task<Result<IEnumerable<TEntity>, DbError>> CreateRangeAsync(IEnumerable<TEntity> entities, CancellationToken token = default) => CreateRangeAsync(entities.Select(e => (TInner)TInner.From(e)), token);

    public async Task<Result<Unit, DbError>> UpdateAsync(TEntity entity, CancellationToken token = default) {

        await using var _disp = await _conn.OpenAsyncDisposable(token);
        await using var transaction = await _conn.BeginTransactionAsync(token);
        await using var cmd = _conn.CreateCommand($"""
                UPDATE {TName} SET
                {_helper.SqlFieldsList.Select((field, ind) => $"{field.sqlName}={ind + 1}")}
                WHERE 
                RETURNING {_helper.IdCol};
                """);
        _helper.FillParameters(cmd, (TInner)TInner.From(entity));
        var count = await cmd.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
        return DbHelper.EnsureSingle(count);
    }

    public async Task<Option<TEntity>> GetByIdAsync(TId id, CancellationToken token = default)
        =>
            (await Get($"{TName}.{_helper.IdCol} = $1", "LIMIT 1",
                Some<Action<NpgsqlCommand>>(cmd => cmd.Parameters.Add(new NpgsqlParameter<TId> { Value = id })), token))
            .FirstOrDefault();

    public async Task<Result<int, DbError>> DeleteRangeAsyncWithOpenConn(IEnumerable<TId> ids, CancellationToken token = default) {
        var cmdBuilder = new StringBuilder();
        cmdBuilder.AppendLine($"DELETE FROM {TName}");
        cmdBuilder.Append($"WHERE {TName}.{_helper.IdCol} IN (");
        List<NpgsqlParameter> parameters = [];
        foreach ((var i, var id) in ids.Index()) {
            cmdBuilder.Append($"${i + 1}, ");
            parameters.Add(new NpgsqlParameter<TId> { Value = id });

        }

        var len = cmdBuilder.Length;
        cmdBuilder.Remove(len - 2, 2);
        cmdBuilder.Append(");");
        await using var cmd = _conn.CreateCommand(cmdBuilder.ToString());
        cmd.Parameters.AddRange(parameters.ToArray());
        int result = await cmd.ExecuteNonQueryAsync(token);
        return new Ok<int, DbError>(result);
    }
    public async Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<TId> ids, CancellationToken token = default) {
        await using var _disp = await _conn.OpenAsyncDisposable(token);
        var result = await DeleteRangeAsyncWithOpenConn(ids, token);
        return result;
    }

    public async Task<Result<Unit, DbError>> DeleteAsync(TId id, CancellationToken token = default) {
        await using var _disp = await _conn.OpenAsyncDisposable(token);
        await using var transaction = await _conn.BeginTransactionAsync(token);
        var deleteResult = await DeleteRangeAsyncWithOpenConn([id], token);
        await transaction.CommitAsync(token);
        return deleteResult.SelectMany(DbHelper.EnsureSingle);
    }
}
