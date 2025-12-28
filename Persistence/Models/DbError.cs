namespace ZipZap.Persistence.Models;

public abstract record DbError {
    public sealed record ScalarNotReturned: DbError;
    public sealed record UniqueViolation: DbError;
    public sealed record NothingCreated: DbError;
    public sealed record Unknown: DbError;
}
