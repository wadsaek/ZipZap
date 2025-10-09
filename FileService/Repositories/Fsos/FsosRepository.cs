using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Npgsql;
using System.Linq;
using ZipZap.Classes;
using ZipZap.FileService.Models;
using ZipZap.Classes.Helpers;
using ZipZap.FileService.Extensions;
using System.Collections;
using System.Text;

using static ZipZap.Classes.Helpers.OptionExt;
using Directory = ZipZap.Classes.Directory;
using File = ZipZap.Classes.File;
using static ZipZap.FileService.Helpers.Assertions;

namespace ZipZap.FileService.Repositories;

public class FsosRepository : IFsosRepository {
    private readonly NpgsqlConnection _conn;
    private readonly ExceptionConverter<DbError> _converter;
    public FsosRepository(NpgsqlConnection conn, ExceptionConverter<DbError> converter) {
        _conn = conn;
        _converter = converter;
    }

    private async Task<IEnumerable<Fso>> Get(Option<string> condition, Option<string> postCondition, Option<Action<NpgsqlCommand>> commandCallback, CancellationToken token = default) {
        var cmdBuilder = new StringBuilder();
        cmdBuilder.AppendLine("""
                SELECT fsos.id, fsos.fso_name,
                fsos.virtual_location_id, fsos.permissions,
                fsos.fso_owner, fsos.fso_group,
                fsos.fso_type,
                fsos.link_ref, fsos.file_physical_path
                FROM fsos
                """);
        condition.Select(condition => cmdBuilder.AppendLine($"WHERE {condition}"));
        postCondition.Select(cmdBuilder.AppendLine);
        cmdBuilder.Append(';');

        var cmdText = cmdBuilder.ToString();
        await using var cmd = _conn.CreateCommand(cmdText);
        commandCallback.Select(callback => { callback(cmd); return new Unit(); });
        await using var _disposable = await _conn.OpenAsyncDisposable();
        await using var reader = await cmd.ExecuteReaderAsync(token);
        var fsos = new Dictionary<FsoId, Fso>();
        while (await reader.ReadAsync(token)) {
            var id = await reader.GetFieldValueAsync<Guid>(0);
            var fsoName = await reader.GetFieldValueAsync<string>(1);
            var virtualLocationId = await reader.GetFieldValueAsync<Guid?>(2);
            var permissions = await reader.GetFieldValueAsync<BitArray>(3);
            var fsoOwner = await reader.GetFieldValueAsync<string>(4);
            var fsoGroup = await reader.GetFieldValueAsync<string>(5);
            var fsoType = await reader.GetFieldValueAsync<FsoType>(6);
            var linkRef = await reader.GetNullableFieldValueAsync<string>(7);
            var filePhysicalPath = await reader.GetNullableFieldValueAsync<string>(8);
            Fso fso = fsoType switch {
                FsoType.RegularFile => new File(
                    id: new FsoId(id),
                    data: new FsData(
                        virtualLocation: virtualLocationId.ToOption().Select(id => new Directory() { Id = new(id) }),
                        fsoOwner,
                        fsoGroup
                    ),
                    name: fsoName,
                    dataPath: filePhysicalPath!,
                    permissions: FilePermissions.FromBitArray(permissions)
                ),
                FsoType.Directory => new Directory(
                    id: new FsoId(id),
                    data: new FsData(
                        virtualLocation: virtualLocationId.ToOption().Select(id => new Directory() { Id = new(id) }),

                        fsoOwner,
                        fsoGroup
                    ),
                    name: fsoName,
                    permissions: DirectoryPermissions.FromBitArray(permissions)
                ),
                FsoType.Symlink => new Symlink(
                    id: new FsoId(id),
                    data: new FsData(
                        virtualLocation: virtualLocationId.ToOption().Select(id => new Directory() { Id = new(id) }),
                        fsoOwner,
                        fsoGroup
                    ),
                    name: fsoName,
                    target: linkRef!
                ),
                _ => throw new InvalidEnumVariantException()
            };
            fsos.Add(fso.Id, fso);
        }
        foreach ((_, var fso) in fsos) {
            var data = fso.Data;
            if (data.VirtualLocation is Some<Directory>(Directory dir))
                data.VirtualLocation = fsos.GetValueOrDefault(dir.Id) as Directory;
        }

        return fsos.Values;
    }
    public async Task<IEnumerable<Fso>> GetAll(CancellationToken token = default)
        => await Get(None<string>(), None<string>(), None<Action<NpgsqlCommand>>(), token);


    public async Task<IEnumerable<Fso>> GetAllByDirectory(Directory location, CancellationToken token = default)
        => await Get(
                Some("fsos.virtual_location_id = $1"),
                None<string>(),
                Some<Action<NpgsqlCommand>>(
                    cmd => cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = location.Id.Id })
                    )
                );


    private void FillFsoParameters(NpgsqlCommand cmd, Fso createEntity) {
        cmd.Parameters.Add(new NpgsqlParameter<string> { Value = createEntity.Name });
        cmd.Parameters.Add(new NpgsqlParameter<Guid?> { Value = createEntity.Data.VirtualLocation.Select(dir => dir.Id.Id) });
        cmd.Parameters.Add(new NpgsqlParameter<BitArray?> {
            Value =
                    (createEntity as File)?.Permissions.ToBitArray()
                    ?? (createEntity as Directory)?.Permissions.ToBitArray()
        });
        cmd.Parameters.Add(new NpgsqlParameter<string> { Value = createEntity.Data.FsoOwner });
        cmd.Parameters.Add(new NpgsqlParameter<string> { Value = createEntity.Data.FsoGroup });
        cmd.Parameters.Add(new NpgsqlParameter<FsoType> {
            Value = createEntity switch {
                File => FsoType.RegularFile,
                Directory => FsoType.Directory,
                Symlink => FsoType.Symlink,
                _ => throw new InvalidEnumVariantException()
            }
        });
        cmd.Parameters.Add(new NpgsqlParameter<string?> { Value = (createEntity as Symlink)?.Target });
        cmd.Parameters.Add(new NpgsqlParameter<string?> { Value = (createEntity as File)?.PhysicalPath });
    }
    public async Task<Result<Unit, DbError>> CreateAsync(Fso createEntity, CancellationToken token = default) {
        var cmd = _conn.CreateCommand("""
                INSERT INTO fsos
                (fso_name, virtual_location_id,
                permssions, fso_owner, fso_group, fso_type,
                link_ref,file_physical_path )
                VALUES (
                    $1, $2, $3, $4,
                    $5, $6, $7, $8
                    )
                RETURNING id;
                """);
        FillFsoParameters(cmd, createEntity);
        await using var _disposable = await _conn.OpenAsyncDisposable();
        var id = await cmd.ExecuteScalarAsync(token) as Guid?;
        if (id is null) return new Err<Unit, DbError>(new DbError());
        createEntity.Id = new(id.Value);
        return new Ok<Unit, DbError>(new Unit());
    }

    public async Task<Result<Unit, DbError>> CreateRangeAsync(IEnumerable<Fso> entities, CancellationToken token = default) {
        var entityList = entities.ToList();
        var cmdBuilder = new StringBuilder(
                """
                INSERT INTO fsos
                (fso_name, virtual_location_id,
                permssions, fso_owner, fso_group, fso_type,
                link_ref,file_physical_path )
                VALUES

                """
                );
        var cmd = _conn.CreateCommand();
        foreach ((var i, var fso) in entityList.Index()) {
            var offset = i * 8;
            cmdBuilder.AppendFormat("""
                (
                    ${0}, ${1}, ${2}, ${3},
                    ${4}, ${5}, ${6}, ${7}
                ),
                """,
                    offset + 1, offset + 2, offset + 3, offset + 4,
                    offset + 5, offset + 6, offset + 7, offset + 8
                    );
            FillFsoParameters(cmd, fso);
        }
        cmdBuilder.AppendLine("RETURNING id;");
        cmd.CommandText = cmdBuilder.ToString();
        await using var _ = await _conn.OpenAsyncDisposable();
        var reader = await cmd.ExecuteReaderAsync(token);
        int j = 0; // i is used in the foreach loop
        while (await reader.ReadAsync()) {
            var id = await reader.GetFieldValueAsync<Guid>(0);
            entityList[j++].Id = new(id);

        }
        await using var _disposable = await _conn.OpenAsyncDisposable();
        return new Ok<Unit, DbError>(new Unit());
    }

    public async Task<Result<Unit, DbError>> DeleteAsync(Fso entity, CancellationToken token = default)
        => await DeleteAsync(entity.Id);

    public async Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<Fso> entities, CancellationToken token = default)
        => await DeleteRangeAsync(entities.Select(fso => fso.Id), token);

    public async Task<Result<Unit, DbError>> UpdateAsync(Fso entity, CancellationToken token = default) {

        await using var _disp = await _conn.OpenAsyncDisposable();
        await using var transaction = await _conn.BeginTransactionAsync();
        var cmd = _conn.CreateCommand("""
                UPDATE fsos
                fso_name=$1,
                virtual_location_id=$2,
                permssions=$3,
                fso_owner=$4,
                fso_group=$5,
                fso_type=$6,
                link_ref=$7,
                file_physical_path=$8
                WHERE 
                RETURNING id;
                """);
        FillFsoParameters(cmd, entity);
        var count = await cmd.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        return DbHelper.EnsureSingle(count);
    }

    public async Task<Option<Fso>> GetByIdAsync(FsoId id, CancellationToken token = default)
        => Some(
                await Get(Some("fsos.id = $1"), Some("LIMIT 1"),
                    Some<Action<NpgsqlCommand>>(cmd => cmd.Parameters.Add(new NpgsqlParameter<Guid> { Value = id.Id })))
                )
            .Select(a => a.FirstOrDefault())
            .SelectMany<Fso?, Fso>(fso => fso is null ? None<Fso>() : Some(fso));

    public async Task<Result<Unit, DbError>> DeleteAsync(FsoId id, CancellationToken token = default) {
        await using var _disp = await _conn.OpenAsyncDisposable();
        await using var transaction = await _conn.BeginTransactionAsync();
        var deleteResult = await DeleteRangeAsyncWithOpenConn([id], token);
        await transaction.CommitAsync();
        return deleteResult.SelectMany(DbHelper.EnsureSingle);
    }

    public async Task<Result<int, DbError>> DeleteRangeAsync(IEnumerable<FsoId> ids, CancellationToken token = default) {
        await using var _disp = await _conn.OpenAsyncDisposable();
        var result = await DeleteRangeAsyncWithOpenConn(ids, token);
        return result;
    }
    public async Task<Result<int, DbError>> DeleteRangeAsyncWithOpenConn(IEnumerable<FsoId> ids, CancellationToken token = default) {
        var cmdBuilder = new StringBuilder();
        cmdBuilder.AppendLine("DELETE FROM fsos");
        cmdBuilder.Append("WHERE fsos.id IN (");
        List<NpgsqlParameter> parameters = [];
        foreach ((var i, var fsoId) in ids.Index()) {
            cmdBuilder.Append($"${i+1}, ");
            parameters.Add(new NpgsqlParameter{ Value = fsoId.Id });

        }
        var len = cmdBuilder.Length;
        cmdBuilder.Remove(len - 2, 2);
        cmdBuilder.Append(");");
        var cmd = _conn.CreateCommand(cmdBuilder.ToString());
        cmd.Parameters.AddRange(parameters.ToArray());
        int result = await cmd.ExecuteNonQueryAsync(token);
        return new Ok<int, DbError>(result);
    }

    public async Task<Option<Directory>> GetRootDirectory(FsoId id, CancellationToken token = default) {
        var fsos = await Get(None<string>(), None<string>(), Some<Action<NpgsqlCommand>>(cmd => {
            cmd.CommandText = $"""
            WITH RECURSIVE ctename AS (
                    SELECT f.id, f.fso_name,f.virtual_location_id,
                    0 AS level
                    FROM fsos f 
                    WHERE f.id = $1
                    UNION ALL
                    SELECT f.id, f.fso_name, f.virtual_location_id ,
                    ctename.level + 1
                    FROM fsos f
                    JOIN ctename ON f.id = ctename.virtual_location_id
                    )
            SELECT {sqlFieldsInOrder} FROM ctename as fsos order by level desc limit 1;
        """;
            cmd.Parameters.Add(new NpgsqlParameter { Value = id.Id });
        }));
        var directory = fsos.FirstOrDefault();
        Assert(directory is Directory or null);
        return directory as Directory;
    }
}
