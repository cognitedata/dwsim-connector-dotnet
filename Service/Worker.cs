/**
 * Copyright 2024 Cognite AS
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
 
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _eventLog;
    private readonly ConnectorParams _setup;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public Worker(IHostApplicationLifetime hostApplicationLifetime, ILogger<Worker> eventLog, ConnectorParams setup)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _eventLog = eventLog;
        _setup = setup;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _eventLog.LogInformation("Starting DWSIM connector service");

        try
        {
            // Move to the connector working directory
            var path = _setup.WorkDir;
            if (path == null)
            {
                path = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.FullName;
            }
            if (string.IsNullOrEmpty(path))
            {
                _eventLog.LogError("Invalid working directory");
                return;
            }
            if (!Directory.Exists(path))
            {
                _eventLog.LogError("Invalid working directory: {Message}", path);
                return;
            }
            Directory.SetCurrentDirectory(path);

            // Start the connector loop
            await Connector.ConnectorRuntime
                .Run(_eventLog, stoppingToken)
                .ConfigureAwait(false);
        }
        finally
        {
            // On exit, finalize the host application as well
            _eventLog.LogWarning("Exiting DWSIM connector service");
            _hostApplicationLifetime.StopApplication();
        }
    }
}
