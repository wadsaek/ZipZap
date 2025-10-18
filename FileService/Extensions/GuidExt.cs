using System;

using ZipZap.Classes;

namespace ZipZap.FileService.Extensions;

public static class GuidExt {
    public static UserId ToUserId(this Guid guid) => new UserId(guid);
    public static FsoId ToFsoId(this Guid guid) => new FsoId(guid);
}
