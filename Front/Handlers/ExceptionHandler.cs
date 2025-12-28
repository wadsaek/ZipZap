using Grpc.Core;

using ZipZap.Classes.Helpers;
using ZipZap.Front.Services;

namespace ZipZap.Front.Handlers.Exceptions;

public static class ServiceExceptionHandler {
    public static ExceptionConverter<ServiceError> GetExceptionConverter() => new SimpleExceptionConverter<ServiceError>(ex => ex switch {
        RpcException { StatusCode: StatusCode.Unauthenticated } => new ServiceError.Unathorized(),
        RpcException { StatusCode: StatusCode.NotFound } => new ServiceError.NotFound(),
        RpcException { StatusCode: StatusCode.FailedPrecondition, Status.Detail: var detail } => new ServiceError.FailedPrecondition(detail),
        _ => new ServiceError.Unknown(ex)

    });
}
