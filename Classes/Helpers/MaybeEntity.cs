namespace ZipZap.Classes.Helpers;

public abstract record MaybeEntity<T, TId>(TId Id)
where T : IEntity<TId>
where TId : IStrongId {
    public static implicit operator MaybeEntity<T, TId>(TId Id) => new OnlyId<T, TId>(Id);
    public static implicit operator MaybeEntity<T, TId>(T Entity) => new ExistsEntity<T, TId>(Entity);
    public static implicit operator TId(MaybeEntity<T, TId> MaybeEntity) => MaybeEntity.Id;
}

public sealed record OnlyId<T, TId>(TId Id) : MaybeEntity<T, TId>(Id)
where T : IEntity<TId>
where TId : IStrongId;

public sealed record ExistsEntity<T, TId>(T Entity) : MaybeEntity<T, TId>(Entity.Id)
where T : IEntity<TId>
where TId : IStrongId;
