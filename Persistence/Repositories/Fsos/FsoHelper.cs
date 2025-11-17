using System;
using System.Collections;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Persistance.Data;
using ZipZap.Persistance.Extensions;

namespace ZipZap.Persistance.Repositories;

internal class FsoHelper : EntityHelper<FsoInner, Fso, Guid> {
    public override string IdCol => GetColumnName(nameof(FsoInner.Id));

    public override FsoInner CloneWithId(FsoInner entity, Guid id) {
        var copy = entity.Copy();
        copy.Id = id;
        return copy;
    }

    public override async Task<Fso> Parse(NpgsqlDataReader reader, CancellationToken token = default) {
        var inner = new FsoInner() {
            Id = await reader.GetFieldValueAsync<Guid>($"{TableName}_{GetColumnName(nameof(FsoInner.Id))}", token),
            FsoName = await reader.GetFieldValueAsync<string>($"{TableName}_{GetColumnName(nameof(FsoInner.FsoName))}", token),
            VirtualLocationId = await reader.GetFieldValueAsync<Guid?>($"{TableName}_{GetColumnName(nameof(FsoInner.VirtualLocationId))}", token),
            Permissions = await reader.GetFieldValueAsync<BitArray>($"{TableName}_{GetColumnName(nameof(FsoInner.Permissions))}", token),
            FsoOwner = await reader.GetFieldValueAsync<int>($"{TableName}_{GetColumnName(nameof(FsoInner.FsoOwner))}", token),
            FsoGroup = await reader.GetFieldValueAsync<int>($"{TableName}_{GetColumnName(nameof(FsoInner.FsoGroup))}", token),
            FsoType = await reader.GetFieldValueAsync<FsoType>($"{TableName}_{GetColumnName(nameof(FsoInner.FsoType))}", token),
            LinkRef = await reader.GetNullableFieldValueAsync<string>($"{TableName}_{GetColumnName(nameof(FsoInner.LinkRef))}", token),
        };
        return inner.Into();
    }
}
