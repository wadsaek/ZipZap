using ZipZap.Classes.Helpers;

using static ZipZap.Classes.Helpers.MaybeEntityConstructor;

namespace ZipZap.Classes;

public interface IStrongId;

public static class StrongIdExt {
    extension(FsoId id) {
        public MaybeEntity<TEntity, FsoId> AsIdOf<TEntity>() where TEntity : IEntity<FsoId>
            => OnlyId<TEntity, FsoId>(id);
    }
}
