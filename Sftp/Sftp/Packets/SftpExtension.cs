// SftpExtension.cs - Part of the ZipZap project for storing files online
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
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Sftp.Numbers;
using ZipZap.Sftp.Ssh;

namespace ZipZap.Sftp.Sftp;

public abstract record SftpExtension() {
    internal static bool TryParse(string extname, MemoryStream stream, [NotNullWhen(true)] out SftpExtension? ext) {
        switch (extname) {
            case SftpExtensionDeclaration.LSetStat.ExtensionName: {
                    // out parameters are invariant, sadly
                    var success = LSetStat.TryParse(stream, out var extension);
                    ext = extension;
                    return success;
                }
            default: {
                    // we might not know what the extension means,
                    // but there's no reason to say it's invalid as an extension
                    // moreso, the explicit behaviour for unsupported extensions
                    // is sending SSH_FX_OP_UNSUPPORTED
                    ext = new UnknownExtension(extname);
                    return true;
                }
        }
    }
    public abstract Task<ISftpServerPayload> HandlePacket(uint requestId, ISftpRequestHandler handler, CancellationToken cancellationToken);

    private sealed record UnknownExtension(string Name) : SftpExtension {
        public override Task<ISftpServerPayload> HandlePacket(uint requestId, ISftpRequestHandler handler, CancellationToken cancellationToken) {
            return Task.FromResult(new Status(
                requestId,
                SftpError.OpUnsupported,
                $"{Name} is an unsupported extension") as ISftpServerPayload
            );
        }
    }

    public sealed record LSetStat(string Path, FileAttributes Attrs) : SftpExtension {
        internal static bool TryParse(MemoryStream stream, [NotNullWhen(true)] out LSetStat? ext) {
            ext = null;
            if (!stream.SshTryReadStringSync(out var path)) return false;
            if (!FileAttributes.TryParse(stream, out var attrs)) return false;
            ext = new(path, attrs);
            return true;
        }
        public override async Task<ISftpServerPayload> HandlePacket(uint id, ISftpRequestHandler handler, CancellationToken cancellationToken) {
            var result = await handler.LSetStat(Path, Attrs, cancellationToken);
            return result.ToStatusPacket(id);
        }
    }

}

public abstract record SftpExtensionDeclaration(string Name, string Version) {
    public sealed record LSetStat() : SftpExtensionDeclaration(ExtensionName, "1") {
        public const string ExtensionName = "lsetstat@openssh.com";
    }
}

