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

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using Cognite.Simulator.Utils;
using Cognite.Simulator.Utils.Automation;
using CogniteSdk.Alpha;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Connector.Dwsim
{
    public class DwsimClient :
        AutomationClient,
        ISimulatorClient<DefaultModelFilestate, SimulatorRoutineRevision>
    {
        private readonly ILogger<DwsimClient> _logger;
        public string Version { get; init; }
        private readonly Dictionary<string, string> _propMap = new Dictionary<string, string>();
        private readonly UnitConverter _unitConverter;
        private readonly string _dwsimInstallationPath;

        // Lock to prevent concurrent access to simulator resources
        private readonly object _simulatorLock = new object();

        public DwsimClient(
            ILogger<DwsimClient> logger, DefaultConfig<DwsimAutomationConfig> config)
            : base(logger, config.Automation)
        {
            _logger = logger;
            _dwsimInstallationPath = config.Automation.DwsimInstallationPath ?? throw new DwsimException("DWSIM installation path is not set");
            string dllPath = Path.Combine(_dwsimInstallationPath, "DWSIM.Automation.dll");
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(dllPath);
            Version = fvi.FileVersion != null ? fvi.FileVersion : "N/A";

            string propsDll = Path.Combine(_dwsimInstallationPath, "DWSIM.FlowsheetBase.dll");
            Assembly assembly = Assembly.LoadFrom(propsDll);
            ResourceManager rm = new ResourceManager("DWSIM.FlowsheetBase.Properties", assembly);
            var resourceSet = rm.GetResourceSet(CultureInfo.InvariantCulture, true, false);
            if (resourceSet != null)
            {
                foreach (DictionaryEntry entry in resourceSet)
                {
                    var key = entry.Key?.ToString();
                    var value = entry.Value?.ToString();
                    if (key != null && value != null)
                    {
                        _propMap[key] = value;
                    }
                }
            }

            _unitConverter = new UnitConverter(_dwsimInstallationPath);
        }

        protected override void PreShutdown()
        {
            if (Server != null)
            {
                Server.ReleaseResources(); // Clean up used DWSIM resources
                _logger.LogInformation("DWSIM resources released");
            }
        }

        public Task TestConnection(CancellationToken _token)
        {
            _logger.LogInformation("Testing DWSIM connection...");

            lock (_simulatorLock)
            {
                try
                {
                    Initialize();
                    Shutdown();
                }
                catch (Exception e)
                {
                    throw new SimulatorConnectionException($"DWSIM is not available: {e.Message}", e);
                }
            }
            _logger.LogInformation("Connection to DWSIM established and removed successfully");
            return Task.CompletedTask;
        }

        public bool CanOpenModel(string path)
        {
            _logger.LogDebug("Attempting to open file {Path}", path);
            lock (_simulatorLock)
            {
                try
                {
                    Initialize();
                    return OpenModel(path) != null;
                }
                catch (Exception e) when (e is COMException || e is SystemException || e is FileNotFoundException)
                {
                    // Assuming that these are the cases that cannot be retried
                    throw new DwsimException(e.Message, false);
                }
                finally
                {
                    Shutdown();
                }
            }
        }

        public dynamic OpenModel(string path)
        {
            string normalizedPath = Path.GetFullPath(path);
            return Server.LoadFlowsheet(normalizedPath);
        }

        public Task<Dictionary<string, SimulatorValueItem>> RunSimulation(DefaultModelFilestate modelState, SimulatorRoutineRevision routineRev, Dictionary<string, SimulatorValueItem> inputData, CancellationToken token)
        {
            _logger.LogDebug($"- Started running {routineRev.ExternalId} in DWSIM");
            lock (_simulatorLock)
            {
                try
                {
                    Initialize();
                    var model = OpenModel(modelState.FilePath);
                    _logger.LogDebug("- Model revision {ExternalId} open in DWSIM", modelState.ExternalId);

                    Dictionary<string, SimulatorValueItem> result = new Dictionary<string, SimulatorValueItem>();

                    var routine = new DwsimRoutine(routineRev, model, Server, inputData, _propMap, _unitConverter, _logger);

                    result = routine.PerformSimulation(token);
                    return Task.FromResult(result);
                }
                finally
                {
                    Shutdown();
                }
            }
        }

        /// <summary>
        /// Extracts model information as SimulatorModelRevisionDataFlowsheet
        /// </summary>
        /// <param name="modelFile">The model file to parse</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Parsed flowsheet or null if parsing fails</returns>
        private SimulatorModelRevisionDataFlowsheet? ExtractFlowsheet(DefaultModelFilestate modelFile, CancellationToken token = default)
        {
            _logger.LogDebug("Opening model revision {ExternalId} for model parsing", modelFile.ExternalId);
            lock (_simulatorLock)
            {
                try
                {
                    Initialize();
                    dynamic model = OpenModel(modelFile.FilePath);

                    var modelParser = new DwsimModelParser(_logger, _propMap, _dwsimInstallationPath);
                    dynamic? flowsheet = modelParser.Parse(model, modelFile.FilePath, token);

                    if (flowsheet == null)
                    {
                        _logger.LogWarning("Model parsing failed for {ExternalId}", modelFile.ExternalId);
                    }

                    // DEBUG: Save flowsheet as JSON for troubleshooting
                    try
                    {
                        string modelDirectory = Path.GetDirectoryName(modelFile.FilePath) ?? "";
                        string modelNameWithoutExt = Path.GetFileNameWithoutExtension(modelFile.FilePath);
                        string jsonFilePath = Path.Combine(modelDirectory, $"{modelNameWithoutExt}_flowsheet_debug.json");

                        var settings = new JsonSerializerSettings
                        {
                            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
                            Formatting = Formatting.Indented
                        };
                        string jsonContent = JsonConvert.SerializeObject(flowsheet, settings);
                        File.WriteAllText(jsonFilePath, jsonContent);

                        _logger.LogInformation("DEBUG: Saved flowsheet JSON to {FilePath}", jsonFilePath);
                    }
                    catch (Exception jsonEx)
                    {
                        _logger.LogWarning(jsonEx, "DEBUG: Failed to save flowsheet JSON for troubleshooting");
                    }

                    return flowsheet;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error while parsing DWSIM model {ExternalId}: {Error}", modelFile.ExternalId, e.Message);
                    throw;
                }
                finally
                {
                    Shutdown();
                }
            }
        }

        public Task ExtractModelInformation(DefaultModelFilestate state, CancellationToken token)
        {
            try
            {
                _logger.LogInformation("Opening model {ExternalId}", state.ExternalId);
                SimulatorModelRevisionDataFlowsheet? flowsheet = ExtractFlowsheet(state, token);

                if (flowsheet != null)
                {
                    state.ParsingInfo.Flowsheet = flowsheet;
                    state.ParsingInfo.RevisionDataInfo = [];
                }

                state.CanRead = true;
                state.ParsingInfo.SetSuccess();
                _logger.LogInformation("Successfully parsed model revision {ExternalId} in DWSIM", state.ExternalId);
            }
            catch (DwsimException de) when (!de.CanRetry)
            {
                _logger.LogError("DWSIM error while opening model {Name}: {Error}", state.ExternalId, de.Message);
                state.ParsingInfo.SetFailure(de.Message);
                state.CanRead = false; // This file cannot be parsed. Will not retry
            }
            catch (Exception e)
            {
                _logger.LogWarning("Couldn't open the model {ExternalId}, will retry: {Error}", state.ExternalId, e.Message);
                state.ParsingInfo.SetFailure();
                state.CanRead = false;
            }
            return Task.CompletedTask;
        }

        public string GetSimulatorVersion(CancellationToken _token)
        {
            return Version;
        }

        public string GetConnectorVersion(CancellationToken _token)
        {
            return Cognite.Extractor.Metrics.Version.GetVersion(
                Assembly.GetExecutingAssembly(),
                "0.0.1");
        }
    }

    public class UnitConverter
    {
        private readonly Assembly _assembly;
        private readonly Type _type;
        public UnitConverter(string installationPath)
        {
            string dllPath = Path.Combine(installationPath, "DWSIM.SharedClasses.dll");
            _assembly = Assembly.LoadFrom(dllPath);
            if (_assembly == null)
            {
                throw new DwsimException("Failed to load System of Units assembly");
            }
            else
            {
                var assemblyType = _assembly.GetType("DWSIM.SharedClasses.SystemsOfUnits.Converter");
                if (assemblyType == null)
                {
                    throw new DwsimException("Cannot find DWSIM Unit Converter class type");
                }
                _type = assemblyType;
            }
        }

        public double ConvertFromSI(string unit, double value)
        {
            return InvokeConversion("ConvertFromSI", unit, value);
        }

        public double ConvertToSI(string unit, double value)
        {
            return InvokeConversion("ConvertToSI", unit, value);
        }

        public double InvokeConversion(string method, string unit, double value)
        {
            var info = _type.GetMethod(method);
            if (info == null)
            {
                throw new DwsimException("Cannot find unit conversion method in the DWSIM class type");
            }
            var res = info.Invoke(null, new object[] { unit, value });
            if (res == null)
            {
                throw new DwsimException("Failed to invoke unit convertion method");
            }
            return (double)res;
        }
    }

    public class DwsimException : Exception
    {
        public bool CanRetry { get; }
        public DwsimException(string message, bool canRetry = true) : base(message)
        {
            CanRetry = canRetry;
        }

    }

    public class SimulatorConnectionException : Exception
    {
        public SimulatorConnectionException()
        {
        }

        public SimulatorConnectionException(string message) : base(message)
        {
        }

        public SimulatorConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
