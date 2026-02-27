// ExtInfo.cs - Part of the ZipZap project for storing files online
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
using System.Text;

using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh;

public record ExtInfo(Extension[] Extensions) : IServerPayload, IClientPayload<ExtInfo> {
    public static Message Message => Message.ExtInfo;

    public static bool TryParse(byte[] payload, [NotNullWhen(true)] out ExtInfo? value) {
        value = null;
        var stream = new MemoryStream(payload);
        if (!(stream.SshTryReadByteSync(out var msg) && msg == (byte)Message))
            return false;
        if (!stream.SshTryReadUint32Sync(out var count)) return false;
        var extensions = new Extension?[count];
        for (var i = 0; i < count; i++) {
            if (!stream.SshTryReadStringSync(out var extName)) return false;
            if (!stream.SshTryReadByteStringSync(out var extValue)) return false;
            var extensionResult = extName switch {
                Extension.NoFlowControl.Name => Extension.NoFlowControl.TryParse(extValue, out extensions[i]),
                Extension.ServerSigAlgs.Name => Extension.ServerSigAlgs.TryParse(extValue, out extensions[i]),
                _ => (extensions[i] = new Extension.Other(extName, extValue)) is not null

            };
            if (!extensionResult) return false;
        }
        value = new(extensions!);
        return true;
    }

    public byte[] ToPayload() {
        var builder = new SshMessageBuilder();
        builder.Write((byte)Message);
        builder.Write((uint)Extensions.Length);
        foreach (var ext in Extensions) {
            builder
                .Write(ext.Name)
                .WriteByteString(ext.GetValue());
        }
        return builder.Build();
    }
}

public abstract record Extension(string Name) {
    public abstract byte[] GetValue();
    public sealed record ServerSigAlgs(NameList Algs) : Extension(Name) {
        public new const string Name = "server-sig-algs";
        public static bool TryParse(byte[] bytes, [NotNullWhen(true)] out Extension? serverSigAlg) {
            serverSigAlg = null;
            var str = Encoding.ASCII.GetString(bytes);
            if (!NameList.TryParse(str, out var names)) return false;
            serverSigAlg = new ServerSigAlgs(names);
            return true;
        }

        public override byte[] GetValue() {
            return Algs.ToByteString();
        }
    }
    public sealed record NoFlowControl(bool IsPreferred) : Extension(Name) {
        public new const string Name = "no-flow-control";

        internal static bool TryParse(byte[] bytes, [NotNullWhen(true)] out Extension? noFlowControl) {
            var (success, result) = bytes switch {
                [(byte)'p'] => (true, true),
                [(byte)'s'] => (true, false),
                _ => (false, default)
            };
            noFlowControl = null;
            if (!success) return false;
            noFlowControl = new NoFlowControl(result);
            return true;
        }

        public override byte[] GetValue() => IsPreferred ? [(byte)'p'] : [(byte)'s'];
    }
    public sealed record Other(string Name, byte[] Value) : Extension(Name) {
        public override byte[] GetValue() => Value;
    }

}
