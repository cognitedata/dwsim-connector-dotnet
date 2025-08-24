/**
 * Copyright 2025 Cognite AS
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

namespace Connector.Tests.Mocks;

public class MockDwsimFlowsheetObject(string name, string productName, bool shouldThrowOnPropertyAccess = false)
{
    public string Name { get; set; } = name;
    public string ProductName { get; set; } = productName;
    private readonly Dictionary<string, object?> _properties = new();
    private readonly Dictionary<string, string> _units = new();
    private readonly HashSet<string> _writeableProperties = [];
    private readonly HashSet<string> _readableProperties = [];

    public void AddProperty(string propertyKey, object? value, string? unit = null, bool writeable = false)
    {
        _properties[propertyKey] = value;
        if (unit != null)
            _units[propertyKey] = unit;

        _readableProperties.Add(propertyKey);
        if (writeable)
            _writeableProperties.Add(propertyKey);
    }

    public IList GetProperties(int mode)
    {
        if (shouldThrowOnPropertyAccess)
            throw new InvalidOperationException("Simulated COM error getting properties");

        // mode 1 = write properties, mode 3 = read properties
        return mode == 1 ? _writeableProperties.ToList() : _readableProperties.ToList();
    }

    public object? GetPropertyValue(string propertyKey)
    {
        return shouldThrowOnPropertyAccess ? throw new InvalidOperationException($"Simulated COM error accessing property {propertyKey}") : _properties.GetValueOrDefault(propertyKey);
    }

    public string? GetPropertyUnit(string propertyKey)
    {
        return shouldThrowOnPropertyAccess ? throw new InvalidOperationException($"Simulated COM error getting unit for {propertyKey}") : _units.GetValueOrDefault(propertyKey);
    }
}

public class MockDwsimFlowsheet
{
    private readonly Dictionary<string, MockDwsimFlowsheetObject> _objects = new();

    public void AddObject(string name, MockDwsimFlowsheetObject obj)
    {
        _objects[name] = obj;
    }

    public MockDwsimFlowsheetObject? GetFlowsheetSimulationObject(string name)
    {
        return _objects.GetValueOrDefault(name);
    }
}

public class MockUnitSystem
{
    private readonly Dictionary<string, string> _unitTypes = new()
    {
        { "K", "temperature" },
        { "Pa", "pressure" },
        { "kg/s", "massflow" },
        { "kg/m3", "density" },
        { "kW", "power" },
        { "W", "power" },
        { "m", "length" },
        { "kg", "mass" },
        { "m/s", "velocity" },
        { "", "none" },
        { "unit", "dimensionless" }
    };

    public string GetUnitType(string unit)
    {
        return _unitTypes.GetValueOrDefault(unit, "none");
    }

    public override string ToString()
    {
        return "MockUnitSystem";
    }
}

public static class MockDwsimData
{
    public static readonly Dictionary<string, string> PropertyMap = new()
    {
        { "PROP_MS_0", "Temperature" },
        { "PROP_MS_1", "Pressure" },
        { "PROP_MS_2", "Mass Flow" },
        { "PROP_PH_0", "Enthalpy" }
    };
}
