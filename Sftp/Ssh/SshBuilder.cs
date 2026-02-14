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
    private sealed record BigIntegerItem(BigInteger Value, bool IsUnsigned) : SshItem { public override int Length => 4 + Value.GetByteCount(IsUnsigned); }

    public SshMessageBuilder Write(byte value) { items.Add(new ByteItem(value)); return this; }
    public SshMessageBuilder Write(string value) { items.Add(new StringItem(value)); return this; }
    public SshMessageBuilder Write(bool value) { items.Add(new BooleanItem(value)); return this; }
    public SshMessageBuilder Write(uint value) { items.Add(new Uint32Item(value)); return this; }
    public SshMessageBuilder Write(ulong value) { items.Add(new Uint64Item(value)); return this; }
    public SshMessageBuilder Write(long value) { items.Add(new Int64Item(value)); return this; }
    public SshMessageBuilder Write(int value) { items.Add(new Int32Item(value)); return this; }
    public SshMessageBuilder Write(BigInteger value) => Write(value, false);
    public SshMessageBuilder Write(BigInteger value, bool isUnsigned) { items.Add(new BigIntegerItem(value, isUnsigned)); return this; }
    public SshMessageBuilder WriteArray(byte[] bytes) { items.Add(new ByteArrayItem(bytes)); return this; }
    public SshMessageBuilder WriteByteString(byte[] bytes) { items.Add(new ByteStringItem(bytes)); return this; }

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
                case BigIntegerItem(var value, var isUnsigned):
                    stream.SshWriteBigIntSync(value, isUnsigned);
                    break;
            }
        }
        return buffer;
    }
}
