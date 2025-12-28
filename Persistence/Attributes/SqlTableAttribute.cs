using System;

namespace ZipZap.Persistence.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class SqlTableAttribute(string table) : Attribute {
    public string Table { get; } = table;
}
