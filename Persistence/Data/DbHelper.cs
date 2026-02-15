// DbHelper.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using ZipZap.Classes.Helpers;
using ZipZap.LangExt.Helpers;
using ZipZap.Persistence.Models;

namespace ZipZap.Persistence.Data;

public static class DbHelper {
    public static Result<Unit, DbError> EnsureSingle(int n)
 => n switch {
     0 => new Err<Unit, DbError>(new DbError.NothingChanged()),
     1 => new Ok<Unit, DbError>(new Unit()),
     _ => throw new System.IO.InvalidDataException()
 };
}
