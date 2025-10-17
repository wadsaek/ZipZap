using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.FileService.Extensions;
using ZipZap.Classes.Helpers;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using static ZipZap.Classes.Helpers.Constructors;

namespace ZipZap.FileService.Repositories;

public interface IEntityHelper<T> {
    public string SqlFieldsInOrder { get; }
    public Task<T> Parse(NpgsqlDataReader reader, CancellationToken token = default);
}
public class FsoHelper : IEntityHelper<Fso> {
    public IEnumerable<string> SqlFields =>
        [
        "id", "fso_name", "virtual_location_id", "permissions", "fso_owner",
        "fso_group", "fso_type", "link_ref", "file_physical_path"
        ];
    public IEnumerable<string> SqlFieldsPrefixed => SqlFields.Select(f => $"fsos.{f}");
    public string SqlFieldsInOrder => SqlFieldsPrefixed.Aggregate((acc, next) => $"{acc}, {next}");

    public async Task<Fso> Parse(NpgsqlDataReader reader, CancellationToken token = default) {
        var id = await reader.GetFieldValueAsync<Guid>("fsos.id", token);
        var name = await reader.GetFieldValueAsync<string>("fsos.fso_name", token);
        var virtualLocationId = await reader.GetFieldValueAsync<Guid?>("fsos.virtual_location_id", token);
        var permissions = await reader.GetFieldValueAsync<BitArray>("fsos.permissions", token);
        var fsoOwner = await reader.GetFieldValueAsync<int>("fsos.fso_owner", token);
        var fsoGroup = await reader.GetFieldValueAsync<int>("fsos.fsoGroup", token);
        var fsoType = await reader.GetFieldValueAsync<FsoType>("fsos.fso_type", token);
        var linkRef = await reader.GetNullableFieldValueAsync<string>("fsos.link_ref", token);
        var filePhysicalPath = await reader.GetNullableFieldValueAsync<string>("fsos.file_physical_path", token);

        var virtualLocation = virtualLocationId
            .ToOption()
            .Select(id => new FsoId(id))
            .Select(Directory.WithId);

        var data = new FsData(
                                virtualLocation,
                                Permissions.FromBitArray(permissions),
                                name,
                                fsoOwner,
                                fsoGroup
                                );
        return fsoType switch {
            FsoType.RegularFile => new File( new FsoId(id), data),
            FsoType.Directory => new Directory( new FsoId(id), data),
            FsoType.Symlink => new Symlink( new FsoId(id), data, linkRef!),
            _ => throw new InvalidEnumVariantException(nameof(fsoType))
        };
    }
}
