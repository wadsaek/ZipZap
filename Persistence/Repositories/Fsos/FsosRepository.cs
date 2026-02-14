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
using ZipZap.LangExt.Extensions;
using ZipZap.LangExt.Helpers;
using ZipZap.Persistence.Data;
using ZipZap.Persistence.Extensions;
using ZipZap.Persistence.Models;

using static ZipZap.LangExt.Helpers.Assertions;

using Directory = ZipZap.Classes.Directory;

namespace ZipZap.Persistence.Repositories;

internal class FsosRepository : IFsosRepository {
    private readonly EntityHelper<FsoInner, Fso, Guid> _fsoHelper;
    private readonly ExceptionConverter<DbError> _converter;
    private readonly IBasicRepository<Fso, FsoInner, Guid> _basic;

    private string TableName => _fsoHelper.TableName;
    private string IdCol => _fsoHelper.GetColumnName(nameof(FsoInner.Id));
    public FsosRepository(
            EntityHelper<FsoInner, Fso, Guid> fsoHelper,
            ExceptionConverter<DbError> converter, IBasicRepository<Fso, FsoInner, Guid> basic) {
        _fsoHelper = fsoHelper;
        _converter = converter;
        _basic = basic;
    }

    private string GetRootQuery => $"""
        WITH RECURSIVE ctename AS (
                SELECT
                {_fsoHelper.SqlFieldsInOrder},
                0 AS level
                FROM {TableName}
                WHERE {TableName}.{IdCol} = $1
                UNION ALL
                SELECT
                {_fsoHelper.SqlFieldsInOrder},
                ctename.level + 1
                FROM {TableName}
                JOIN ctename ON {TableName}.{IdCol} =
                {TableName}_{_fsoHelper.GetColumnName(nameof(FsoInner.VirtualLocationId))}
                )
        SELECT
        {_fsoHelper.SqlFields.Select(f => $"{TableName}_{f}").ConcatenateWith(", ")}
        FROM ctename
        """;
    public async Task<Directory?> GetRootDirectory(FsoId id, CancellationToken token = default) {
        var fsos = await _basic.Get(conn => {
            var cmd = conn.CreateCommand($"{GetRootQuery} order by level desc LIMIT 1;");

            cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = id.Value });
            return cmd;
        }, token);
        var directory = fsos.FirstOrDefault();
        Assert(directory is Directory or null);
        return directory as Directory;
    }
    public async Task<IEnumerable<Fso>> GetFullPathTree(FsoId id, CancellationToken token = default) {
        var fsos = await _basic.Get(conn => {
            var cmd = conn.CreateCommand($"{GetRootQuery} order by level desc;");

            cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = id.Value });
            return cmd;
        }, token);
        return fsos;
    }

    ///<returns>The most deeply nested fso that is a parent of <paramref name="fsoId"/> that is shared with the <paramref name="userId"/> user</returns>
    public async Task<Fso?> GetDeepestSharedFso(FsoId fsoId, UserId userId, CancellationToken cancellationToken) {
        var fsos = await _basic.Get(conn => {
            var cmd = conn.CreateCommand($"{GetRootQuery} JOIN fso_access ON fso_access.fso_id = {TableName}_{_fsoHelper.IdCol} WHERE fso_access.user_id = $2 ORDER BY level DESC LIMIT 1;");
            cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = fsoId.Value });
            cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = userId.Value });

            return cmd;
        }, cancellationToken);

        var deepestMatch = fsos.FirstOrDefault();
        return deepestMatch;
    }

    public async Task<Fso?> GetByPath(MaybeEntity<Directory, FsoId> root, string path, CancellationToken token = default) =>
        (await _basic.Get(conn => {
            var cmd = conn.CreateCommand();
            var builder = new StringBuilder(
                """
                WITH recursive paths(paths_level, paths_path_fragment) as (
                VALUES
                (0,'/'),
                """);

            cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = root.Id.Value });
            // Parameter 1 is reserved for the query
            var i = 0;
            foreach (var (index, pathPart) in path.SplitPath().Index()) {
                var level = index + 1;
                var paramIndex = index + 2;
                builder.Append($"({level},${paramIndex}),");
                cmd.Parameters.Add(new NpgsqlParameter<string> { Value = pathPart });
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
            TableName,
            IdCol,
            _fsoHelper.SqlFields.Select(f => $"{TableName}_{f}").ConcatenateWith(", "),
            _fsoHelper.GetColumnName(nameof(FsoInner.VirtualLocationId)),
            _fsoHelper.GetColumnName(nameof(FsoInner.FsoName)),
            i
            );
            cmd.CommandText = builder.ToString();
            return cmd;
        }, token
        )).FirstOrDefault();


    public Task<IEnumerable<Fso>> GetAllByDirectory(MaybeEntity<Directory, FsoId> location, CancellationToken token = default)
        => _basic.Get(
                $"{TableName}.{_fsoHelper.GetColumnName(nameof(FsoInner.VirtualLocationId))} = $1",
                null,
                cmd => cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = location.Id.Value }), token);

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

    public Task<Fso?> GetByIdAsync(FsoId id, CancellationToken token = default)
        => _basic.GetByIdAsync(id.Value, token);

    public Task<Result<Unit, DbError>> DeleteAsync(FsoId id, CancellationToken token = default)
        => _basic.DeleteAsync(id.Value, token);

    public Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<FsoId> ids, CancellationToken token = default)
        => _basic.DeleteRangeAsyncWithOpenConn(ids.Select(id => id.Value), token);

    public Task<IEnumerable<Fso>> GetAll(CancellationToken token = default)
        => _basic.GetAll(token);

    public async Task<Fso?> GetByDirectoryAndName(MaybeEntity<Directory, FsoId> location, string name, CancellationToken token = default)
        => (await _basic.Get(
                    $"""
                    {TableName}.{_fsoHelper.GetColumnName(nameof(FsoInner.VirtualLocationId))} = $1
                    AND
                    {TableName}.{_fsoHelper.GetColumnName(nameof(FsoInner.FsoName))} = $2
                    """,
                    "LIMIT 1",
                    cmd => {
                        cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = location.Id.Value });
                        cmd.Parameters.Add(new NpgsqlParameter<string> { Value = name });
                    },
                    token)).FirstOrDefault();

}
