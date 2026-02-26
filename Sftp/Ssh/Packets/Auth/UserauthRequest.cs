// UserauthRequest.cs - Part of the ZipZap project for storing files online
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


using System.Diagnostics.CodeAnalysis;
using System.IO;

using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh.Auth;

public abstract record UserauthRequest(string Username, string ServiceName) : IClientPayload<UserauthRequest> {
    public static Message Message => Message.UserauthRequest;

    public static bool TryParse(byte[] payload, [NotNullWhen(true)] out UserauthRequest? value) {
        value = null;
        var stream = new MemoryStream(payload);
        if (!stream.SshTryReadByteSync(out var msg) || (Message)msg != Message) return false;
        if (!stream.SshTryReadStringSync(out var username)) return false;
        if (!stream.SshTryReadStringSync(out var serviceName)) return false;
        if (!stream.SshTryReadStringSync(out var methodName)) return false;
        switch (methodName) {
            case None.MethodName: {
                    value = new None(username, serviceName);
                    break;
                }
            case PublicKey.MethodName: {
                    if (!stream.SshTryReadBoolSync(out var hasSignature)) return false;
                    if (!stream.SshTryReadStringSync(out var algName)) return false;
                    if (!stream.SshTryReadByteStringSync(out var blob)) return false;
                    if (!hasSignature) {
                        value = new PublicKey.WithoutSignature(
                            username,
                            serviceName,
                            algName,
                            blob
                        );
                        break;
                    }
                    if (!stream.SshTryReadByteStringSync(out var signature)) return false;
                    value = new PublicKey.WithSignature(
                        username,
                        serviceName,
                        algName,
                        blob,
                        signature
                    );
                    break;
                }
            case Password.MethodName: {
                    if (!stream.SshTryReadBoolSync(out var changesPassword)) return false;
                    if (!stream.SshTryReadStringSync(out var password)) return false;
                    if (!changesPassword) {
                        value = new Password.NotChanged(username, serviceName, password);
                        break;
                    }
                    if (!stream.SshTryReadStringSync(out var newPassword)) return false;
                    value = new Password.Changed(username, serviceName, password, newPassword);
                    break;
                }
            default: {
                    value = new Unrecognized(username, serviceName, payload);
                    break;
                }
        }
        return true;
    }
    public sealed record None(string Username, string ServiceName) : UserauthRequest(Username, ServiceName) {
        public const string MethodName = "none";
    }

    public abstract record PublicKey(string Username, string ServiceName, string AlgName, byte[] PublicKeyBytes) : UserauthRequest(Username, ServiceName) {
        public const string MethodName = "publickey";
        public sealed record WithSignature(
            string Username, string ServiceName, string AlgName, byte[] PublicKeyBytes, byte[] Signature
        ) : PublicKey(Username, ServiceName, AlgName, PublicKeyBytes);
        public sealed record WithoutSignature(
            string Username, string ServiceName, string AlgName, byte[] PublicKeyBytes
        ) : PublicKey(Username, ServiceName, AlgName, PublicKeyBytes);
    }

    public abstract record Password(string Username, string ServiceName, string CurrentPassword) : UserauthRequest(Username, ServiceName) {
        public const string MethodName = "password";

        public sealed record NotChanged(
            string Username,
            string ServiceName,
            string CurrentPassword
        ) : Password(Username, ServiceName, CurrentPassword);

        public sealed record Changed(
            string Username,
            string ServiceName,
            string CurrentPassword,
            string NewPassword
        ) : Password(Username, ServiceName, CurrentPassword);
    }
    public sealed record Unrecognized(string Username, string ServiceName, byte[] Packet) : UserauthRequest(Username, ServiceName);
}
