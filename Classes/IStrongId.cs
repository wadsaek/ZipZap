using ZipZap.Classes.Helpers;
using static ZipZap.Classes.Helpers.Constructors;

namespace ZipZap.Classes;
public interface IStrongId;

public static class IStrongIdExt{
    extension(FsoId id){
        public MaybeEntity<TEntity,FsoId> AsIdOf<TEntity>() where TEntity: IEntity<FsoId>
            => OnlyId<TEntity,FsoId>(id);
    }
}
