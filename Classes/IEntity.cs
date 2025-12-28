namespace ZipZap.Classes;

public interface IEntity<out TId> {
    public TId Id { get; }
}
