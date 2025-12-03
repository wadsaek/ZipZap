using System.Threading;
using System.Threading.Tasks;

using ZipZap.Classes.Helpers;

namespace ZipZap.Front.Services;
public interface ILoginSerivce{
    public Task<Result<string,LoginError>> Login(string username, string password, CancellationToken cancellationToken = default);
}
