// BackendFactory.cs - Part of the ZipZap project for storing files online
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
using ZipZap.Front.Services;
using ZipZap.Grpc;

namespace ZipZap.Front.Factories;

public class BackendFactory : IFactory<IBackend, BackendConfiguration> {
    private readonly FilesStoringService.FilesStoringServiceClient _client;
    private readonly ExceptionConverter<ServiceError> _exceptionConverter;

    public BackendFactory(FilesStoringService.FilesStoringServiceClient client, ExceptionConverter<ServiceError> exceptionConverter) {
        _client = client;
        _exceptionConverter = exceptionConverter;
    }

    public IBackend Create(BackendConfiguration configuration)
        => new Backend(_client, configuration, _exceptionConverter);
}

