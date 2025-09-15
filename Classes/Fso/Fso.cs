using System;

namespace ZipZap.Classes;

public abstract class Fso {
    protected Fso(FsoId id, string name, FsData data) {
        Id = id;
        Name = name;
        Data = data;
    }

    public FsoId Id { get; set; }
    public string Name { get; set; }

    // fs data
    public FsData Data { get; set; }
}

public record struct FsoId(Guid id);
