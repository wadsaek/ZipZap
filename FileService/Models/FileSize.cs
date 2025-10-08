public record struct FileSize(long bytes) {
    public static FileSize FromBytes(long bytes) => new FileSize(bytes);
    public static FileSize FromKiloBytes(long kb) => FromBytes(kb << 10);
    public static FileSize FromMegaBytes(long mb) => FromKiloBytes(mb << 10);

    public long AsBytes() => bytes;
    public long AsKiloBytes() => bytes >> 10;
    public long AsMegaBytes() => AsKiloBytes() >> 10;

    public static FileSize operator +(FileSize fs1, FileSize fs2) => new FileSize(fs1.bytes + fs2.bytes);
}
