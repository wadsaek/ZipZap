// Assertions.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System;

namespace ZipZap.LangExt.Helpers;

public static class Assertions {
    public static void Assert(bool expression, string message = "Assserion failed") {
        if (!expression) throw new ArgumentException(message);
    }
    public static void AssertEq<T>(this T fst, T target, string? message = null)
        where T : IEquatable<T> {
        message ??= $"Assserion failed, fst={fst}, target={target}";
        Assert(fst.Equals(target), message);
    }
}
