using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.FileService.Models;

namespace ZipZap.FileService.Repositories;

public interface IRepository<TEntity, TKey>
where TEntity : IEntity<TKey>
where TKey : IEquatable<TKey> {

    public Task<IEnumerable<TEntity>> GetAll(CancellationToken token = default);
    public Task<Result<TEntity, DbError>> CreateAsync(TEntity createEntity, CancellationToken token = default);
    public Task<Result<IEnumerable<TEntity>, DbError>> CreateRangeAsync(IEnumerable<TEntity> entities, CancellationToken token = default);
    public Task<Result<Unit, DbError>> DeleteAsync(TEntity entity, CancellationToken token = default);
    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken token = default);
    public Task<Result<Unit, DbError>> UpdateAsync(TEntity entity, CancellationToken token = default);
    public Task<Option<TEntity>> GetByIdAsync(TKey id, CancellationToken token = default);
    public Task<Result<Unit, DbError>> DeleteAsync(TKey id, CancellationToken token = default);
    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken token = default);
}
