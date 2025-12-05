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

using System.Xml.Linq;
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

    // Expected values from ShowerMixer.dwxmz
    private const string HotWaterId = "MAT-35db8d0f-0c15-46db-8913-163b2a809fc2";
    private const string ColdWaterId = "MAT-95bdfac9-28cb-42a1-8e96-f0343ebe1b9e";
    private const string MixerId = "MIST-fd2d8a5c-d194-40e0-8728-efc763d2236a";
    private const string ShowerWaterId = "MAT-2cdd6910-215c-45f9-a147-71d22dac909d";

    public DwsimModelParserXmlTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");
        Mock<ILogger<DwsimClient>> mockLogger = new();
        _parser = new TestableParser(mockLogger.Object, new Dictionary<string, string>(), "dummy-path");
    }

    private string ExtractXmlForTesting()
    {
        string dwxmzPath = Path.Combine(_testDataPath, "ShowerMixer.dwxmz");
        string xmlPath = _parser.ExtractXmlFromDwxmz(dwxmzPath);

        // Track temp directory for cleanup
        string? tempDir = Path.GetDirectoryName(xmlPath);
        if (tempDir != null)
            _tempDirectories.Add(tempDir);

        return xmlPath;
    }

    private XDocument LoadXmlForTesting(string xmlPath)
    {
        return XDocument.Load(xmlPath);
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
    public void ParseNodesFromXml_WithShowerMixer_ShouldReturnFourNodes()
    {
        // Arrange
        string xmlPath = ExtractXmlForTesting();

        try
        {
            XDocument doc = LoadXmlForTesting(xmlPath);

            // Act
            List<SimulatorModelRevisionDataObjectNode> nodes = DwsimModelParser.ParseNodesFromXml(doc);

            // Assert
            Assert.NotNull(nodes);
            Assert.Equal(4, nodes.Count);

            // Verify all nodes have required properties
            foreach (var node in nodes)
            {
                Assert.NotNull(node.Id);
                Assert.NotNull(node.Name);
                Assert.NotNull(node.Type);
                Assert.NotNull(node.Properties);
            }

            // Verify specific nodes exist
            Assert.Contains(nodes, n => n.Id == HotWaterId);
            Assert.Contains(nodes, n => n.Id == ColdWaterId);
            Assert.Contains(nodes, n => n.Id == MixerId);
            Assert.Contains(nodes, n => n.Id == ShowerWaterId);

            // Verify node types
            Assert.Equal(3, nodes.Count(n => n.Type == "MaterialStream"));
            Assert.Equal(1, nodes.Count(n => n.Type == "Mixer"));
        }
        finally
        {
            // Cleanup
            _parser.CleanupTempFiles(xmlPath);
        }
    }

    [Fact]
    public void ParseNodesFromXml_WithEmptyDocument_ShouldReturnEmptyList()
    {
        // Arrange
        var emptyDoc = new XDocument();

        // Act
        List<SimulatorModelRevisionDataObjectNode> nodes = DwsimModelParser.ParseNodesFromXml(emptyDoc);

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
        Assert.Null(node.GraphicalObject.Active);
    }

    [Fact]
    public void ParseNodesFromXml_ShouldExtractGraphicalProperties()
    {
        // Arrange
        string xmlPath = ExtractXmlForTesting();

        try
        {
            XDocument doc = LoadXmlForTesting(xmlPath);

            // Act
            List<SimulatorModelRevisionDataObjectNode> nodes = DwsimModelParser.ParseNodesFromXml(doc);

            // Assert
            Assert.NotNull(nodes);

            // All nodes should have graphical properties
            var nodesWithGraphics = nodes.Where(n => n.GraphicalObject != null).ToList();
            Assert.Equal(4, nodesWithGraphics.Count);

            // Verify Hot Water stream graphics (X=184.9046, Y=286.0079)
            var hotWater = nodes.First(n => n.Id == HotWaterId);
            Assert.NotNull(hotWater.GraphicalObject);
            Assert.NotNull(hotWater.GraphicalObject.Position);
            Assert.Equal(184.9046, hotWater.GraphicalObject.Position.X, precision: 4);
            Assert.Equal(286.0079, hotWater.GraphicalObject.Position.Y, precision: 4);
            Assert.Equal(20, hotWater.GraphicalObject.Width);
            Assert.Equal(20, hotWater.GraphicalObject.Height);
            Assert.True(hotWater.GraphicalObject.Active);
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
        Assert.True(node.GraphicalObject.Active);
    }

    #region Edge Generation Tests

    [Fact]
    public void GenerateFlowsheetEdgesFromXml_WithShowerMixer_ShouldReturnThreeEdges()
    {
        // Arrange
        string xmlPath = ExtractXmlForTesting();

        try
        {
            XDocument doc = LoadXmlForTesting(xmlPath);
            List<SimulatorModelRevisionDataObjectNode> nodes = DwsimModelParser.ParseNodesFromXml(doc);

            // Act
            var edges = DwsimModelParser.GenerateFlowsheetEdgesFromXml(doc, nodes).ToList();

            // Assert
            Assert.Equal(3, edges.Count);

            // Verify Hot Water -> Mixer connection
            var hotToMixer = edges.FirstOrDefault(e => e.SourceId == HotWaterId && e.TargetId == MixerId);
            Assert.NotNull(hotToMixer);
            Assert.Equal(SimulatorModelRevisionDataConnectionType.Material, hotToMixer.ConnectionType);

            // Verify Cold Water -> Mixer connection
            var coldToMixer = edges.FirstOrDefault(e => e.SourceId == ColdWaterId && e.TargetId == MixerId);
            Assert.NotNull(coldToMixer);
            Assert.Equal(SimulatorModelRevisionDataConnectionType.Material, coldToMixer.ConnectionType);

            // Verify Mixer -> Shower Water connection
            var mixerToShower = edges.FirstOrDefault(e => e.SourceId == MixerId && e.TargetId == ShowerWaterId);
            Assert.NotNull(mixerToShower);
        }
        finally
        {
            _parser.CleanupTempFiles(xmlPath);
        }
    }

    [Fact]
    public void GenerateFlowsheetEdgesFromXml_WithEmptyNodeList_ShouldReturnEmptyEdges()
    {
        // Arrange
        string xmlPath = ExtractXmlForTesting();

        try
        {
            XDocument doc = LoadXmlForTesting(xmlPath);
            var emptyNodes = new List<SimulatorModelRevisionDataObjectNode>();

            // Act
            var edges = DwsimModelParser.GenerateFlowsheetEdgesFromXml(doc, emptyNodes).ToList();

            // Assert - no edges can be created without nodes
            Assert.Empty(edges);
        }
        finally
        {
            _parser.CleanupTempFiles(xmlPath);
        }
    }

    [Fact]
    public void GenerateFlowsheetEdgesFromXml_WithNullNodeList_ShouldReturnEmptyEdges()
    {
        // Arrange
        string xmlPath = ExtractXmlForTesting();

        try
        {
            XDocument doc = LoadXmlForTesting(xmlPath);

            // Act
            var edges = DwsimModelParser.GenerateFlowsheetEdgesFromXml(doc, null!).ToList();

            // Assert - no edges can be created without nodes
            Assert.Empty(edges);
        }
        finally
        {
            _parser.CleanupTempFiles(xmlPath);
        }
    }

    #endregion

    #region Thermodynamic Extraction Tests

    [Fact]
    public void ExtractThermodynamicDataFromXml_WithShowerMixer_ShouldExtractWaterCompound()
    {
        // Arrange
        string xmlPath = ExtractXmlForTesting();

        try
        {
            XDocument doc = LoadXmlForTesting(xmlPath);

            // Act
            var thermodynamics = DwsimModelParser.ExtractThermodynamicDataFromXml(doc);

            // Assert
            Assert.NotNull(thermodynamics);
            Assert.NotNull(thermodynamics.Components);
            Assert.Single(thermodynamics.Components);
            Assert.Contains("Water", thermodynamics.Components);
        }
        finally
        {
            _parser.CleanupTempFiles(xmlPath);
        }
    }

    [Fact]
    public void ExtractThermodynamicDataFromXml_WithShowerMixer_ShouldExtractPropertyPackage()
    {
        // Arrange
        string xmlPath = ExtractXmlForTesting();

        try
        {
            XDocument doc = LoadXmlForTesting(xmlPath);

            // Act
            var thermodynamics = DwsimModelParser.ExtractThermodynamicDataFromXml(doc);

            // Assert
            Assert.NotNull(thermodynamics);
            Assert.NotNull(thermodynamics.PropertyPackages);
            Assert.Single(thermodynamics.PropertyPackages);
            Assert.Contains("Steam Tables (IAPWS-IF97)", thermodynamics.PropertyPackages);
        }
        finally
        {
            _parser.CleanupTempFiles(xmlPath);
        }
    }

    [Fact]
    public void ExtractThermodynamicDataFromXml_WithEmptyDocument_ShouldReturnEmptyLists()
    {
        // Arrange
        var emptyDoc = new XDocument();

        // Act
        var thermodynamics = DwsimModelParser.ExtractThermodynamicDataFromXml(emptyDoc);

        // Assert
        Assert.NotNull(thermodynamics);
        Assert.NotNull(thermodynamics.Components);
        Assert.NotNull(thermodynamics.PropertyPackages);
        Assert.Empty(thermodynamics.Components);
        Assert.Empty(thermodynamics.PropertyPackages);
    }

    #endregion

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
