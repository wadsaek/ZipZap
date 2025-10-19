namespace ZipZap.Classes;

public interface IEntity<TId> {
    public TId Id { get; }
}
