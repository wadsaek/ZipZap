using System;
using System.Collections;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.FileService.Data;

using static ZipZap.Classes.Helpers.Constructors;
namespace ZipZap.FileService.Repositories;

internal class UserHelper : EntityHelper<UserInner, User, Guid> {
    public override string IdCol => GetColumnName(nameof(UserInner.Id));

    public override UserInner CloneWithId(UserInner entity, Guid id) {
        var copy = entity.Copy();
        copy.Id = id;
        return copy;
    }

    public override async Task<User> Parse(NpgsqlDataReader reader, CancellationToken token = default) {
        var Id = await reader.GetFieldValueAsync<Guid>($"{TableName}_{IdCol}", token);
        var Username = await reader.GetFieldValueAsync<string>($"{TableName}_{GetColumnName(nameof(UserInner.Username))}", token);
        var PasswordHash = await reader.GetFieldValueAsync<byte[]>($"{TableName}_{GetColumnName(nameof(UserInner.PasswordHash))}", token);
        var Email = await reader.GetFieldValueAsync<string>($"{TableName}_{GetColumnName(nameof(UserInner.Email))}", token);
        var Root = await reader.GetFieldValueAsync<Guid>($"{TableName}_{GetColumnName(nameof(UserInner.Root))}", token);
        var user = new UserInner() {
            Root = Root,
            Email = Email,
            PasswordHash = PasswordHash,
            Username = Username,
            Id = Id
        }.Into();


        return user;
    }
}
