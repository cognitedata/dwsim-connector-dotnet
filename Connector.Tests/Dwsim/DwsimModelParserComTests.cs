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

using System.IO.Compression;
using Connector.Dwsim;
using Connector.Tests.Mocks;
using CogniteSdk.Alpha;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Connector.Tests.Dwsim;

public class DwsimModelParserComTests
{
    private readonly TestableModelParser _parser;

    public DwsimModelParserComTests()
    {
        Mock<ILogger<DwsimClient>> mockLogger = new();
        _parser = new TestableModelParser(mockLogger.Object, MockDwsimData.PropertyMap);
    }

    [Fact]
    public void ExtractNodePropertiesFromCom_ExtractsPropertiesCorrectly()
    {
        // Arrange
        var comObject = new MockDwsimFlowsheetObject("MSTR-001", "Material Stream");
        comObject.AddProperty("PROP_MS_0", 298.15, "K", true);
        comObject.AddProperty("PROP_MS_1", 101325.0, "Pa", true);
        comObject.AddProperty("PROP_MS_2", 10.0, "kg/s", true);
        comObject.AddProperty("Density", 998.2, "kg/m3", false);
        comObject.AddProperty("ComponentNames", new[] { "Water", "Ethanol" }, "", false);

        // Act
        var properties = _parser.TestExtractNodePropertiesFromCom(comObject, "Material Stream", "MSTR-001");

        // Assert
        properties.Should().HaveCount(5);

        // Test mapped property name
        var tempProp = properties.FirstOrDefault(p => p.Name == "Temperature");
        tempProp.Should().NotBeNull();
        tempProp!.ValueType.Should().Be(SimulatorValueType.DOUBLE);
        tempProp.ReadOnly.Should().BeFalse();
        tempProp.Unit?.Name.Should().Be("K");
        tempProp.Unit?.Quantity.Should().Be("temperature");

        // Test unmapped property
        var densityProp = properties.FirstOrDefault(p => p.Name == "Density");
        densityProp.Should().NotBeNull();
        densityProp!.ReadOnly.Should().BeTrue();

        // Test array property
        var namesProp = properties.FirstOrDefault(p => p.Name == "ComponentNames");
        namesProp.Should().NotBeNull();
        namesProp!.ValueType.Should().Be(SimulatorValueType.STRING_ARRAY);
    }

    [Fact]
    public void ExtractNodePropertiesFromCom_HandlesNaNValues()
    {
        // Arrange
        var comObject = new MockDwsimFlowsheetObject("MSTR-001", "Material Stream");
        comObject.AddProperty("ValidProperty", 298.15, "K", true);
        comObject.AddProperty("NaNProperty", double.NaN, "Pa", true);
        comObject.AddProperty("InfinityProperty", double.PositiveInfinity, "m/s", true);

        // Act
        var properties = _parser.TestExtractNodePropertiesFromCom(comObject, "Material Stream", "MSTR-001");

        // Assert
        properties.Should().HaveCount(1); // Only valid property should be included
        properties.First().Name.Should().Be("ValidProperty");
    }

    [Fact]
    public void ExtractNodePropertiesFromCom_HandlesEmptyArrays()
    {
        // Arrange
        var comObject = new MockDwsimFlowsheetObject("MSTR-001", "Material Stream");
        comObject.AddProperty("ValidProperty", 298.15, "K", true);
        comObject.AddProperty("EmptyArray", Array.Empty<double>(), "", false);

        // Act
        var properties = _parser.TestExtractNodePropertiesFromCom(comObject, "Material Stream", "MSTR-001");

        // Assert
        properties.Should().HaveCount(1); // Empty array should be filtered out
        properties.First().Name.Should().Be("ValidProperty");
    }

    [Fact]
    public void ExtractNodePropertiesFromCom_HandlesComErrors()
    {
        // Arrange
        var comObject = new MockDwsimFlowsheetObject("MSTR-001", "Material Stream", shouldThrowOnPropertyAccess: true);

        // Act
        var properties = _parser.TestExtractNodePropertiesFromCom(comObject, "Material Stream", "MSTR-001");

        // Assert
        properties.Should().BeEmpty(); // Should handle errors gracefully
    }

    [Fact]
    public void ExtractNodePropertiesFromCom_RespectsMaxPropertiesLimit()
    {
        // Arrange
        var comObject = new MockDwsimFlowsheetObject("MSTR-001", "Material Stream");

        // Add more properties than the limit (100)
        for (int i = 0; i < 150; i++)
        {
            comObject.AddProperty($"Property_{i:D3}", i * 10.0, "unit", true);
        }

        // Act
        var properties = _parser.TestExtractNodePropertiesFromCom(comObject, "Material Stream", "MSTR-001");

        // Assert
        properties.Should().HaveCount(100); // Should be limited to MaxPropertiesPerNode
    }

    [Fact]
    public void CreateModelProperty_HandlesVariousValueTypes()
    {
        // Arrange
        var comObject = new MockDwsimFlowsheetObject("TEST", "Test Object");
        comObject.AddProperty("DoubleVal", 123.45, "m", true);
        comObject.AddProperty("FloatVal", 67.89f, "kg", true);
        comObject.AddProperty("IntVal", 42, "", true);
        comObject.AddProperty("BoolVal", true, "", false);
        comObject.AddProperty("StringVal", "test string", "", false);
        comObject.AddProperty("DoubleArray", new[] { 1.1, 2.2, 3.3 }, "Pa", false);
        comObject.AddProperty("StringArray", new[] { "A", "B", "C" }, "", false);

        // Act
        var properties = _parser.TestExtractNodePropertiesFromCom(comObject, "Test Object", "TEST");

        // Assert
        properties.Should().HaveCount(7);

        var doubleProp = properties.FirstOrDefault(p => p.Name == "DoubleVal");
        doubleProp!.ValueType.Should().Be(SimulatorValueType.DOUBLE);

        var boolProp = properties.FirstOrDefault(p => p.Name == "BoolVal");
        boolProp!.ValueType.Should().Be(SimulatorValueType.DOUBLE); // Converted to double

        var stringProp = properties.FirstOrDefault(p => p.Name == "StringVal");
        stringProp!.ValueType.Should().Be(SimulatorValueType.STRING);

        var arrayProp = properties.FirstOrDefault(p => p.Name == "DoubleArray");
        arrayProp!.ValueType.Should().Be(SimulatorValueType.DOUBLE_ARRAY);

        var stringArrayProp = properties.FirstOrDefault(p => p.Name == "StringArray");
        stringArrayProp!.ValueType.Should().Be(SimulatorValueType.STRING_ARRAY);
    }

    [Fact]
    public void CreateModelProperty_HandlesUnknownUnitType()
    {
        // Arrange - Create a COM object with an unknown unit
        var comObject = new MockDwsimFlowsheetObject("TEST", "Test Object");
        comObject.AddProperty("UnknownUnit", 123.45, "unknown_unit", true);

        // Act
        var properties = _parser.TestExtractNodePropertiesFromCom(comObject, "Test Object", "TEST");

        // Assert
        var property = properties.First();
        property.Unit.Should().BeNull(); // Should not create unit reference for unknown unit
    }

    [Fact]
    public void CreateModelProperty_HandlesMappedPropertyNames()
    {
        // Arrange
        var comObject = new MockDwsimFlowsheetObject("MSTR-001", "Material Stream");
        comObject.AddProperty("PROP_MS_0", 298.15, "K", true);
        comObject.AddProperty("PROP_MS_1/Phase1", 101325.0, "Pa", true);

        // Act
        var properties = _parser.TestExtractNodePropertiesFromCom(comObject, "Material Stream", "MSTR-001");

        // Assert
        var tempProp = properties.FirstOrDefault(p => p.Name == "Temperature");
        tempProp.Should().NotBeNull();

        var phaseProp = properties.FirstOrDefault(p => p.Name == "Pressure/Phase1");
        phaseProp.Should().NotBeNull();
    }

    [Fact]
    public void MockDwsimFlowsheetObject_ProductNameIsAccessible()
    {
        // Arrange & Act
        var comObject = new MockDwsimFlowsheetObject("MSTR-001", "Material Stream");
        
        // Assert
        comObject.ProductName.Should().Be("Material Stream");
        
        // Verify it can be modified
        comObject.ProductName = "Updated Stream Type";
        comObject.ProductName.Should().Be("Updated Stream Type");
    }

    [Fact]
    public void Parse_IntegratesXmlAndComData()
    {
        // Arrange - Create temporary DWXMZ file from XML
        string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "Xml", "basic_flowsheet.xml");
        string tempDwxmzPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.dwxmz");

        try
        {
            // Create DWXMZ (zip file) containing the XML
            using (var archive = ZipFile.Open(tempDwxmzPath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(xmlPath, "data.xml");
            }

            // Create mock COM objects for the nodes in the XML
            var mockFlowsheet = new MockDwsimFlowsheet();
            var heatExchanger = new MockDwsimFlowsheetObject("HE-001", "Heat Exchanger");
            heatExchanger.AddProperty("Heat Duty", 1500.0, "kW", true);
            heatExchanger.AddProperty("Pressure Drop", 1000.0, "Pa", false);
            mockFlowsheet.AddObject("HE-001", heatExchanger);

            var pump = new MockDwsimFlowsheetObject("PUMP-001", "Pump");
            pump.AddProperty("Pump Power", 750.0, "W", true);
            pump.AddProperty("Efficiency", 0.85, "", false);
            mockFlowsheet.AddObject("PUMP-001", pump);

            // Act - Parse using both XML and COM data
            var flowsheet = _parser.TestParseWithMocks(mockFlowsheet, tempDwxmzPath, CancellationToken.None);

            // Assert
            flowsheet.Should().NotBeNull();
            flowsheet!.SimulatorObjectNodes.Should().HaveCount(5);

            // Verify that XML graphics data is preserved
            var heNode = flowsheet.SimulatorObjectNodes.FirstOrDefault(n => n.Id == "HE-001");
            heNode.Should().NotBeNull();
            heNode!.GraphicalObject.Should().NotBeNull();
            heNode.GraphicalObject!.Position.X.Should().Be(100);
            heNode.GraphicalObject.Position.Y.Should().Be(200);

            // Verify that COM properties are added
            heNode.Properties.Should().HaveCount(2);
            var heatDutyProp = heNode.Properties.FirstOrDefault(p => p.Name == "Heat Duty");
            heatDutyProp.Should().NotBeNull();
            heatDutyProp!.Unit?.Quantity.Should().Be("power");

            // Verify nodes without COM objects still exist but have no properties
            var streamNode = flowsheet.SimulatorObjectNodes.FirstOrDefault(n => n.Id == "MSTR-001");
            streamNode.Should().NotBeNull();
            streamNode!.Properties.Should().BeEmpty(); // No COM object provided
        }
        finally
        {
            // Cleanup temp file
            if (File.Exists(tempDwxmzPath))
                File.Delete(tempDwxmzPath);
        }
    }

    // Testable version that exposes internal methods for testing
    private class TestableModelParser(ILogger<DwsimClient> logger, Dictionary<string, string> propMap)
        : DwsimModelParser(logger, propMap, CreateMockDwsimPath(), new MockUnitSystem())
    {
        private static string CreateMockDwsimPath()
        {
            // Create a temporary directory to simulate DWSIM installation
            string tempPath = Path.Combine(Path.GetTempPath(), $"mock_dwsim_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempPath);
            return tempPath;
        }

        public List<SimulatorModelRevisionDataProperty> TestExtractNodePropertiesFromCom(
            MockDwsimFlowsheetObject obj, string objectType, string nodeName)
        {
            // Use reflection to call the private method
            var method = typeof(DwsimModelParser).GetMethod("ExtractNodePropertiesFromCom",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return (List<SimulatorModelRevisionDataProperty>)method!.Invoke(this, [obj, objectType, nodeName])!;
        }

        public SimulatorModelRevisionDataFlowsheet? TestParseWithMocks(MockDwsimFlowsheet mockFlowsheet, string filePath, CancellationToken token)
        {
            return Parse(mockFlowsheet, filePath, token);
        }
    }
}
