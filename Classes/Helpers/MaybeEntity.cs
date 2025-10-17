namespace ZipZap.Classes.Helpers;

public abstract record MaybeEntity<T, TKey>(TKey Id) where T : IEntity<TKey> {
    public static implicit operator MaybeEntity<T, TKey>(TKey Id) => new OnlyId<T, TKey>(Id);
    public static implicit operator MaybeEntity<T, TKey>(T Entity) => new ExistsEntity<T, TKey>(Entity);
}
public sealed record OnlyId<T, TKey>(TKey Id) : MaybeEntity<T, TKey>(Id) where T : IEntity<TKey>;
public sealed record ExistsEntity<T, TKey>(T Entity) : MaybeEntity<T, TKey>(Entity.Id) where T : IEntity<TKey>;
