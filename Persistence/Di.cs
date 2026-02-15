// Di.cs - Part of the ZipZap project for storing files online
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

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Persistence.Data;
using ZipZap.Persistence.Models;
using ZipZap.Persistence.Repositories;

namespace ZipZap.Persistence;

public static class Di {
    extension(WebApplicationBuilder builder) {
        public WebApplicationBuilder AddPersistence(string connectionString) {
            builder.Services.AddSingleton<ExceptionConverter<DbError>>(new SimpleExceptionConverter<DbError>(_ => new DbError.Unknown()));
            builder.Services.AddScoped(typeof(IBasicRepository<,,>), typeof(BasicRepository<,,>));
            builder.Services.AddScoped<IFsosRepository, FsosRepository>();
            builder.Services.AddScoped<IUserRepository, UserReposirory>();
            builder.Services.AddScoped<EntityHelper<FsoInner, Fso, Guid>, FsoHelper>();
            builder.Services.AddScoped<EntityHelper<UserInner, User, Guid>, UserHelper>();
            builder.Services.AddNpgsqlDataSource(connectionString, builder => {
                builder.MapEnum<FsoType>();
                builder.MapEnum<UserRole>();
            });
            return builder;
        }
    }
}
