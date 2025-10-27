using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Persistance.Models;

namespace ZipZap.Persistance.Repositories;

public interface IRepository<TEntity, TId>
where TEntity : IEntity<TId>
where TId : IEquatable<TId> {

    public Task<IEnumerable<TEntity>> GetAll(CancellationToken token = default);
    public Task<Result<TEntity, DbError>> CreateAsync(TEntity createEntity, CancellationToken token = default);
    public Task<Result<IEnumerable<TEntity>, DbError>> CreateRangeAsync(IEnumerable<TEntity> entities, CancellationToken token = default);
    public Task<Result<Unit, DbError>> DeleteAsync(TEntity entity, CancellationToken token = default);
    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken token = default);
    public Task<Result<Unit, DbError>> UpdateAsync(TEntity entity, CancellationToken token = default);
    public Task<Option<TEntity>> GetByIdAsync(TId id, CancellationToken token = default);
    public Task<Result<Unit, DbError>> DeleteAsync(TId id, CancellationToken token = default);
    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<TId> ids, CancellationToken token = default);
}
