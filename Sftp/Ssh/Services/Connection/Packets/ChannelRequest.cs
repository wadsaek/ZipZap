// ChannelRequest.cs - Part of the ZipZap project for storing files online
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
namespace ZipZap.Sftp.Ssh.Services.Connection.Packets;

// byte      SSH_MSG_CHANNEL_REQUEST
// uint32    recipient channel
// string    request type in US-ASCII characters only
// boolean   want reply
// ....      type-specific data follows
public abstract record ChannelRequest(uint Recipient, bool WantReply, ChannelRequestSpecificData SpecificData) : IClientPayload<ChannelRequest> {
    public static Message Message => Message.ChannelRequest;

    public static bool TryParse(byte[] payload, [MaybeNullWhen(false)] out ChannelRequest value) {
        value = null;
        var stream = new MemoryStream(payload);
        if (!stream.ExpectMessage(Message)) return false;
        if (!stream.SshTryReadUint32Sync(out var recipient)) return false;
        if (!stream.SshTryReadStringSync(out var requestType)) return false;
        if (!stream.SshTryReadBoolSync(out var wantReply)) return false;
        switch (requestType) {
            case ChannelRequestSpecificData.Subsystem.RequestType: {
                    if (!ChannelRequestSpecificData.Subsystem.TryParse(stream, out var data)) return false;
                    value = new ChannelRequestGen<ChannelRequestSpecificData.Subsystem>(recipient, wantReply, data);
                    return true;
                }
            default: {
                    value = new ChannelRequestGen<ChannelRequestSpecificData.UnrecognizedData>(
                        recipient,
                        wantReply,
                        new(
                            requestType,
                            payload[(int)stream.Position..]
                        )
                    );
                    return true;
                }
        }
    }

    public sealed record ChannelRequestGen<T>(uint Recipient, bool WantReply, T SpecificDataGen)
    : ChannelRequest(Recipient, WantReply, SpecificDataGen)
    where T : ChannelRequestSpecificData;
}
public abstract record ChannelRequestSpecificData {
    public abstract string GetRequestType();
    public abstract byte[] ToPayload();
    public sealed record Subsystem(string Name) : ChannelRequestSpecificData {
        public const string RequestType = "subsystem";
        public override string GetRequestType() => RequestType;

        public override byte[] ToPayload() => new SshMessageBuilder().Write(Name).Build();
        public static bool TryParse(MemoryStream stream, [MaybeNullWhen(false)] out Subsystem value) {
            value = null;
            if (!stream.SshTryReadStringSync(out var name)) return false;
            value = new(name);
            return true;

        }
    }
    public sealed record UnrecognizedData(string ChannelType, byte[] Value) : ChannelRequestSpecificData {
        public override string GetRequestType() => ChannelType;

        public override byte[] ToPayload() => Value;
    }
}
