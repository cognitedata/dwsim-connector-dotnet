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

using Cognite.Simulator.Utils;
using CogniteSdk.Alpha;
using Connector.Dwsim;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Connector;

public static class ConnectorRuntime
{

    public static void Init()
    {
        DefaultConnectorRuntime<DwsimAutomationConfig, DefaultModelFilestate, DefaultModelFileStatePoco>.ConfigureServices = ConfigureServices;
        DefaultConnectorRuntime<DwsimAutomationConfig, DefaultModelFilestate, DefaultModelFileStatePoco>.ConnectorName = "DWSIM";
        DefaultConnectorRuntime<DwsimAutomationConfig, DefaultModelFilestate, DefaultModelFileStatePoco>.SimulatorDefinition = SimulatorDefinition.Get();
    }
    static void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISimulatorClient<DefaultModelFilestate, SimulatorRoutineRevision>, DwsimClient>();
    }

    public static async Task RunStandalone()
    {
        Init();
        await DefaultConnectorRuntime<DwsimAutomationConfig, DefaultModelFilestate, DefaultModelFileStatePoco>.RunStandalone().ConfigureAwait(false);
    }

    public static async Task Run(ILogger defaultLogger, CancellationToken token)
    {
        Init();
        await DefaultConnectorRuntime<DwsimAutomationConfig, DefaultModelFilestate, DefaultModelFileStatePoco>.Run(defaultLogger, token).ConfigureAwait(false);
    }
}

