namespace ZipZap.Classes.Helpers;

public abstract record MaybeEntity<T, TId>(TId Id)
where T : IEntity<TId>
where TId : IStrongId {
    public static implicit operator MaybeEntity<T, TId>(TId id) => new OnlyId<T, TId>(id);
    public static implicit operator MaybeEntity<T, TId>(T entity) => new ExistsEntity<T, TId>(entity);
    public static implicit operator TId(MaybeEntity<T, TId> maybeEntity) => maybeEntity.Id;
}

public sealed record OnlyId<T, TId>(TId Id) : MaybeEntity<T, TId>(Id)
where T : IEntity<TId>
where TId : IStrongId;

public sealed record ExistsEntity<T, TId>(T Entity) : MaybeEntity<T, TId>(Entity.Id)
where T : IEntity<TId>
where TId : IStrongId;
