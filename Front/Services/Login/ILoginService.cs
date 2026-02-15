// ILoginService.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

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
