using ZipZap.Classes.Helpers;

using static ZipZap.Classes.Helpers.Constructors;

namespace ZipZap.Classes.Extensions;

public static class IEntityExt {
    extension<F>(F fso)
    where F : Fso {
        public static MaybeEntity<F, FsoId> WithId(FsoId id) => OnlyId<F, FsoId>(id);
        public MaybeEntity<F, FsoId> AsMaybe() => ExistsEntity<F, FsoId>(fso);
    }
}
