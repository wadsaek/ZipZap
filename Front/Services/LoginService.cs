using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

using ZipZap.Classes.Helpers;
using ZipZap.Grpc;

using static ZipZap.Classes.Helpers.Constructors;

namespace ZipZap.Front.Services;

public class LoginService : ILoginSerivce {
    private readonly FilesStoringService.FilesStoringServiceClient _filesStoringService;
    private readonly ExceptionConverter<ServiceError> _exceptionConverter;

    public LoginService(FilesStoringService.FilesStoringServiceClient filesStoringService, ExceptionConverter<ServiceError> exceptionConverter) {
        _filesStoringService = filesStoringService;
        _exceptionConverter = exceptionConverter;
    }

    public async Task<Result<string, LoginError>> Login(string username, string password, CancellationToken cancellationToken = default) {
        if(new[] { username,password }.Any(string.IsNullOrWhiteSpace))
            return Err<string,LoginError>(new EmptyCredentials());
        try {
            var response = await _filesStoringService.LoginAsync(new() { Username = username, Password = password }, cancellationToken: cancellationToken);
            return Ok<string, LoginError>(response.Token);
        } catch (RpcException exception) {
            if (exception.StatusCode == StatusCode.Unauthenticated)
                return Err<string, LoginError>(new WrongCredentials());
            return Err<string, LoginError>(new LoginServiceError(_exceptionConverter.Convert(exception)));
        }
    }
}

