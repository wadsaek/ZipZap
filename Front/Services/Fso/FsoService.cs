using System;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Front.Factories;
using ZipZap.Front.Handlers;

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

}

