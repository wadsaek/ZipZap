using System;

namespace ZipZap.Persistance.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class SqlTableAttribute(string table) : Attribute {
    public string Table { get; set; } = table;
}
