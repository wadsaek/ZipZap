using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Persistance.Data;
using ZipZap.Persistance.Models;
using ZipZap.Persistance.Repositories;

namespace ZipZap.Persistance;

public static class DI {
    extension(WebApplicationBuilder builder) {
        public WebApplicationBuilder AddPersistance(string connectionString) {
            builder.Services.AddSingleton<ExceptionConverter<DbError>>(new SimpleExceptionConverter<DbError>(err => new DbError()));
            builder.Services.AddScoped(typeof(BasicRepository<,,>));
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
