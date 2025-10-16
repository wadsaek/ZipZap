using System.Collections.Generic;
using ZipZap.Classes.Helpers;
using static ZipZap.Classes.Helpers.OptionExt;

namespace ZipZap.Classes;

sealed public class Directory : Fso {
    public Directory() {
        MaybeChildren = None<IEnumerable<Fso>>();
    }
    public Option<IEnumerable<Fso>> MaybeChildren { get; set; }

    private Directory(
        FsoId id,
        FsData data,
        Option<IEnumerable<Fso>> children
        ) : base(id, data) {
        MaybeChildren = children;
    }
    public Directory(
        FsoId id,
        FsData data
        ) : this(id, data, None<IEnumerable<Fso>>()) {
    }
    public Directory(
        FsoId id,
        FsData data,
        IEnumerable<Fso> children
        ) : this(id, data, Some(children)) {
    }

}
