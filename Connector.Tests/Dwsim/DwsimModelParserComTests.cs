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

using System.Reflection;
using CogniteSdk.Alpha;
using Connector.Dwsim;
using Microsoft.Extensions.Logging;
using Moq;

namespace Connector.Tests.Dwsim;

/// <summary>
/// Tests for COM property extraction methods in DwsimModelParser.
/// Uses DispatchProxy to create dynamic mock COM objects for testing.
/// </summary>
public class DwsimModelParserComTests
{
    private readonly Mock<ILogger<DwsimClient>> _mockLogger;
    private readonly Dictionary<string, string> _propMap;
    private readonly TestableComParser _parser;

    public DwsimModelParserComTests()
    {
        _mockLogger = new Mock<ILogger<DwsimClient>>();
        _propMap = new Dictionary<string, string>
        {
            { "PROP_MS_0", "Temperature" },
            { "PROP_MS_2", "Mass Flow" },
            { "PROP_MS_105", "Mass Flow (Mixture)" }
        };
        _parser = new TestableComParser(_mockLogger.Object, _propMap);
    }

    [Fact]
    public void ExtractNodePropertiesFromCom_WithValidProperties_ShouldReturnProperties()
    {
        // Arrange
        var mockCom = MockDwsimProxy.Create(
            writeProps: ["Temperature", "Pressure"],
            readProps: ["Temperature", "Pressure", "Density"],
            values: new() { { "Temperature", 25.5 }, { "Pressure", 101325.0 }, { "Density", 1000.0 } },
            units: new() { { "Temperature", "C" }, { "Pressure", "Pa" }, { "Density", "kg/m3" } });

        // Act
        List<SimulatorModelRevisionDataProperty> properties = _parser.ExtractNodePropertiesFromComPublic(mockCom, "MaterialStream", "S-01");

        // Assert
        Assert.Equal(3, properties.Count);
        Assert.Contains(properties, p => p.Name == "Temperature" && p.ReadOnly == false);
        Assert.Contains(properties, p => p.Name == "Density" && p.ReadOnly == true);
    }

    [Theory]
    [InlineData(double.NaN, true)]
    [InlineData(double.PositiveInfinity, true)]
    [InlineData(double.NegativeInfinity, true)]
    [InlineData(100.5, false)]
    public void CreateModelProperty_WithDoubleValues_ShouldHandleValidAndInvalid(double value, bool shouldBeNull)
    {
        // Arrange
        var mockCom = MockDwsimProxy.Create(
            writeProps: ["TestProp"],
            readProps: ["TestProp"],
            values: new() { { "TestProp", value } },
            units: new() { { "TestProp", "kg/h" } });

        // Act
        var property = _parser.CreateModelPropertyPublic("TestProp", mockCom, "Stream", "S-01", new[] { "TestProp" });

        // Assert
        if (shouldBeNull)
        {
            Assert.Null(property);
        }
        else
        {
            Assert.NotNull(property);
            Assert.Equal(SimulatorValueType.DOUBLE, property.ValueType);
            Assert.Equal("kg/h", property.Unit?.Name);
        }
    }

    [Fact]
    public void CreateModelProperty_WithBoolValue_ShouldConvertToDouble()
    {
        // Arrange
        var mockCom = MockDwsimProxy.Create(
            writeProps: ["Active"],
            readProps: ["Active"],
            values: new() { { "Active", true } },
            units: new() { { "Active", "" } });

        // Act
        var property = _parser.CreateModelPropertyPublic("Active", mockCom, "Valve", "V-01", new[] { "Active" });

        // Assert
        Assert.NotNull(property);
        Assert.Equal(SimulatorValueType.DOUBLE, property.ValueType);
        var doubleValue = (SimulatorValue.Double)property.Value!;
        Assert.Equal(1.0, doubleValue.Value);
    }

    [Fact]
    public void CreateModelProperty_WithPropPrefix_ShouldTranslateName()
    {
        // Arrange
        var mockCom = MockDwsimProxy.Create(
            writeProps: ["PROP_MS_0", "PROP_MS_105/Oxygen"],
            readProps: ["PROP_MS_0", "PROP_MS_105/Oxygen"],
            values: new() { { "PROP_MS_0", 298.15 }, { "PROP_MS_105/Oxygen", 0.21 } },
            units: new() { { "PROP_MS_0", "K" }, { "PROP_MS_105/Oxygen", "kg/s" } });

        // Act - Test simple PROP translation
        var property = _parser.CreateModelPropertyPublic("PROP_MS_0", mockCom, "Stream", "S-01", new[] { "PROP_MS_0", "PROP_MS_105/Oxygen" });

        // Assert
        Assert.NotNull(property);
        Assert.Equal("Temperature", property.Name);

        // Act - Test PROP with suffix translation (mixture property)
        var mixtureProperty = _parser.CreateModelPropertyPublic("PROP_MS_105/Oxygen", mockCom, "Stream", "S-01", new[] { "PROP_MS_0", "PROP_MS_105/Oxygen" });

        // Assert
        Assert.NotNull(mixtureProperty);
        Assert.Equal("Mass Flow (Mixture)/Oxygen", mixtureProperty.Name);
        Assert.Equal("kg/s", mixtureProperty.Unit?.Name);
        Assert.Equal(0.21, ((SimulatorValue.Double)mixtureProperty.Value!).Value);
    }

    [Fact]
    public void CreateModelProperty_WithDoubleArray_ShouldCreateArrayProperty()
    {
        // Arrange
        var array = new double[] { 0.25, 0.50, 0.25 };
        var mockCom = MockDwsimProxy.Create(
            writeProps: ["Composition"],
            readProps: ["Composition"],
            values: new() { { "Composition", array } },
            units: new() { { "Composition", "" } });

        // Act
        var property = _parser.CreateModelPropertyPublic("Composition", mockCom, "Stream", "S-01", new[] { "Composition" });

        // Assert
        Assert.NotNull(property);
        Assert.Equal(SimulatorValueType.DOUBLE_ARRAY, property.ValueType);
    }

    [Fact]
    public void CreateModelProperty_WithReadOnlyProperty_ShouldSetReadOnlyTrue()
    {
        // Arrange
        var mockCom = MockDwsimProxy.Create(
            writeProps: ["Temperature"],
            readProps: ["Temperature", "Density"],
            values: new() { { "Density", 1000.0 } },
            units: new() { { "Density", "kg/m3" } });

        // Act
        var property = _parser.CreateModelPropertyPublic("Density", mockCom, "Stream", "S-01", new[] { "Temperature" });

        // Assert
        Assert.NotNull(property);
        Assert.True(property.ReadOnly);
    }

    private class TestableComParser : DwsimModelParser
    {
        public TestableComParser(ILogger<DwsimClient> logger, Dictionary<string, string> propMap)
            : base(logger, propMap, "dummy-path", new MockUnitSystem())
        {
        }

        public List<SimulatorModelRevisionDataProperty> ExtractNodePropertiesFromComPublic(
            dynamic obj, string objectType, string nodeName)
            => ExtractNodePropertiesFromCom(obj, objectType, nodeName);

        public SimulatorModelRevisionDataProperty? CreateModelPropertyPublic(
            string propertyKey, dynamic obj, string objectType, string objectName, dynamic writeProperties)
            => CreateModelProperty(propertyKey, obj, objectType, objectName, writeProperties);
    }

    public class MockUnitSystem
    {
        public string? GetUnitType(string unit) => unit switch
        {
            "C" or "K" => "temperature",
            "Pa" or "bar" => "pressure",
            "kg/m3" => "density",
            "kg/h" or "kg/s" => "massflow",
            _ => "none"
        };
    }
}

/// <summary>
/// DispatchProxy-based mock for DWSIM COM objects
/// </summary>
public class MockDwsimProxy : DispatchProxy
{
    private string[] _writeProps = Array.Empty<string>();
    private string[] _readProps = Array.Empty<string>();
    private Dictionary<string, object> _values = new();
    private Dictionary<string, string> _units = new();

    public static dynamic Create(
        string[] writeProps,
        string[] readProps,
        Dictionary<string, object> values,
        Dictionary<string, string> units)
    {
        object proxy = Create<IDwsimComObject, MockDwsimProxy>();
        ((MockDwsimProxy)proxy).Initialize(writeProps, readProps, values, units);
        return proxy;
    }

    private void Initialize(
        string[] writeProps,
        string[] readProps,
        Dictionary<string, object> values,
        Dictionary<string, string> units)
    {
        _writeProps = writeProps;
        _readProps = readProps;
        _values = values;
        _units = units;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null) return null;

        return targetMethod.Name switch
        {
            nameof(IDwsimComObject.GetProperties) => args?[0] is int type && type == 1 ? _writeProps : _readProps,
            nameof(IDwsimComObject.GetPropertyValue) => args?[0] is string key ? _values.GetValueOrDefault(key) : null,
            nameof(IDwsimComObject.GetPropertyUnit) => args?[0] is string key ? _units.GetValueOrDefault(key, "") : "",
            _ => null
        };
    }
}

/// <summary>
/// Interface representing DWSIM COM object contract
/// </summary>
public interface IDwsimComObject
{
    string[] GetProperties(int propertyType);
    object? GetPropertyValue(string key);
    string GetPropertyUnit(string key);
}
