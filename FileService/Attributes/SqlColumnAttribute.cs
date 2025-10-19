using System;

namespace ZipZap.FileService.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class SqlColumnAttribute(string column) : Attribute {
    public string Column { get; set; } = column;
}
