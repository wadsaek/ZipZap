using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Persistance.Data;
using ZipZap.Persistance.Extensions;
using ZipZap.Persistance.Models;

namespace ZipZap.Persistance.Repositories;

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
    private string UTName => _userHelper.TableName;
    private string FTName => _fsoHelper.TableName;

    public Task<IEnumerable<User>> GetAll(CancellationToken token = default)
        => _basic.GetAll(token);

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
}
