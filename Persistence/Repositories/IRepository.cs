// IRepository.cs - Part of the ZipZap project for storing files online
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.LangExt.Helpers;
using ZipZap.Persistence.Models;

namespace ZipZap.Persistence.Repositories;

public interface IRepository<TEntity, in TId>
where TEntity : IEntity<TId>
where TId : IEquatable<TId> {

    public Task<IEnumerable<TEntity>> GetAll(CancellationToken token = default);
    public Task<Result<TEntity, DbError>> CreateAsync(TEntity createEntity, CancellationToken token = default);
    public Task<Result<IEnumerable<TEntity>, DbError>> CreateRangeAsync(IEnumerable<TEntity> entities, CancellationToken token = default);
    public Task<Result<Unit, DbError>> DeleteAsync(TEntity entity, CancellationToken token = default);
    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken token = default);
    public Task<Result<Unit, DbError>> UpdateAsync(TEntity entity, CancellationToken token = default);
    public Task<TEntity?> GetByIdAsync(TId id, CancellationToken token = default);
    public Task<Result<Unit, DbError>> DeleteAsync(TId id, CancellationToken token = default);
    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<TId> ids, CancellationToken token = default);
}
