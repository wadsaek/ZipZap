namespace ZipZap.Persistance.Data;

public interface ITranslatable<T> {
    public T Into();
    public static abstract ITranslatable<T> From(T entity);
}
