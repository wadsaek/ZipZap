using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

using ZipZap.Classes.Adapters;
using ZipZap.Classes.Helpers;
using ZipZap.Grpc;
using ZipZap.LangExt.Helpers;

using static ZipZap.LangExt.Helpers.ResultConstructor;

namespace ZipZap.Front.Services;

public class LoginService : ILoginService {
    private readonly FilesStoringService.FilesStoringServiceClient _filesStoringService;
    private readonly ExceptionConverter<ServiceError> _exceptionConverter;

    public LoginService(FilesStoringService.FilesStoringServiceClient filesStoringService, ExceptionConverter<ServiceError> exceptionConverter) {
        _filesStoringService = filesStoringService;
        _exceptionConverter = exceptionConverter;
    }

    public async Task<Result<string, LoginError>> Login(string username, string password, CancellationToken cancellationToken = default) {
        if (new[] { username, password }.Any(string.IsNullOrWhiteSpace))
            return Err<string, LoginError>(new LoginError.EmptyCredentials());
        try {
            var response = await _filesStoringService.LoginAsync(new() { Username = username, Password = password }, cancellationToken: cancellationToken);
            return Ok<string, LoginError>(response.Token);
        } catch (RpcException exception) {
            if (exception.StatusCode == StatusCode.Unauthenticated)
                return Err<string, LoginError>(new LoginError.WrongCredentials());
            return Err<string, LoginError>(new LoginError.Service(_exceptionConverter.Convert(exception)));
        }
    }

    public async Task<Result<string, SignupError>> SignUp(SignUpInfo signUpInfo, CancellationToken cancellationToken = default) {
        if (new[] { signUpInfo.Email, signUpInfo.Password, signUpInfo.Username }.Any(string.IsNullOrWhiteSpace))
            return Err<string, SignupError>(new SignupError.EmptyCredentials());
        try {
            var response = await _filesStoringService.SignUpAsync(
                new SignUpRequest {
                    Email = signUpInfo.Email,
                    Password = signUpInfo.Password,
                    Username = signUpInfo.Username,
                    DefaultOwnership = signUpInfo.Ownership.ToGrpcOwnership()
                },
                cancellationToken: cancellationToken
            );
            return response.ResponseCase switch {
                SignUpResponse.ResponseOneofCase.Ok => Ok<string, SignupError>(response.Ok.Token),
                SignUpResponse.ResponseOneofCase.Error => Err<string, SignupError>(response.Error switch {

                    SignUpError.InvalidEmail => new SignupError.InvalidEmail(),
                    SignUpError.InvalidPassword => new SignupError.InvalidPassword(),
                    SignUpError.InvalidUsername => new SignupError.InvalidLogin(),
                    _ => throw new NotImplementedException(),
                }),
                _ or SignUpResponse.ResponseOneofCase.None => throw new InvalidEnumArgumentException(),
            };
        } catch (RpcException exception) {
            SignupError error = exception.Status switch {
                { StatusCode: StatusCode.AlreadyExists } => new SignupError.UserExists(),
                _ => new SignupError.Other(exception.ToString())
            };
            return Err<string, SignupError>(error);
        }
    }
}

