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

using CogniteSdk.Alpha;
using Connector.Dwsim;
using Microsoft.Extensions.Logging;
using Moq;

namespace Connector.Tests.Dwsim;

public class DwsimModelParserXmlTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly TestableParser _parser;
    private readonly List<string> _tempDirectories = new();

    public DwsimModelParserXmlTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");
        Mock<ILogger<DwsimClient>> mockLogger = new();
        _parser = new TestableParser(mockLogger.Object, new Dictionary<string, string>(), "dummy-path");
    }

    private string ExtractXmlForTesting()
    {
        string dwxmzPath = Path.Combine(_testDataPath, "minimal_simulation.dwxmz");
        string xmlPath = _parser.ExtractXmlFromDwxmz(dwxmzPath);

        // Track temp directory for cleanup
        string? tempDir = Path.GetDirectoryName(xmlPath);
        if (tempDir != null)
            _tempDirectories.Add(tempDir);

        return xmlPath;
    }

    public void Dispose()
    {
        // Cleanup all temp directories
        foreach (string dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch { /* Ignore cleanup errors */ }
        }
    }

    [Fact]
    public void ParseNodesFromXml_WithMinimalSimulation_ShouldReturnNodes()
    {
        // Arrange
        string xmlPath = ExtractXmlForTesting();

        try
        {
            // Act
            List<SimulatorModelRevisionDataObjectNode> nodes = DwsimModelParser.ParseNodesFromXml(xmlPath);

            // Assert
            Assert.NotNull(nodes);
            Assert.True(nodes.Count > 0, "Should parse at least one node from minimal_simulation.dwxmz");

            // Verify all nodes have required properties
            foreach (var node in nodes)
            {
                Assert.NotNull(node.Id);
                Assert.NotNull(node.Name);
                Assert.NotNull(node.Type);
                Assert.NotNull(node.Properties);
            }

            // Check for various node types if they exist
            var nodeTypes = nodes.Select(n => n.Type).Distinct().ToList();
            Assert.True(nodeTypes.Count > 0, "Should have at least one node type");
        }
        finally
        {
            // Cleanup
            _parser.CleanupTempFiles(xmlPath);
        }
    }

    [Fact]
    public void ParseNodesFromXml_WithInvalidXmlPath_ShouldReturnEmptyList()
    {
        // Arrange
        string invalidPath = Path.Combine(_testDataPath, "nonexistent.xml");

        // Act
        List<SimulatorModelRevisionDataObjectNode> nodes = DwsimModelParser.ParseNodesFromXml(invalidPath);

        // Assert
        Assert.NotNull(nodes);
        Assert.Empty(nodes);
    }

    [Fact]
    public void CreateNodeFromXml_WithValidElements_ShouldCreateNode()
    {
        // Arrange
        const string xmlContent = @"
            <SimulationObject>
                <ComponentName>TestObject</ComponentName>
                <Type>DWSIM.UnitOperations.Pump</Type>
            </SimulationObject>";
        var simObj = System.Xml.Linq.XElement.Parse(xmlContent);

        const string graphicXmlContent = @"
            <GraphicObject>
                <Name>TestObject</Name>
                <Tag>TestPump</Tag>
                <X>150</X>
                <Y>250</Y>
                <Width>50</Width>
                <Height>30</Height>
                <Rotation>45</Rotation>
                <FlippedH>true</FlippedH>
                <FlippedV>false</FlippedV>
                <Active>true</Active>
            </GraphicObject>";
        var graphicObj = System.Xml.Linq.XElement.Parse(graphicXmlContent);

        // Act
        var node = DwsimModelParser.CreateNodeFromXml(simObj, graphicObj);

        // Assert
        Assert.NotNull(node);
        Assert.Equal("TestObject", node.Id);
        Assert.Equal("TestPump", node.Name);
        Assert.Equal("Pump", node.Type);
        Assert.NotNull(node.GraphicalObject);
        Assert.Equal(150, node.GraphicalObject.Position.X);
        Assert.Equal(250, node.GraphicalObject.Position.Y);
        Assert.Equal(50, node.GraphicalObject.Width);
        Assert.Equal(30, node.GraphicalObject.Height);
        Assert.Equal(45, node.GraphicalObject.Angle);
        Assert.True(node.GraphicalObject.ScaleX);
        Assert.False(node.GraphicalObject.ScaleY);
        Assert.True(node.GraphicalObject.Active);
    }

    [Fact]
    public void CreateNodeFromXml_WithoutGraphicObject_ShouldCreateNodeWithoutGraphics()
    {
        // Arrange
        const string xmlContent = @"
            <SimulationObject>
                <ComponentName>TestStream</ComponentName>
                <Type>DWSIM.Thermodynamics.Streams.MaterialStream</Type>
            </SimulationObject>";
        var simObj = System.Xml.Linq.XElement.Parse(xmlContent);

        // Act
        var node = DwsimModelParser.CreateNodeFromXml(simObj, null);

        // Assert
        Assert.NotNull(node);
        Assert.Equal("TestStream", node.Id);
        Assert.Equal("TestStream", node.Name);
        Assert.Equal("MaterialStream", node.Type);
        Assert.Null(node.GraphicalObject);
    }

    [Fact]
    public void CreateNodeFromXml_WithInvalidXml_ShouldReturnNull()
    {
        // Arrange
        const string xmlContent = @"<InvalidElement />";
        var invalidObj = System.Xml.Linq.XElement.Parse(xmlContent);

        // Act
        var node = DwsimModelParser.CreateNodeFromXml(invalidObj, null);

        // Assert
        Assert.Null(node);
    }

    [Fact]
    public void CreateNodeFromXml_WithMissingRequiredFields_ShouldReturnNull()
    {
        // Arrange - Missing Type element
        const string xmlContentNoType = @"
            <SimulationObject>
                <ComponentName>TestObject</ComponentName>
            </SimulationObject>";
        var simObjNoType = System.Xml.Linq.XElement.Parse(xmlContentNoType);

        // Arrange - Missing both ComponentName and Name
        const string xmlContentNoId = @"
            <SimulationObject>
                <Type>DWSIM.UnitOperations.Pump</Type>
            </SimulationObject>";
        var simObjNoId = System.Xml.Linq.XElement.Parse(xmlContentNoId);

        // Act
        var nodeNoType = DwsimModelParser.CreateNodeFromXml(simObjNoType, null);
        var nodeNoId = DwsimModelParser.CreateNodeFromXml(simObjNoId, null);

        // Assert
        Assert.Null(nodeNoType); // Should return null when Type is missing
        Assert.Null(nodeNoId); // Should return null when ID (ComponentName/Name) is missing
    }

    [Fact]
    public void CreateNodeFromXml_WithPartialGraphicProperties_ShouldOnlySetPresentValues()
    {
        // Arrange
        const string simXmlContent = @"
            <SimulationObject>
                <ComponentName>TestObject</ComponentName>
                <Type>DWSIM.UnitOperations.Pump</Type>
            </SimulationObject>";
        var simObj = System.Xml.Linq.XElement.Parse(simXmlContent);

        // Graphic object with only some properties
        string graphicXmlContent = @"
            <GraphicObject>
                <Name>TestObject</Name>
                <X>100</X>
                <Y>200</Y>
                <Width>50</Width>
            </GraphicObject>";
        var graphicObj = System.Xml.Linq.XElement.Parse(graphicXmlContent);

        // Act
        var node = DwsimModelParser.CreateNodeFromXml(simObj, graphicObj);

        // Assert
        Assert.NotNull(node);
        Assert.NotNull(node.GraphicalObject);
        Assert.NotNull(node.GraphicalObject.Position);
        Assert.Equal(100, node.GraphicalObject.Position.X);
        Assert.Equal(200, node.GraphicalObject.Position.Y);
        Assert.Equal(50, node.GraphicalObject.Width);

        // These should be null since they weren't in the XML
        Assert.Null(node.GraphicalObject.Height);
        Assert.Null(node.GraphicalObject.Angle);
        Assert.Null(node.GraphicalObject.ScaleX);
        Assert.Null(node.GraphicalObject.ScaleY);
        Assert.Null(node.GraphicalObject.Active);
    }

    [Fact]
    public void ParseNodesFromXml_ShouldExtractGraphicalProperties()
    {
        // Arrange
        string xmlPath = ExtractXmlForTesting();

        try
        {
            // Act
            List<SimulatorModelRevisionDataObjectNode> nodes = DwsimModelParser.ParseNodesFromXml(xmlPath);

            // Assert
            Assert.NotNull(nodes);

            // Check if any nodes have graphical properties
            var nodesWithGraphics = nodes.Where(n => n.GraphicalObject != null).ToList();
            Assert.NotEmpty(nodesWithGraphics);

            foreach (var node in nodesWithGraphics)
            {
                if (node.GraphicalObject.Width.HasValue)
                    Assert.True(node.GraphicalObject.Width >= 0);

                if (node.GraphicalObject.Height.HasValue)
                    Assert.True(node.GraphicalObject.Height >= 0);
            }
        }
        finally
        {
            // Cleanup
            _parser.CleanupTempFiles(xmlPath);
        }
    }

    [Fact]
    public void CreateNodeFromXml_WithMalformedNumericData_ShouldHandleGracefully()
    {
        // Arrange - graphic object with mix of valid and invalid values
        var simObj = System.Xml.Linq.XElement.Parse(@"
            <SimulationObject>
                <ComponentName>TestObject</ComponentName>
                <Type>DWSIM.UnitOperations.Pump</Type>
            </SimulationObject>");
        var graphicObj = System.Xml.Linq.XElement.Parse(@"
            <GraphicObject>
                <Name>TestObject</Name>
                <X>not_a_number</X>
                <Y>200</Y>
                <Width>invalid</Width>
                <Height>50</Height>
                <Rotation>bad_value</Rotation>
                <FlippedH>not_bool</FlippedH>
                <Active>true</Active>
            </GraphicObject>");

        // Act
        var node = DwsimModelParser.CreateNodeFromXml(simObj, graphicObj);

        // Assert - invalid values become null, valid values are parsed
        Assert.NotNull(node);
        Assert.Null(node.GraphicalObject.Position); // X was invalid
        Assert.Null(node.GraphicalObject.Width);
        Assert.Equal(50, node.GraphicalObject.Height);
        Assert.Null(node.GraphicalObject.Angle);
        Assert.Null(node.GraphicalObject.ScaleX);
        Assert.True(node.GraphicalObject.Active);
    }

    private class TestableParser(
        ILogger<DwsimClient> logger,
        Dictionary<string, string> propMap,
        string dwsimInstallationPath)
        : DwsimModelParser(logger, propMap, dwsimInstallationPath, new MockUnitSystem())
    {
        public new string ExtractXmlFromDwxmz(string filePath) => base.ExtractXmlFromDwxmz(filePath);
        public new void CleanupTempFiles(string xmlPath) => base.CleanupTempFiles(xmlPath);
    }

    private class MockUnitSystem
    {
        // Mock unit system for testing without DWSIM dependencies
    }
}
