using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Persistence.Extensions;
using ZipZap.Persistence.Data;
using ZipZap.Persistence.Models;

namespace ZipZap.Persistence.Repositories;

internal class UserReposirory : IUserRepository {
    private readonly NpgsqlConnection _conn;
    private readonly EntityHelper<UserInner, User, Guid> _userHelper;
    private readonly EntityHelper<FsoInner, Fso, Guid> _fsoHelper;
    private readonly IBasicRepository<User, UserInner, Guid> _basic;

    public UserReposirory(
            NpgsqlConnection conn,
            EntityHelper<UserInner, User, Guid> userHelper,
            EntityHelper<FsoInner, Fso, Guid> fsoHelper,
            IBasicRepository<User, UserInner, Guid> basic) {
        _conn = conn;
        _userHelper = userHelper;
        _fsoHelper = fsoHelper;
        _basic = basic;
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
        => DeleteAsync(entity.Id, token);

    public Task<Result<Unit, DbError>> DeleteAsync(UserId id, CancellationToken token = default)
        => _basic.DeleteAsync(id.Value, token);

    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<User> entities, CancellationToken token = default)
        => DeleteRangeAsync(entities.Select(e => e.Id), token);

    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<UserId> ids, CancellationToken token = default)
        => _basic.DeleteRangeAsync(ids.Select(id => id.Value), token);

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
