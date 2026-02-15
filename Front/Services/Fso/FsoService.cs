// FsoService.cs - Part of the ZipZap project for storing files online
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Front.Factories;
using ZipZap.Front.Handlers;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Services;

public class FsoService : IFsoService {
    private readonly IFactory<IBackend, BackendConfiguration> _factory;

    public FsoService(IFactory<IBackend, BackendConfiguration> factory) {
        _factory = factory;
    }

    public async Task<FsoStatus> GetFsoBySpecificationAsync(FileSpecification specification, BackendConfiguration backendConfiguration, CancellationToken cancellationToken) {
        if (specification.Type == IdType.Path)
            specification = specification with { Identifier = specification.Identifier?.NormalizePath() };
        var backend = _factory.Create(backendConfiguration);
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

    public Task<Result<IEnumerable<string>, ServiceError>> GetFullPath(FsoId id, BackendConfiguration backendConfiguration, CancellationToken cancellationToken) {
        return _factory.Create(backendConfiguration).GetFullPath(id, cancellationToken);
    }

    public async Task<FsoStatus> GetFsoWithRoot(PathData path, FsoId anchor, BackendConfiguration backendConfiguration, CancellationToken cancellationToken) {
        return FsoStatus.FromServiceResult(await _factory.Create(backendConfiguration).GetFsoWithRootAsync(path, anchor, cancellationToken));
    }
}

