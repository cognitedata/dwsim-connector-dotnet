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

namespace Connector;

internal class DwsimRoutine : RoutineImplementationBase
{
    private readonly dynamic _interface;
    private readonly dynamic _model;
    private readonly Dictionary<string, string> _propMap;
    private readonly UnitConverter _units;


    public DwsimRoutine(
        SimulatorRoutineRevision routineRevision,
        dynamic model,
        dynamic interf,
        Dictionary<string, SimulatorValueItem> inputData,
        Dictionary<string, string> propMap,
        UnitConverter units) :
        base(routineRevision, inputData)
    {
        _model = model;
        _interface = interf;
        _propMap = propMap;
        _units = units;
    }

    public override void SetInput(SimulatorRoutineRevisionInput inputConfig, SimulatorValueItem input, Dictionary<string, string> arguments)
    {
        var (objectName, objectProperty) = GetObjectNameAndProperty(arguments);
        SetPropertyValue(objectProperty, objectName, input);
        input.SimulatorObjectReference = new Dictionary<string, string> {
            { "objectName", objectName },
            { "objectProperty", objectProperty },
        };
    }

    private SimulatorValue GetCompositionValue(dynamic outObj, SimulatorRoutineRevisionOutput outputConfig, string objectName)
    {
        if (outputConfig.ValueType != SimulatorValueType.DOUBLE_ARRAY)
        {
            throw new SimulationException($"Get error: Unsupported value type {outputConfig.ValueType} for property 'Composition'. Expected DOUBLE_ARRAY");
        }

        try {
            double[] composition = outObj.GetOverallComposition();
            return SimulatorValue.Create(composition);
        }
        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException e)
        {
            throw new SimulationException($"Get error: Unrecognized property 'Composition' for object {objectName} {e}");
        }
    }

    private SimulatorValue GetComponentsValue(dynamic outObj, SimulatorRoutineRevisionOutput outputConfig, string objectName)
    {
        if (outputConfig.ValueType != SimulatorValueType.STRING_ARRAY)
        {
            throw new SimulationException($"Get error: Unsupported value type {outputConfig.ValueType} for property 'Components'. Expected STRING_ARRAY");
        }

        try {
            string[] components = outObj.ComponentIds;
            return SimulatorValue.Create(components);
        }
        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException e)
        {
            throw new SimulationException($"Get error: Unrecognized property 'Components' for object {objectName} {e}");
        }
    }
        

    public override SimulatorValueItem GetOutput(SimulatorRoutineRevisionOutput outputConfig, Dictionary<string, string> arguments)
    {
        var (objectName, objectProperty) = GetObjectNameAndProperty(arguments);

        dynamic outObj = _model.GetFlowsheetSimulationObject(objectName);
        if (outObj == null)
        {
            throw new SimulationException($"Get error: Cannot find flowsheet object named '{objectName}'");
        }

        var resultItem = new SimulatorValueItem()
        {
            SimulatorObjectReference = new Dictionary<string, string> {
                { "objectName", objectName },
                { "objectProperty", objectProperty },
            },
            TimeseriesExternalId = outputConfig.SaveTimeseriesExternalId,
            ReferenceId = outputConfig.ReferenceId,
            ValueType = outputConfig.ValueType,
        };

        // Special logic for "Composition" property
        if (objectProperty == "Composition")
        {
            resultItem.Value = GetCompositionValue(outObj, outputConfig, objectName);;
            return resultItem;
        }

        // Special logic for "Components" property
        if (objectProperty == "Components")
        {
            resultItem.Value = GetComponentsValue(outObj, outputConfig, objectName);
            return resultItem;
        }

        // Properties that we can read from
        string[] outObjProps = outObj.GetProperties(3);
        string propKey = objectProperty;

        // Some properties have different names in the UI and in the simulation model
        if (!outObjProps.Contains(objectProperty))
        {
            // translate from UI label to property ID.
            var props = _propMap.Where(kvp => kvp.Value == objectProperty && outObjProps.Contains(kvp.Key));
            if (!props.Any())
            {
                throw new SimulationException($"Get error: Unrecognized property '{objectProperty}'");
            }
            propKey = props.First().Key;
        }
        var value = outObj.GetPropertyValue(propKey);
        SimulatorValue resultValue;
        SimulatorValueUnit? outputUnit = null;
        if (outputConfig.ValueType == SimulatorValueType.DOUBLE && value is double)
        {
            if (outputConfig.Unit?.Name != null)
            {
                double result = _units.ConvertFromSI(outputConfig.Unit.Name, value);
                resultValue = SimulatorValue.Create(result);
                outputUnit = new SimulatorValueUnit()
                {
                    Name = outputConfig.Unit?.Name,
                };
            }
            else
            {
                resultValue = SimulatorValue.Create(value);
            }
        }
        else if (outputConfig.ValueType == SimulatorValueType.STRING)
        {
            resultValue = SimulatorValue.Create(value.ToString());
        }
        else
        {
            throw new SimulationException($"Get error: Unsupported value type {outputConfig.ValueType} for property {objectProperty} of object {objectName} with value {value}");
        }

        resultItem.Value = resultValue;
        resultItem.Unit = outputUnit;
        return resultItem;
    }

    public override void RunCommand(Dictionary<string, string> arguments)
    {
        if (!arguments.TryGetValue("command", out string? command))
        {
            throw new SimulationException($"Command error: Command not defined");
        }
        if (command == "Solve")
        {
            _interface.CalculateFlowsheet2(_model);
            if (!_model.Solved)
            {
                throw new SimulationException($"Command error: {_model.ErrorMessage}");
            }
        }
        else
        {
            throw new SimulationException($"Command error: Invalid command type {command}");
        }
    }

    private void SetCompositionValue(dynamic inObj, SimulatorValueItem valueItem, string objectName)
    {
        if (valueItem.ValueType == SimulatorValueType.DOUBLE_ARRAY)
        {
            var arrValue = ((SimulatorValue.DoubleArray) valueItem.Value)?.Value;
            try
            {
                inObj.SetOverallMolarComposition(arrValue?.ToArray());
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException e)
            {
                throw new SimulationException($"Set error: Unrecognized property 'Composition' for object {objectName} {e}");
            }
        }
        else
        {
            throw new SimulationException($"Set error: Unsupported value type {valueItem.ValueType} for property 'Composition'");
        }
    }

    private void SetPropertyValue(string propertyName, string objectName, SimulatorValueItem valueItem)
    {
        dynamic inObj = _model.GetFlowsheetSimulationObject(objectName);
        if (inObj == null)
        {
            throw new SimulationException($"Set error: Cannot find flowsheet object named '{objectName}'");
        }

        // Special logic for "Composition" property
        if (propertyName == "Composition")
        {
            SetCompositionValue(inObj, valueItem, objectName);
            return;
        }

        // Properties that we can write to
        string[] inObjProps = inObj.GetProperties(1);
        string propKey = propertyName;
        if (!inObjProps.Contains(propertyName))
        {
            // translate from UI label to property ID.
            var props = _propMap.Where(kvp => kvp.Value == propertyName && inObjProps.Contains(kvp.Key));
            if (!props.Any())
            {
                throw new SimulationException($"Set error: Unrecognized property '{propertyName}' for object '{objectName}'");
            }
            propKey = props.First().Key;
        }
        bool success = false;

        if (valueItem.ValueType == SimulatorValueType.DOUBLE)
        {
            var rawValue = ((SimulatorValue.Double) valueItem.Value).Value;
            double value = rawValue;
            if (valueItem.Unit?.Name != null)
            {
                value = _units.ConvertToSI(valueItem.Unit.Name, rawValue);
            }
            success = inObj.SetPropertyValue(propKey, value);
        }
        else if (valueItem.ValueType == SimulatorValueType.STRING)
        {
            var rawValue = ((SimulatorValue.String) valueItem.Value).Value;
            success = inObj.SetPropertyValue(propKey, rawValue);
        }
        if (!success)
        {
            throw new SimulationException($"Set error: Failed to set property {propertyName} ({propKey}) to {valueItem} unit: {valueItem?.Unit?.Name}");
        }
    }

    private (string Name, string Property) GetObjectNameAndProperty(Dictionary<string, string> arguments)
    {
        if (!arguments.TryGetValue("objectProperty", out string objectProperty))
        {
            throw new SimulationException($"Set error: Object property not defined");
        }
        if (!arguments.TryGetValue("objectName", out string objectName))
        {
            throw new SimulationException($"Set error: Object name not defined");
        }

        return (objectName, objectProperty);
    }
}