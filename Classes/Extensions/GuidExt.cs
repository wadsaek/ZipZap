using System;


namespace ZipZap.Classes.Extensions;

public static class GuidExt {
    extension(Guid guid) {
        public UserId ToUserId() => new(guid);
        public FsoId ToFsoId() => new(guid);
        public FsoAccessId ToFsoAccessId() => new(guid);
        public Grpc.Guid ToGrpcGuid() => new() { Value = guid.ToString() };
    }
}
