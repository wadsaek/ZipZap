namespace ZipZap.Classes;
public class FsData {
    public FsData(Directory? virtualLocation, int fsoOwner, int fsoGroup) {
        VirtualLocation = virtualLocation;
        FsoOwner = fsoOwner;
        FsoGroup = fsoGroup;
    }

    public Directory? VirtualLocation { get; set; }

    public int FsoOwner { get; set; }
    public int FsoGroup { get; set; }
}
