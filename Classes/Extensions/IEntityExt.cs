using ZipZap.Classes.Helpers;

using static ZipZap.Classes.Helpers.MaybeEntityConstructor;

namespace ZipZap.Classes.Extensions;

public static class EntityExt {
    extension<F>(F fso)
    where F : Fso {
        public MaybeEntity<F, FsoId> AsMaybe() => ExistsEntity<F, FsoId>(fso);
    }
}
