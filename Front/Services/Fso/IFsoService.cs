// IFsoService.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Front.Handlers;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Services;

public interface IFsoService {
    public Task<FsoStatus> GetFsoBySpecificationAsync(FileSpecification specification, BackendConfiguration backendConfiguration, CancellationToken cancellationToken);
    public Task<Result<IEnumerable<string>, ServiceError>> GetFullPath(FsoId fsoId, BackendConfiguration backendConfiguration, CancellationToken cancellationToken);
    public Task<FsoStatus> GetFsoWithRoot(PathData path, FsoId anchor, BackendConfiguration backendConfiguration, CancellationToken cancellationToken);
}
