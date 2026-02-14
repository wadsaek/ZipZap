using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Persistence.Data;

namespace ZipZap.Persistence.Repositories;

internal class FsoAccessHelper : EntityHelper<FsoAccessInner, FsoAccess, Guid> {
    public override string IdCol => GetColumnName(nameof(FsoAccessInner.Id));

    public override FsoAccessInner CloneWithId(FsoAccessInner entity, Guid id) {
        return new(entity) { Id = id };
    }

    public override async Task<FsoAccess> Parse(NpgsqlDataReader reader, CancellationToken token = default) {
        var inner = new FsoAccessInner(
             await reader.GetFieldValueAsync<Guid>($"{TableName}_{GetColumnName(nameof(FsoAccessInner.Id))}", token),
             await reader.GetFieldValueAsync<Guid>($"{TableName}_{GetColumnName(nameof(FsoAccessInner.FsoId))}", token),
             await reader.GetFieldValueAsync<Guid>($"{TableName}_{GetColumnName(nameof(FsoAccessInner.UserId))}", token)
        );
        return inner.Into();
    }
}
