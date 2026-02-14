using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Services;

public interface ILoginService {
    public Task<Result<string, LoginError>> Login(string username, string password, CancellationToken cancellationToken = default);
    public Task<Result<string, SignupError>> SignUp(SignUpInfo signUpInfo, CancellationToken cancellationToken = default);
}

public abstract record SignupError {
    public sealed record UserExists : SignupError;
    public sealed record InvalidLogin : SignupError;
    public sealed record InvalidPassword : SignupError;
    public sealed record InvalidEmail : SignupError;
    public sealed record EmptyCredentials : SignupError;
    public sealed record Other(string Detail) : SignupError;
}

public sealed record SignUpInfo(
    string Username,
    string Password,
    string Email,
    Ownership Ownership
);

public abstract record LoginError {
    public sealed record WrongCredentials : LoginError {
        public override string ToString() => "One or more fields is wrong";
    }
    public sealed record EmptyCredentials : LoginError {
        public override string ToString() => "One or more fields is empty";
    }
    public sealed record Service(ServiceError ServiceError) : LoginError;
}
