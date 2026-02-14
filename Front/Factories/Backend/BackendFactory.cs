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

