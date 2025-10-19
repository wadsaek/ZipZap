namespace ZipZap.Classes.Helpers;

public abstract record MaybeEntity<T, TId>(TId Id) where T : IEntity<TId> {
    public static implicit operator MaybeEntity<T, TId>(TId Id) => new OnlyId<T, TId>(Id);
    public static implicit operator MaybeEntity<T, TId>(T Entity) => new ExistsEntity<T, TId>(Entity);
}
public sealed record OnlyId<T, TId>(TId Id) : MaybeEntity<T, TId>(Id) where T : IEntity<TId>;
public sealed record ExistsEntity<T, TId>(T Entity) : MaybeEntity<T, TId>(Entity.Id) where T : IEntity<TId>;
