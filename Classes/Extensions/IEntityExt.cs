using ZipZap.Classes.Helpers;

using static ZipZap.Classes.Helpers.Constructors;

namespace ZipZap.Classes.Extensions;

public static class IEntityExt {
    extension<F>(F fso)
    where F : Fso {
        public MaybeEntity<F, FsoId> AsMaybe() => ExistsEntity<F, FsoId>(fso);
    }
}
