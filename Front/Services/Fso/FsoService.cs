// FsoService.cs - Part of the ZipZap project for storing files online
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Handlers;
using ZipZap.LangExt.Extensions;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Services;

public class FsoService : IFsoService {

    public async Task<FsoStatus> GetFsoBySpecificationAsync(FileSpecification specification, IBackend backend, CancellationToken cancellationToken) {
        if (specification.Type == IdType.Path)
            specification = specification with { Identifier = specification.Identifier?.NormalizePath() };
        if (specification.Type == IdType.Id)
            return await GetFsoById(backend, specification.Identifier, cancellationToken);

        return FsoStatus.FromServiceResult(await backend.GetFsoByPathAsync(
            PathData.CreatePathDataWithPath(specification.Identifier),
            cancellationToken
        ));
    }
    public static async Task<FsoStatus> GetFsoById(IBackend backend, string? path, CancellationToken cancellationToken = default) {
        if (!Guid.TryParse(path, out var guid))
            return new FsoStatus.ParseError();
        return FsoStatus.FromServiceResult(await backend.GetFsoByIdAsync(guid.ToFsoId(), cancellationToken));
    }

    public Task<Result<IEnumerable<string>, ServiceError>> GetFullPath(FsoId id, IBackend backend, CancellationToken cancellationToken) {
        return backend.GetFullPath(id, cancellationToken);
    }

    public async Task<FsoStatus> GetFsoWithRoot(PathData path, FsoId anchor, IBackend backend, CancellationToken cancellationToken) {
        return FsoStatus.FromServiceResult(await backend.GetFsoWithRootAsync(path, anchor, cancellationToken));
    }
    public Task<Result<Unit, ServiceError>> Move(Fso id, string newPath, IBackend backend, CancellationToken cancellationToken) {
        var pathsplit = newPath.SplitPath().ToArray();
        var newDirname = pathsplit[..^1].ConcatenateWith("/");
        var newFileName = pathsplit[^1];
        return backend.GetFsoByPathAsync(new PathDataWithPath(newDirname), cancellationToken)
        .SelectAsync(param => {
            var data = id.Data;
            data = data with {
                Name = newFileName,
                VirtualLocation = param.Id
            };
            id = id with { Data = data };
            return id;
        })
        .SelectManyAsync(updated => backend.UpdateFso(updated, cancellationToken));

    }
}

