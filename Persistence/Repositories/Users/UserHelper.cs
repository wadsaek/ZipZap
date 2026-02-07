using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Persistence.Data;

namespace ZipZap.Persistence.Repositories;

internal class UserHelper : EntityHelper<UserInner, User, Guid> {
    public override string IdCol => GetColumnName(nameof(UserInner.Id));

    public override UserInner CloneWithId(UserInner entity, Guid id) {
        return new(entity) { Id = id };
    }

    public override async Task<User> Parse(NpgsqlDataReader reader, CancellationToken token = default) {
        var id = await reader.GetFieldValueAsync<Guid>($"{TableName}_{IdCol}", token);
        var username = await reader.GetFieldValueAsync<string>($"{TableName}_{GetColumnName(nameof(UserInner.Username))}", token);
        var passwordHash = await reader.GetFieldValueAsync<byte[]>($"{TableName}_{GetColumnName(nameof(UserInner.PasswordHash))}", token);
        var email = await reader.GetFieldValueAsync<string>($"{TableName}_{GetColumnName(nameof(UserInner.Email))}", token);
        var role = await reader.GetFieldValueAsync<UserRole>($"{TableName}_{GetColumnName(nameof(UserInner.Role))}", token);
        var root = await reader.GetFieldValueAsync<Guid>($"{TableName}_{GetColumnName(nameof(UserInner.Root))}", token);
        var user = new UserInner(
            id, username,
            passwordHash,
            email,
            root,
            role
        ).Into();


        return user;
    }
}
