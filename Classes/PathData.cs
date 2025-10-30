using System.Collections.Generic;

namespace ZipZap.Classes;

public abstract record PathData(string Name);
public sealed record PathDataWithPath(string Name, IEnumerable<string> Path) : PathData(Name);
public sealed record PathDataWithId(string Name, FsoId ParentId) : PathData(Name);
