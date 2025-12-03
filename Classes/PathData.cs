using System.Linq;

using ZipZap.Classes.Extensions;

namespace ZipZap.Classes;

public abstract record PathData(string Name);
public sealed record PathDataWithPath(string Path) : PathData(
    Path
    .SplitPath()
    .LastOrDefault("/")
);
public sealed record PathDataWithId(string Name, FsoId ParentId) : PathData(Name);
