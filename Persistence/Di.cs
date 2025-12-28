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
            });
            return builder;
        }
    }
}
