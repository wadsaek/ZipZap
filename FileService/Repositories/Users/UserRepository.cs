using System;
using System.Collections.Generic;
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

internal class UserReposirory : IUserRepository {
    private readonly NpgsqlConnection _conn;
    private readonly EntityHelper<UserInner, User, Guid> _userHelper;
    private readonly EntityHelper<FsoInner, Fso, Guid> _fsoHelper;
    private readonly BasicRepository<User, UserInner, Guid> _basic;

    public UserReposirory(NpgsqlConnection conn, EntityHelper<UserInner, User, Guid> userHelper, EntityHelper<FsoInner, Fso, Guid> fsoHelper, BasicRepository<User, UserInner, Guid> basic) {
        _conn = conn;
        _userHelper = userHelper;
        _fsoHelper = fsoHelper;
        _basic = basic;
    }
    private string UTName => _userHelper.TableName;
    private string FTName => _fsoHelper.TableName;
    public Task<IEnumerable<User>> GetAll(CancellationToken token = default) => _basic.GetAll(token);

    public async Task<Option<User>> GetByIdAsync(UserId id, CancellationToken token = default) {
        await using var _disposable = await _conn.OpenAsyncDisposable(token);
        var cmdBuilder = new StringBuilder($"""
                    SELECT {_userHelper.SqlFieldsInOrder}, {_fsoHelper.SqlFieldsInOrder} FROM {_userHelper.TableName}
                    LEFT JOIN {FTName} ON
                    {_userHelper.TableName}.{_userHelper.GetColumnName(nameof(UserInner.Root))} =
                    {_fsoHelper.TableName}.{_fsoHelper.GetColumnName(nameof(FsoInner.Id))}
                    WHERE {UTName}.{_userHelper.GetColumnName(nameof(UserInner.Id))} = $1
                    LIMIT 1
                    """);
        var cmd = _conn.CreateCommand(cmdBuilder.ToString());
        cmd.Parameters.Add(new() { Value = id.Value });
        await using var reader = await cmd.ExecuteReaderAsync(token);
        User? user = null;
        while (await reader.ReadAsync(token)) {
            if (user is not null)
                throw new System.IO.InvalidDataException();
            user = await _userHelper.Parse(reader, token);
            user = user with { Root = (await _fsoHelper.Parse(reader, token) as Directory)! };
        }
        return user;

    }

    public Task<Result<Unit, DbError>> DeleteAsync(User entity, CancellationToken token = default) {
        throw new NotImplementedException();
    }

    public Task<Result<Unit, DbError>> DeleteAsync(UserId id, CancellationToken token = default) {
        throw new NotImplementedException();
    }

    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<User> entities, CancellationToken token = default) {
        throw new NotImplementedException();
    }

    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<UserId> ids, CancellationToken token = default) {
        throw new NotImplementedException();
    }

    public Task<Result<Unit, DbError>> UpdateAsync(User entity, CancellationToken token = default) {
        throw new NotImplementedException();
    }

    public async Task<Result<User, DbError>> CreateAsync(User createEntity, CancellationToken token = default) {
        await using var cmd = _conn.CreateCommand($"""
                INSERT INTO fsos
                ({_userHelper.SqlFields.ConcatenateWith(", ")})
                VALUES (
                    $1, $2, $3, $4,
                    $5, $6, $7, $8
                    )
                RETURNING id;
                """);
        FillUserParameters(cmd, createEntity);
        await using var _disposable = await _conn.OpenAsyncDisposable(token);
        var id = await cmd.ExecuteScalarAsync(token) as Guid?;
        if (id is null) return new Err<User, DbError>(new DbError());
        return new Ok<User, DbError>(createEntity with { Id = new(id.Value) });
    }

    private void FillUserParameters(NpgsqlCommand cmd, User createEntity) {
        throw new NotImplementedException();
    }

    public Task<Result<IEnumerable<User>, DbError>> CreateRangeAsync(IEnumerable<User> entities, CancellationToken token = default) {
        throw new NotImplementedException();
    }
}
