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

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Service;

public class Program
{
    public static int Main(string[] args)
    {
        return GetCommandLineOptions().InvokeAsync(args).Result;
    }
    private static Parser GetCommandLineOptions()
    {
        var rootCommand = new RootCommand();

        var flag = new Option<bool>("--service", "Required flag when starting the connector as a service");
        flag.AddAlias("-s");
        rootCommand.AddOption(flag);

        var dir = new Option<string>("--workdir", "Indicates the working directory to run the connector from");
        dir.AddAlias("-w");
        rootCommand.AddOption(dir);

        rootCommand.Handler = CommandHandler.Create((ConnectorParams setup) =>
        {
            if (setup.Service)
            {
                RunService(setup);
            }
            else
            {
                RunStandalone();
            }
        });

        return new CommandLineBuilder(rootCommand)
            .UseVersionOption()
            .UseHelp()
            .Build();
    }

    private static void RunStandalone()
    {
        Connector.ConnectorRuntime.RunStandalone().Wait();
    }

    private static void RunService(ConnectorParams setup)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton(setup);
                services.AddHostedService<Worker>();
            });
        builder = builder.ConfigureLogging(loggerFactory => loggerFactory.AddEventLog())
            .UseWindowsService(options => options.ServiceName = "DwsimConnector");
        builder.Build().Run();
    }
}

public class ConnectorParams
{
    public bool Service { get; set; }
    public string WorkDir { get; set; }
}
