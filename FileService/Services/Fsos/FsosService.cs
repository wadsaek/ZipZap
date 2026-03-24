// FsosService.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes.Helpers;
using ZipZap.FileService.Extensions;
using ZipZap.FileService.Helpers;
using ZipZap.Persistence.Models;
using ZipZap.Persistence.Repositories;

namespace ZipZap.FileService.Services;

public interface IFsosService {
    Task<Result<Unit, DbError>> RemoveFso(FsoId fso, DeleteOptions options, CancellationToken cancellationToken);
    Task<Result<int, DbError>> RemoveFsoRange(IEnumerable<FsoId> fsos, CancellationToken cancellationToken);
}

public class FsosService : IFsosService {
    private readonly IFsosRepository _fsosRepo;
    private readonly IIO _io;

    public FsosService(IFsosRepository fsosRepo, IIO io) {
        _fsosRepo = fsosRepo;
        _io = io;
    }

    public async Task<Result<Unit, DbError>> RemoveFso(FsoId fso, DeleteOptions options, CancellationToken cancellationToken) {
        if (options is DeleteOptions.All or DeleteOptions.AllExceptDirectories) {
            var willBeDeleted = await _fsosRepo.GetAllChildFilesAsync(fso, cancellationToken);
            var paths = willBeDeleted.Select(f => f.PhysicalPath);
            await _io.RemoveRangeAsync(paths);
        }
        return await _fsosRepo.DeleteAsync(fso, options, cancellationToken);
    }

    public async Task<Result<int, DbError>> RemoveFsoRange(IEnumerable<FsoId> fsos, CancellationToken cancellationToken) {
        List<File> willBeDeleted = [];
        foreach (var fso in fsos) {
            var thisFile = await _fsosRepo.GetAllChildFilesAsync(fso, cancellationToken);
            willBeDeleted.AddRange(thisFile);
        }
        var paths = willBeDeleted.Select(f => f.PhysicalPath).Distinct();
        await _io.RemoveRangeAsync(paths);
        return await _fsosRepo.DeleteRangeAsync(fsos, cancellationToken);
    }
}
