// Deffered.cs - Part of the ZipZap project for storing files online
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
using System.Threading.Tasks;

namespace ZipZap.Classes.Helpers;

public class Deffered<T> : IAsyncDisposable, IDisposable {
    private readonly T _defferedTarget;
    private readonly Action<T> _defferedSync;
    private readonly Func<T, Task> _defferedAsync;

    public Deffered(T defferedTarget,
            Action<T> defferedSync,
            Func<T, Task> defferedAsync) {
        _defferedTarget = defferedTarget;
        _defferedSync = defferedSync;
        _defferedAsync = defferedAsync;
    }

    public void Dispose() =>
        _defferedSync(_defferedTarget);

    public async ValueTask DisposeAsync() =>
        await _defferedAsync(_defferedTarget);
}
