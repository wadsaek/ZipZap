using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
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
