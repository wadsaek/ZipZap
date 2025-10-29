using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.Persistance.Data;
using ZipZap.Persistance.Models;

using static ZipZap.Classes.Helpers.Assertions;
using static ZipZap.Classes.Helpers.Constructors;

using Directory = ZipZap.Classes.Directory;

namespace ZipZap.Persistance.Repositories;

internal class FsosRepository : IFsosRepository {
    private readonly EntityHelper<FsoInner, Fso, Guid> _fsoHelper;
    private readonly ExceptionConverter<DbError> _converter;
    private readonly IBasicRepository<Fso, FsoInner, Guid> _basic;

    private string TName => _fsoHelper.TableName;
    private string IdCol => _fsoHelper.GetColumnName(nameof(FsoInner.Id));
    public FsosRepository(EntityHelper<FsoInner, Fso, Guid> fsoHelper, ExceptionConverter<DbError> converter, IBasicRepository<Fso, FsoInner, Guid> basic) {
        _fsoHelper = fsoHelper;
        _converter = converter;
        _basic = basic;
    }

    private string GetRootQuery => $"""
        WITH RECURSIVE ctename AS (
                SELECT
                {_fsoHelper.SqlFieldsInOrder},
                0 AS level
                FROM {TName}
                WHERE {TName}.{IdCol} = $1
                UNION ALL
                SELECT
                {_fsoHelper.SqlFieldsInOrder},
                ctename.level + 1
                FROM {TName}
                JOIN ctename ON {TName}.{IdCol} =
                {TName}_{_fsoHelper.GetColumnName(nameof(FsoInner.VirtualLocationId))}
                )
        SELECT
        {_fsoHelper.SqlFields.Select(f => $"{TName}_{f}").ConcatenateWith(", ")}
        FROM ctename order by level desc;
        """;
    public async Task<Option<Directory>> GetRootDirectory(FsoId id, CancellationToken token = default) {
        var fsos = await _basic.Get(None<string>(), None<string>(), Some<Action<NpgsqlCommand>>(cmd => {
            cmd.CommandText = $"{GetRootQuery} LIMIT 1";
            cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = id.Value });
        }), token);
        var directory = fsos.FirstOrDefault();
        Assert(directory is Directory or null);
        return directory as Directory;
    }
    public async Task<IEnumerable<Directory>> GetFullPathTree(FsoId id, CancellationToken token = default) {
        var fsos = await _basic.Get(None<string>(), None<string>(), Some<Action<NpgsqlCommand>>(cmd => {
            cmd.CommandText = $"{GetRootQuery}";
            cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = id.Value });
        }), token);
        return fsos.Assert(fso => fso is Directory).Cast<Directory>();
    }


    public Task<IEnumerable<Fso>> GetAllByDirectory(Directory location, CancellationToken token = default)
        => _basic.Get(
                $"{TName}.{_fsoHelper.GetColumnName(nameof(FsoInner.VirtualLocationId))} = $1",
                None<string>(),
                Some<Action<NpgsqlCommand>>(
                    cmd => cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = location.Id.Value })
                ), token);

    public Task<Result<Fso, DbError>> CreateAsync(Fso createEntity, CancellationToken token = default)
        => _basic.CreateAsync(createEntity, token);

    public Task<Result<IEnumerable<Fso>, DbError>> CreateRangeAsync(IEnumerable<Fso> entities, CancellationToken token = default)
        => _basic.CreateRangeAsync(entities, token);

    public Task<Result<Unit, DbError>> DeleteAsync(Fso entity, CancellationToken token = default)
        => DeleteAsync(entity.Id, token);

    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<Fso> entities, CancellationToken token = default)
        => DeleteRangeAsync(entities.Select(fso => fso.Id), token);

    public Task<Result<Unit, DbError>> UpdateAsync(Fso entity, CancellationToken token = default)
        => _basic.UpdateAsync(entity, token);

    public Task<Option<Fso>> GetByIdAsync(FsoId id, CancellationToken token = default)
        => _basic.GetByIdAsync(id.Value, token);

    public Task<Result<Unit, DbError>> DeleteAsync(FsoId id, CancellationToken token = default)
        => _basic.DeleteAsync(id.Value, token);

    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<FsoId> ids, CancellationToken token = default)
        => _basic.DeleteRangeAsyncWithOpenConn(ids.Select(id => id.Value), token);

    public Task<IEnumerable<Fso>> GetAll(CancellationToken token = default)
        => _basic.Get(None<string>(), None<string>(), None<Action<NpgsqlCommand>>(), token);
}
