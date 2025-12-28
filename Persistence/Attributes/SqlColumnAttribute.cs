using System;

namespace ZipZap.Persistence.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class SqlColumnAttribute(string column) : Attribute {
    public string Column { get; } = column;
}
