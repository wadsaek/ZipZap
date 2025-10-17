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
    extension<T, TKey>(MaybeEntity<T, TKey> maybeEntity) where T : IEntity<TKey> {
        public static MaybeEntity<T, TKey> OnlyId(TKey id) => new OnlyId<T, TKey>(id);
        public static MaybeEntity<T, TKey> ExistsEntity(T entity) => new ExistsEntity<T, TKey>(entity);
    }
}
