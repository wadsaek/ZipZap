// FsoAccessHelper.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

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
