// SftpBackgroundService.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace ZipZap.Sftp;

internal static class TaskListExt {
    extension(List<Task> tasks) {
        public async Task<int> RemoveCompleted() {
            foreach (var a in tasks.Where(t => t.IsCompleted))
                await a;
            return tasks.RemoveAll(t => t.IsCompleted);
        }
    }
}
public class SftpBackgroundService : BackgroundService {
    private readonly ISftpConfiguration _configuration;
    private readonly ILogger<SftpBackgroundService> _logger;
    private readonly IServiceScopeFactory _factory;

    public SftpBackgroundService(ISftpConfiguration configuration, ILogger<SftpBackgroundService> logger, IServiceScopeFactory factory) {
        _configuration = configuration;
        _logger = logger;
        _factory = factory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken) {
        var listener = new TcpListener(IPAddress.Any, _configuration.Port);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("listening on port {Port}", _configuration.Port);
        var tasks = new List<Task>();
        listener.Start();
        while (!cancellationToken.IsCancellationRequested) {
            var socket = await listener.AcceptSocketAsync(cancellationToken);
            using var scope = _factory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<SftpService>();
            tasks.Add(service.HandleSocket(socket, cancellationToken));
            var removed = await tasks.RemoveCompleted();
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("{Count} tasks in queue\n{Removed} tasks removed", tasks.Count, removed);
        }
    }
}
