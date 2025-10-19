using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.FileService.Attributes;
using ZipZap.FileService.Data;
using ZipZap.FileService.Extensions;

using static ZipZap.Classes.Helpers.Constructors;

namespace ZipZap.FileService.Repositories;

internal abstract class EntityHelper<TInner, TEntity, TId>
where TInner : ITranslatable<TEntity>, ISqlRetrievable {
    public EntityHelper() {
        var fields = typeof(TInner).GetProperties();
        SqlFieldsList = fields.Select(
                f => (f,
                    f.GetCustomAttribute<SqlColumnAttribute>()?.Column
                    ?? throw new Exception($"field {f.Name} in {nameof(TInner)} lacks the {nameof(SqlColumnAttribute)} attribute"))
                ).ToImmutableList();

        TableName = typeof(TInner).GetCustomAttribute<SqlTableAttribute>()?.Table
                        ?? throw new Exception($"{nameof(TInner)} lacks the {nameof(SqlTableAttribute)} attribute");

    }
    public string TableName { get; private init; }

    public IImmutableList<(PropertyInfo Key, string sqlName)> SqlFieldsList { get; private init; }

    public IEnumerable<string> SqlFields => SqlFieldsList.Select(pair => pair.sqlName);
    public IEnumerable<string> SqlFieldsPrefixed => SqlFields.Select(f => $"{TableName}.{f} as {TableName}_{f}");
    public string SqlFieldsInOrder => SqlFieldsPrefixed.ConcatenateWith(", ");
    public string GetColumnName(string nameOfField) => SqlFieldsList
        .FirstOrDefault(pair => pair.Key == typeof(TInner).GetProperty(nameOfField)!)
        .sqlName;

    public abstract Task<TEntity> Parse(NpgsqlDataReader reader, CancellationToken token = default);
    public static TInner ToInner(TEntity entity) => (TInner)TInner.From(entity);

    public static void FillParameters(NpgsqlCommand cmd, TInner entity, IEnumerable<(PropertyInfo Key, string sqlName)> properties) {
        var parameters = properties.Select(
                f =>
            (Activator.CreateInstance(
                typeof(NpgsqlParameter<>).MakeGenericType(f.Key.PropertyType)
                ), f.Key)
                ).Select(
                    obj => {
                        obj.Item1?
                            .GetType()
                            .GetProperty(nameof(NpgsqlParameter<>.Value))?
                            .SetValue(obj.Item1, obj.Key.GetValue(entity));
                        return obj.Item1;
                    });
        cmd.Parameters.AddRange(parameters.Cast<NpgsqlParameter>().ToArray());
    }
    public void FillParameters(NpgsqlCommand cmd, TInner entity) => FillParameters(cmd, entity, SqlFieldsList);
    public void FillParameters(NpgsqlCommand cmd, TEntity entity)
        => FillParameters(cmd, ToInner(entity));

    public abstract string IdCol { get; }
    public abstract TInner CloneWithId(TInner entity, TId id);
}

