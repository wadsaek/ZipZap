using System;
using System.Threading.Tasks;

namespace ZipZap.Classes.Helpers;

public class Deffered<T> : IAsyncDisposable, IDisposable {
    private readonly T _defferedTarget;
    private readonly Action<T> _defferedSync;
    private readonly Func<T, Task> _defferedAsync;

    public Deffered(T defferedTarget,
            Action<T> defferedSync,
            Func<T, Task> defferedAsync) {
        _defferedTarget = defferedTarget;
        _defferedSync = defferedSync;
        _defferedAsync = defferedAsync;
    }

    public void Dispose() =>
        _defferedSync(_defferedTarget);

    public async ValueTask DisposeAsync() =>
        await _defferedAsync(_defferedTarget);
}
