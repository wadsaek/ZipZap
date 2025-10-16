using System;

namespace ZipZap.Classes;

public abstract class Fso : IEntity<FsoId> {
    protected Fso(FsoId id, FsData data) {
        Id = id;
        Data = data;
    }
    protected Fso() {
        Id = default;
        Data = null!;
    }

    public FsoId Id { get; set; }

    // fs data
    public FsData Data { get; set; }
}

public record struct FsoId(Guid Id);
