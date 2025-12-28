namespace ZipZap.Classes.Helpers;

public static class Constructors {
    extension<T, E>(Result<T, E>) {
        public static Result<T, E> Ok(T arg) {
            return new Ok<T, E>(arg);
        }
        public static Result<T, E> Err(E err) {
            return new Err<T, E>(err);
        }
    }
    extension<T, TId>(MaybeEntity<T, TId>)
    where T : IEntity<TId>
    where TId : IStrongId {
        public static MaybeEntity<T, TId> OnlyId(TId id) => new OnlyId<T, TId>(id);
        public static MaybeEntity<T, TId> ExistsEntity(T entity) => new ExistsEntity<T, TId>(entity);
    }
}
