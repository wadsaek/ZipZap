// SshBuilder.cs - Part of the ZipZap project for storing files online
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace ZipZap.Sftp.Ssh;

public class SshMessageBuilder {
    private readonly List<SshItem> items = [];

    private abstract record SshItem {
        public abstract int Length { get; }
    };
    private sealed record ByteItem(byte Value) : SshItem { public override int Length => 1; }
    private sealed record ByteArrayItem(byte[] Value) : SshItem { public override int Length => Value.Length; }
    private sealed record ByteStringItem(byte[] Value) : SshItem { public override int Length => Value.Length + 4; }
    private sealed record BooleanItem(bool Value) : SshItem { public override int Length => 1; }
    private sealed record Uint32Item(uint Value) : SshItem { public override int Length => 4; }
    private sealed record Uint64Item(ulong Value) : SshItem { public override int Length => 8; }
    private sealed record Int32Item(int Value) : SshItem { public override int Length => 4; }
    private sealed record Int64Item(long Value) : SshItem { public override int Length => 8; }
    private sealed record StringItem(string Value) : SshItem { public override int Length => 4 + Value.Length; }
    private sealed record BigIntegerItem(BigInteger Value) : SshItem { public override int Length => 4 + Value.GetByteCount(); }

    public SshMessageBuilder Write(byte value) { items.Add(new ByteItem(value)); return this; }
    public SshMessageBuilder Write(string value) { items.Add(new StringItem(value)); return this; }
    public SshMessageBuilder Write(bool value) { items.Add(new BooleanItem(value)); return this; }
    public SshMessageBuilder Write(uint value) { items.Add(new Uint32Item(value)); return this; }
    public SshMessageBuilder Write(ulong value) { items.Add(new Uint64Item(value)); return this; }
    public SshMessageBuilder Write(long value) { items.Add(new Int64Item(value)); return this; }
    public SshMessageBuilder Write(int value) { items.Add(new Int32Item(value)); return this; }
    public SshMessageBuilder Write(BigInteger value) { items.Add(new BigIntegerItem(value)); return this; }
    public SshMessageBuilder WriteArray(byte[] bytes) { items.Add(new ByteArrayItem(bytes)); return this; }
    public SshMessageBuilder WriteByteString(byte[] bytes) { items.Add(new ByteStringItem(bytes)); return this; }
    public SshMessageBuilder WriteByteString(IToByteString value) => WriteByteString(value.ToByteString());

    public byte[] Build() {
        var length = items.Sum(i => i.Length);
        var buffer = new byte[length];

        using var stream = new MemoryStream(buffer);
        foreach (var item in items) {
            switch (item) {
                case ByteItem(var value):
                    stream.SshWriteByteSync(value);
                    break;
                case ByteArrayItem(var value):
                    stream.SshWriteArraySync(value);
                    break;
                case ByteStringItem(var value):
                    stream.SshWriteByteStringSync(value);
                    break;
                case BooleanItem(var value):
                    stream.SshWriteBoolSync(value);
                    break;
                case Uint32Item(var value):
                    stream.SshWriteUint32Sync(value);
                    break;
                case Uint64Item(var value):
                    stream.SshWriteUint64Sync(value);
                    break;
                case Int32Item(var value):
                    stream.SshWriteInt32Sync(value);
                    break;
                case Int64Item(var value):
                    stream.SshWriteInt64Sync(value);
                    break;
                case StringItem(var value):
                    stream.SshWriteStringSync(value);
                    break;
                case BigIntegerItem(var value):
                    stream.SshWriteBigIntSync(value);
                    break;
            }
        }
        return buffer;
    }
}
