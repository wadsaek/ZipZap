namespace ZipZap.Front.Handlers;

public record FileSpecification(string? Identifier, IdType Type);
public enum IdType {
    Path = 0,
    Id
}
