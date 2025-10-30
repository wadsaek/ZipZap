using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.Persistance.Data;
using ZipZap.Persistance.Extensions;
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
        var fsos = await _basic.Get(conn => {
            var cmd = conn.CreateCommand($"{GetRootQuery} LIMIT 1");

            cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = id.Value });
            return cmd;
        }, token);
        var directory = fsos.FirstOrDefault();
        Assert(directory is Directory or null);
        return directory as Directory;
    }
    public async Task<IEnumerable<Directory>> GetFullPathTree(FsoId id, CancellationToken token = default) {
        var fsos = await _basic.Get(conn => {
            var cmd = conn.CreateCommand(GetRootQuery);

            cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = id.Value });
            return cmd;
        }, token);
        return fsos.Assert(fso => fso is Directory).Cast<Directory>();
    }

    public async Task<Option<Fso>> GetByPath(MaybeEntity<Directory, FsoId> root, IEnumerable<string> paths, CancellationToken token = default) => (await _basic.Get(
                (conn => {
                    var cmd = conn.CreateCommand();
                    var builder = new StringBuilder(
                        """
                        WITH recursive paths(paths_level, paths_path_fragment) as (
                        VALUES
                        (0,'/'),
                        """);

                    cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = root.Id.Value });
                    // Parameter 1 is reserved for the query
                    int i = 0;
                    foreach (var (index, path) in paths.Index()) {
                        var level = index + 1;
                        var paramIndex = index + 2;
                        builder.Append($"({index + 1},${index + 2}),");
                        cmd.Parameters.Add(new NpgsqlParameter<string> { Value = path });
                        i = level;
                    }
                    builder.RemoveLastCharacters();
                    builder.AppendFormat(
                """
                        ),
                        ctename AS (
                            SELECT
                            {0},
                            0 AS level
                            FROM {1}
                            WHERE {1}.{2} = $1
                            UNION ALL
                            SELECT {0},
                            ctename.level + 1
                            FROM {1}
                            JOIN ctename ON {1}.{4}  = {1}_{2}
                            join paths on {1}.{5} = paths_path_fragment and paths_level = level + 1
                            )
                        SELECT {3} FROM ctename WHERE level = {6}
                        LIMIT 1;
                        """,
                    _fsoHelper.SqlFieldsInOrder,
                    TName,
                    IdCol,
                    _fsoHelper.SqlFields.Select(f => $"{TName}_{f}").ConcatenateWith(", "),
                    _fsoHelper.GetColumnName(nameof(FsoInner.VirtualLocationId)),
                    _fsoHelper.GetColumnName(nameof(FsoInner.FsoName)),
                    i
                    );
                    cmd.CommandText = builder.ToString();
                    return cmd;
                }), token
                )).FirstOrDefault();


    public Task<IEnumerable<Fso>> GetAllByDirectory(MaybeEntity<Directory, FsoId> location, CancellationToken token = default)
        => _basic.Get(
                $"{TName}.{_fsoHelper.GetColumnName(nameof(FsoInner.VirtualLocationId))} = $1",
                None<string>(),
                new Action<NpgsqlCommand>(
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
        => _basic.GetAll(token);

    public async Task<Option<Fso>> GetByDirectoryAndName(MaybeEntity<Directory, FsoId> location, string name, CancellationToken token = default)
        => (await _basic.Get(
                    $"""
                    {TName}.{_fsoHelper.GetColumnName(nameof(FsoInner.VirtualLocationId))} = $1
                    AND
                    {TName}.{_fsoHelper.GetColumnName(nameof(FsoInner.FsoName))} = $2
                    """,
                    "LIMIT 1",
                    new Action<NpgsqlCommand>(cmd => {
                        cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = location.Id.Value });
                        cmd.Parameters.Add(new NpgsqlParameter<string> { Value = name });
                    }),
                    token)).FirstOrDefault();

}
