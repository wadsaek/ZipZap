using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Persistence.Data;
using ZipZap.Persistence.Extensions;

namespace ZipZap.Persistence.Repositories;

internal class FsoHelper : EntityHelper<FsoInner, Fso, Guid> {
    public override string IdCol => GetColumnName(nameof(FsoInner.Id));

    public override FsoInner CloneWithId(FsoInner entity, Guid id) {
        return new(entity) { Id = id };
    }

    public override async Task<Fso> Parse(NpgsqlDataReader reader, CancellationToken token = default) {
        var inner = new FsoInner(
             await reader.GetFieldValueAsync<Guid>($"{TableName}_{GetColumnName(nameof(FsoInner.Id))}", token),
             await reader.GetFieldValueAsync<string>($"{TableName}_{GetColumnName(nameof(FsoInner.FsoName))}", token),
             await reader.GetFieldValueAsync<Guid?>($"{TableName}_{GetColumnName(nameof(FsoInner.VirtualLocationId))}", token),
             await reader.GetFieldValueAsync<short>($"{TableName}_{GetColumnName(nameof(FsoInner.Permissions))}", token),
             await reader.GetFieldValueAsync<int>($"{TableName}_{GetColumnName(nameof(FsoInner.FsoOwner))}", token),
             await reader.GetFieldValueAsync<int>($"{TableName}_{GetColumnName(nameof(FsoInner.FsoGroup))}", token),
             await reader.GetFieldValueAsync<FsoType>($"{TableName}_{GetColumnName(nameof(FsoInner.FsoType))}", token),
             await reader.GetNullableFieldValueAsync<string>($"{TableName}_{GetColumnName(nameof(FsoInner.LinkRef))}", token)
        );
        return inner.Into();
    }
}
