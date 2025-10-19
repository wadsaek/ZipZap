namespace ZipZap.Classes.Helpers;

public static class Constructors {
    extension<T>(Option<T> option) {
        public static Option<T> Some(T arg) {
            return new Some<T>(arg);
        }
        public static Option<T> None() {
            return new None<T>();
        }
    }
    extension<T, TId>(MaybeEntity<T, TId> maybeEntity) where T : IEntity<TId> {
        public static MaybeEntity<T, TId> OnlyId(TId id) => new OnlyId<T, TId>(id);
        public static MaybeEntity<T, TId> ExistsEntity(T entity) => new ExistsEntity<T, TId>(entity);
    }
}
