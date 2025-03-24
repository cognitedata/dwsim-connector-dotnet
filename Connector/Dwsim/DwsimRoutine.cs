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
using Microsoft.Extensions.Logging;

namespace Connector.Dwsim;

internal class DwsimRoutine : RoutineImplementationBase
{
    private readonly dynamic _interface;
    private readonly dynamic _model;
    private readonly Dictionary<string, string> _propMap;
    private readonly UnitConverter _units;
    private readonly ILogger<DwsimClient> _logger;

    public DwsimRoutine(
        SimulatorRoutineRevision routineRevision,
        dynamic model,
        dynamic interf,
        Dictionary<string, SimulatorValueItem> inputData,
        Dictionary<string, string> propMap,
        UnitConverter units,
        ILogger<DwsimClient> logger) :
        base(routineRevision, inputData, logger)
    {
        _model = model;
        _interface = interf;
        _propMap = propMap;
        _units = units;
        _logger = logger;
    }

    private (string Name, string Property) GetObjectNameAndProperty(Dictionary<string, string> arguments)
    {
        if (!arguments.TryGetValue("objectProperty", out string objectProperty))
        {
            throw new SimulationException("Error: Object property not defined");
        }

        if (!arguments.TryGetValue("objectName", out string objectName))
        {
            throw new SimulationException("Error: Object name not defined");
        }

        return (objectName, objectProperty);
    }

    public override SimulatorValueItem GetOutput(SimulatorRoutineRevisionOutput outputConfig,
        Dictionary<string, string> arguments)
    {
        var (objectName, objectProperty) = GetObjectNameAndProperty(arguments);

        SimulatorValue resValue =
            GetProperty(_model, objectName, objectProperty, outputConfig, _propMap, _units, _logger);
        var simulatorObjectReference = new Dictionary<string, string>()
        {
            { "objectName", objectName },
            { "objectProperty", objectProperty }
        };
        var outputUnitName = outputConfig.Unit?.Name;
        var outputUnit = outputUnitName != null && resValue.Type == SimulatorValueType.DOUBLE
            ? new SimulatorValueUnit()
            {
                Name = outputUnitName,
            }
            : null;
        return new SimulatorValueItem()
        {
            ReferenceId = outputConfig.ReferenceId,
            SimulatorObjectReference = simulatorObjectReference,
            TimeseriesExternalId = outputConfig.SaveTimeseriesExternalId,
            ValueType = resValue.Type,
            Value = resValue,
            Unit = outputUnit
        };
    }

    public override void SetInput(SimulatorRoutineRevisionInput inputConfig, SimulatorValueItem input,
        Dictionary<string, string> arguments)
    {
        var (objectName, objectProperty) = GetObjectNameAndProperty(arguments);
        var simulatorObjectReference = new Dictionary<string, string>()
        {
            { "objectName", objectName },
            { "objectProperty", objectProperty }
        };
        input.SimulatorObjectReference = simulatorObjectReference;

        SetProperty(_model, objectName, objectProperty, input, _propMap, _units, _logger);
    }

    public override void RunCommand(Dictionary<string, string> arguments)
    {
        if (!arguments.TryGetValue("command", out string? command))
        {
            throw new SimulationException("Command error: Command not defined");
        }

        _logger.LogDebug($"Run Command : {command} ");

        switch (command)
        {
            case "Solve":
                {
                    _logger.LogDebug("Running the solver");
                    _interface.CalculateFlowsheet2(_model);
                    if (!_model.Solved)
                    {
                        throw new SimulationException($"Command error: {_model.ErrorMessage}");
                    }

                    break;
                }
            default:
                throw new NotImplementedException($"Unsupported command: '{command}'");
        }
    }

    private static SimulatorValue GetProperty(
        dynamic model,
        string objectName,
        string objectProperty,
        SimulatorRoutineRevisionOutput outputConfig,
        Dictionary<string, string> propMap,
        UnitConverter unitConverter,
        ILogger<DwsimClient> logger)
    {
        var unitName = outputConfig.Unit?.Name;

        // get a reference to the object by name
        var objectRef = GetDwsimObjectFromObjectNameAndProperty(model, objectName, outputConfig.ReferenceId);

        // Special logic for "Composition" property
        if (objectProperty == "Composition")
        {
            if (outputConfig.ValueType != SimulatorValueType.DOUBLE_ARRAY)
            {
                throw new SimulationException(
                    $"Unsupported value type {outputConfig.ValueType} for property 'Composition'. Expected DOUBLE_ARRAY. ReferenceId = '{outputConfig.ReferenceId}'");
            }

            // composition is a unitless property, so we should throw an error if a unit is specified
            if (unitName != null)
            {
                throw new SimulationException(
                    $"Unsupported unit '{unitName}' for property 'Composition'. ReferenceId = '{outputConfig.ReferenceId}'");
            }

            // only objects implementing the IMaterialStream interface have the 'GetOverallComposition' method
            try
            {
                double[] composition = objectRef.GetOverallComposition();
                return SimulatorValue.Create(composition);
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException e)
            {
                throw new SimulationException(
                    $"Unrecognized property 'Composition' for object {objectName} {e}. ReferenceId = '{outputConfig.ReferenceId}'");
            }
        }

        // Special logic for "Components" property
        if (objectProperty == "Components")
        {
            if (outputConfig.ValueType != SimulatorValueType.STRING_ARRAY)
            {
                throw new SimulationException(
                    $"Unsupported value type {outputConfig.ValueType} for property 'Components'. Expected STRING_ARRAY. ReferenceId = '{outputConfig.ReferenceId}'");
            }

            // only objects implementing the IMaterialStream interface have the 'ComponentIds' property
            try
            {
                string[] components = objectRef.ComponentIds;
                return SimulatorValue.Create(components);
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException e)
            {
                throw new SimulationException(
                    $"Unrecognized property 'Components' for object {objectName} {e}. ReferenceId = '{outputConfig.ReferenceId}'");
            }
        }

        // Properties that we can read from the selected object
        string[] objectRefProperties = objectRef.GetProperties(3);
        var propKey = GetPropertyKey(objectName, objectProperty, objectRefProperties, propMap,
            outputConfig.ReferenceId);

        var value = objectRef.GetPropertyValue(propKey);
        switch (value)
        {
            // detected as DOUBLE
            case double:
                {
                    logger.LogDebug($"ReferenceId = '{outputConfig.ReferenceId}' detected as 'DOUBLE'");
                    if (outputConfig.ValueType != SimulatorValueType.DOUBLE)
                    {
                        throw new DwsimException(
                            "Value type mismatch. Expected '" + outputConfig.ValueType +
                            "' but received 'DOUBLE'. ReferenceId = '" + outputConfig.ReferenceId + "'.", canRetry: false);
                    }

                    var numericValue = unitName is null
                        ? value
                        : unitConverter.ConvertFromSI(unitName, value);
                    return new SimulatorValue.Double(numericValue);
                }
            // detected as STRING
            case string:
                {
                    logger.LogDebug($"ReferenceId = '{outputConfig.ReferenceId}' detected as 'STRING'");
                    if (outputConfig.ValueType != SimulatorValueType.STRING)
                    {
                        throw new DwsimException(
                            "Value type mismatch. Expected '" + outputConfig.ValueType +
                            "' but received 'STRING'. ReferenceId = '" + outputConfig.ReferenceId + "'.", canRetry: false);
                    }

                    return new SimulatorValue.String(value);
                }
            // Besides for 'Composition' and 'Components', we only support DOUBLE and STRING types for now
            default:
                throw new NotImplementedException(
                    $"Cannot read value type '{value.GetType()}' from the property '{objectProperty}' of object '{objectName}'. Given value type is not supported. ReferenceId = '{outputConfig.ReferenceId}'");
        }
    }

    private static void SetProperty(
        dynamic model,
        string objectName,
        string objectProperty,
        SimulatorValueItem inputItem,
        Dictionary<string, string> propMap,
        UnitConverter unitConverter,
        ILogger<DwsimClient> logger)
    {
        var unitName = inputItem.Unit?.Name;
        var wrappedValue = inputItem.Value;

        // get a reference to the object by name
        var objectRef = GetDwsimObjectFromObjectNameAndProperty(model, objectName, inputItem.ReferenceId);

        // Special logic for "Composition" property
        if (objectProperty == "Composition")
        {
            if (inputItem.ValueType == SimulatorValueType.DOUBLE_ARRAY)
            {
                var doubleList = ((SimulatorValue.DoubleArray)wrappedValue).Value;

                // composition is a unitless property, so we should throw an error if a unit is specified
                if (unitName != null)
                {
                    throw new SimulationException(
                        $"Unsupported unit '{unitName}' for property 'Composition'. ReferenceId = '{inputItem.ReferenceId}'");
                }

                // only objects implementing the IMaterialStream interface have the 'SetOverallMolarComposition' method
                try
                {
                    objectRef.SetOverallMolarComposition(doubleList?.ToArray());
                }
                catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException e)
                {
                    throw new SimulationException(
                        $"Failed to update 'Composition' for object '{objectName}' {e}. ReferenceId = '{inputItem.ReferenceId}'");
                }
            }
            else
            {
                throw new SimulationException(
                    $"Unsupported value type '{inputItem.ValueType}' for property 'Composition'. ReferenceId = '{inputItem.ReferenceId}'");
            }

            return;
        }

        // Properties that we can write to
        string[] inObjProps = objectRef.GetProperties(1);
        var propKey = GetPropertyKey(objectName, objectProperty, inObjProps, propMap, inputItem.ReferenceId);

        bool success;
        var currentValue = objectRef.GetPropertyValue(propKey);
        switch (currentValue)
        {
            // detected as DOUBLE
            case double:
                {
                    logger.LogDebug($"ReferenceId = '{inputItem.ReferenceId}' detected as 'DOUBLE'");
                    if (inputItem.ValueType != SimulatorValueType.DOUBLE)
                    {
                        throw new DwsimException(
                            "Value type mismatch. Expected '" + inputItem.ValueType +
                            "' but the target is 'DOUBLE'. ReferenceId = '" + inputItem.ReferenceId + "'.",
                            canRetry: false);
                    }

                    var rawValue = ((SimulatorValue.Double)wrappedValue).Value;
                    var value = unitName is null
                        ? rawValue
                        : unitConverter.ConvertToSI(unitName, rawValue);

                    success = objectRef.SetPropertyValue(propKey, value);
                    break;
                }
            // detected as STRING
            case string:
                {
                    logger.LogDebug($"ReferenceId = '{inputItem.ReferenceId}' detected as 'STRING'");
                    if (inputItem.ValueType != SimulatorValueType.STRING)
                    {
                        throw new DwsimException(
                            "Value type mismatch. Expected '" + inputItem.ValueType +
                            "' but the target is 'STRING'. ReferenceId = '" + inputItem.ReferenceId + "'.",
                            canRetry: false);
                    }

                    var value = ((SimulatorValue.String)wrappedValue).Value;
                    success = objectRef.SetPropertyValue(propKey, value);
                    break;
                }
            // Besides for 'Composition', we only support writing to DOUBLE and STRING types for now
            default:
                throw new NotImplementedException(
                    $"Cannot assign value type '{wrappedValue.Type}' to the property '{objectProperty}' of object '{objectName}'. Given value type is not supported. ReferenceId = '{inputItem.ReferenceId}'");
        }

        if (!success)
        {
            throw new SimulationException(
                $"Cannot assign value to the property '{objectProperty}' for object '{objectName}'. ReferenceId = '{inputItem.ReferenceId}'");
        }
    }

    private static dynamic GetDwsimObjectFromObjectNameAndProperty(dynamic model, string objectName, string referenceId)
    {
        // get the object by name
        var obj = model.GetFlowsheetSimulationObject(objectName);
        if (obj == null)
        {
            throw new SimulationException($"Flowsheet object not found: '{objectName}'. ReferenceId = '{referenceId}'");
        }

        return obj;
    }

    private static string GetPropertyKey(string objectName, string objectProperty, string[] objectRefProperties,
        Dictionary<string, string> propMap, string referenceId)
    {
        // Some properties have different names in the UI and in the simulation model
        if (objectRefProperties.Contains(objectProperty)) return objectProperty;

        // translate from UI label to property ID.
        var props = propMap.Where(
            kvp => kvp.Value == objectProperty && objectRefProperties.Contains(kvp.Key));
        if (!props.Any())
        {
            throw new SimulationException(
                $"Unrecognized property '{objectProperty}' for object '{objectName}'. ReferenceId = '{referenceId}'");
        }

        return props.First().Key;
    }
}