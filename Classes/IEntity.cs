namespace ZipZap.Classes;

public interface IEntity<TKey> {
    public TKey Id { get; }
}
