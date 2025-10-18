namespace ZipZap.FileService.Extensions;

public record struct FileSize(long Bytes) {
    public static FileSize FromBytes(long bytes) => new(bytes);
    public static FileSize FromKiloBytes(long kb) => FromBytes(kb << 10);
    public static FileSize FromMegaBytes(long mb) => FromKiloBytes(mb << 10);

    public readonly long AsBytes() => Bytes;
    public readonly long AsKiloBytes() => Bytes >> 10;
    public readonly long AsMegaBytes() => AsKiloBytes() >> 10;

    public static FileSize operator +(FileSize fs1, FileSize fs2) => new(fs1.Bytes + fs2.Bytes);
}
