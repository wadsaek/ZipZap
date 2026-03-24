// FsosRepository.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
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
using File = ZipZap.Classes.File;

namespace ZipZap.Persistence.Repositories;

internal class FsosRepository : IFsosRepository {
    private readonly EntityHelper<FsoInner, Fso, Guid> _fsoHelper;
    private readonly ExceptionConverter<DbError> _converter;
    private readonly IBasicRepository<Fso, FsoInner, Guid> _basic;
    private readonly NpgsqlConnection _conn;

    private string TableName => _fsoHelper.TableName;
    private string IdCol => _fsoHelper.GetColumnName(nameof(FsoInner.Id));
    public FsosRepository(
            NpgsqlConnection conn,
            EntityHelper<FsoInner, Fso, Guid> fsoHelper,
            ExceptionConverter<DbError> converter,
            IBasicRepository<Fso, FsoInner, Guid> basic) {
        _conn = conn;
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

    public async Task<Result<Unit, DbError>> DeleteAsync(FsoId fso, DeleteOptions options, CancellationToken cancellationToken) {
        if (options is DeleteOptions.All)
            return await DeleteAsync(fso, cancellationToken);

        await using var disposable = await _conn.OpenAsyncDisposable(cancellationToken);
        await using var transaction = await _conn.BeginTransactionAsync(cancellationToken);
        var cmdBuilder = new StringBuilder($"""
            DELETE FROM {TableName}
            WHERE {TableName}.{_fsoHelper.IdCol} = $1
            """);
        var (condition, parameter2) = options switch {
            DeleteOptions.AllExceptDirectories
                => ($" AND {TableName}.{_fsoHelper.GetColumnName(nameof(FsoInner.FsoType))} <> $2",
                    new NpgsqlParameter<FsoType> { Value = FsoType.Directory }),

            DeleteOptions.OnlyEmptyDirectories
                => ($"""
                         AND {TableName}.{_fsoHelper.GetColumnName(nameof(FsoInner.FsoType))} = $2
                        AND NOT EXISTS(
                            SELECT FROM {TableName}
                            WHERE {TableName}.{_fsoHelper.GetColumnName(nameof(FsoInner.VirtualLocationId))} = $1
                        )
                    """,
                    new NpgsqlParameter<FsoType> { Value = FsoType.Directory }),
            _ => (string.Empty, null)
        };
        cmdBuilder.Append(condition);
        cmdBuilder.Append(';');
        var cmd = _conn.CreateCommand(cmdBuilder.ToString());
        cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = fso.Value });
        if (parameter2 is not null)
            cmd.Parameters.Add(parameter2);
        var deleted = await cmd.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return DbHelper.EnsureSingle(deleted);
    }

    public async Task<IEnumerable<File>> GetAllChildFilesAsync(FsoId parent, CancellationToken cancellationToken) {
        var fsos = await _basic.Get(conn => {
            var command = conn.CreateCommand($"""
                        WITH RECURSIVE ctename AS (
                            SELECT {_fsoHelper.SqlFieldsInOrder}
                            FROM   {TableName}
                            WHERE  {TableName}.{IdCol} = $1
                            UNION ALL
                            SELECT
                            {_fsoHelper.SqlFieldsInOrder}
                            FROM {TableName}
                            JOIN ctename ON {TableName}_{IdCol} =
                            {TableName}.{_fsoHelper.GetColumnName(nameof(FsoInner.VirtualLocationId))}
                            )
                        SELECT
                        {_fsoHelper.SqlFields.Select(f => $"{TableName}_{f}").ConcatenateWith(", ")}
                        FROM ctename
                        WHERE {TableName}_{_fsoHelper.GetColumnName(nameof(FsoInner.FsoType))} = $2
                        """);
            command.Parameters.Add(new NpgsqlParameter<Guid> { Value = parent.Value });
            command.Parameters.Add(new NpgsqlParameter<FsoType> { Value = FsoType.RegularFile });
            return command;
        }, cancellationToken);
        return fsos.SelectMany<Fso, File>(fso => fso is File file
                ? [file]
                : throw new InvalidDataException($"Querying for files in {nameof(GetAllChildFilesAsync)} returned an fso of different format")
        );
    }
}
