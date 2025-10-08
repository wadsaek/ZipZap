using System;

namespace ZipZap.Classes;

public abstract class Fso : IEntity<FsoId> {
    protected Fso(FsoId id, string name, FsData data) {
        Id = id;
        Name = name;
        Data = data;
    }
    protected Fso() {
        Id = default;
        Name = null!;
        Data = null!;
    }

    public FsoId Id { get; set; }
    public string Name { get; set; }

    // fs data
    public FsData Data { get; set; }
}

public record struct FsoId(Guid Id);
