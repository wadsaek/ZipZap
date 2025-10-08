using ZipZap.Classes.Helpers;
using ZipZap.FileService.Models;

public static class DbHelper{
    public static Result<Unit,DbError> EnsureSingle(int n)
 => n switch {
            0 => new Err<Unit, DbError>(new DbError()),
            1 => new Ok<Unit, DbError>(new Unit()),
            _ => throw new System.IO.InvalidDataException()
        };
}
