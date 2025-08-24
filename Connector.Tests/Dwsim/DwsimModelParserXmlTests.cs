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
using Connector.Dwsim;
using CogniteSdk.Alpha;
using FluentAssertions;
using Xunit;

namespace Connector.Tests.Dwsim;

public class DwsimModelParserXmlTests : IDisposable
{
    private readonly string _tempPath;
    private readonly string _testDataPath;

    public DwsimModelParserXmlTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"dwsim_xml_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempPath);

        _testDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "Xml");
    }

    private string GetTestXmlPath(string fileName)
    {
        return Path.Combine(_testDataPath, fileName);
    }

    [Fact]
    public void ParseNodesFromXmlOnly_ExtractsBasicNodeInformation()
    {
        // Arrange
        string xmlPath = GetTestXmlPath("basic_flowsheet.xml");

        // Act
        var nodes = DwsimModelParser.ParseNodesFromXmlOnly(xmlPath);

        // Assert
        nodes.Should().HaveCount(5);

        var heNode = nodes.FirstOrDefault(n => n.Id == "HE-001");
        heNode.Should().NotBeNull();
        heNode!.Name.Should().Be("HE-001");
        heNode.Type.Should().Be("HeatExchanger");
        heNode.Properties.Should().BeEmpty(); // No COM properties in XML-only parsing

        // Verify graphical properties
        heNode.GraphicalObject.Should().NotBeNull();
        heNode.GraphicalObject!.Position.X.Should().Be(100);
        heNode.GraphicalObject.Position.Y.Should().Be(200);
        heNode.GraphicalObject.Width.Should().Be(40);
        heNode.GraphicalObject.Height.Should().Be(40);
    }

    [Fact]
    public void GenerateFlowsheetEdgesFromXml_ExtractsConnections()
    {
        // Arrange
        string xmlPath = GetTestXmlPath("connections_flowsheet.xml");
        var nodes = DwsimModelParser.ParseNodesFromXmlOnly(xmlPath);

        // Act
        var edges = DwsimModelParser.GenerateFlowsheetEdgesFromXml(xmlPath, nodes).ToList();

        // Assert
        edges.Should().HaveCount(3);

        // Verify pump to stream connection
        var pumpToStream = edges.FirstOrDefault(e => e is { SourceId: "PUMP-001", TargetId: "MSTR-001" });
        pumpToStream.Should().NotBeNull();
        pumpToStream!.ConnectionType.Should().Be(SimulatorModelRevisionDataConnectionType.Material);
        pumpToStream.Name.Should().Be("PUMP-001 -> Stream 1");
    }

    [Fact]
    public void GenerateFlowsheetEdgesFromXml_HandlesUnattachedConnectors()
    {
        // Arrange
        string xmlPath = GetTestXmlPath("unattached_connectors.xml");
        var nodes = DwsimModelParser.ParseNodesFromXmlOnly(xmlPath);

        // Act
        var edges = DwsimModelParser.GenerateFlowsheetEdgesFromXml(xmlPath, nodes);

        // Assert
        edges.Should().BeEmpty(); // No edges for unattached connectors
    }

    [Fact]
    public void GenerateFlowsheetEdgesFromXml_RemovesDuplicateConnections()
    {
        // Arrange
        string xmlPath = GetTestXmlPath("duplicate_connections.xml");
        var nodes = DwsimModelParser.ParseNodesFromXmlOnly(xmlPath);

        // Act
        var edges = DwsimModelParser.GenerateFlowsheetEdgesFromXml(xmlPath, nodes).ToList();

        // Assert
        edges.Should().HaveCount(1); // Should remove duplicates
        var edge = edges.First();
        edge.SourceId.Should().Be("NODE-001");
        edge.TargetId.Should().Be("NODE-002");
    }

    [Fact]
    public void ExtractThermodynamicDataFromXml_ExtractsComponentsAndPropertyPackages()
    {
        // Arrange
        string xmlPath = GetTestXmlPath("thermodynamics_complete.xml");

        // Act
        var thermodynamics = DwsimModelParser.ExtractThermodynamicDataFromXml(xmlPath);

        // Assert
        thermodynamics.Should().NotBeNull();
        thermodynamics.Components.Should().HaveCount(3);
        thermodynamics.Components.Should().Contain("Water");
        thermodynamics.Components.Should().Contain("Ethanol");
        thermodynamics.Components.Should().Contain("Benzene");

        thermodynamics.PropertyPackages.Should().HaveCount(2);
        thermodynamics.PropertyPackages.Should().Contain("PP-PengRobinson");
        thermodynamics.PropertyPackages.Should().Contain("PP-NRTL");
    }

    [Fact]
    public void ExtractThermodynamicDataFromXml_HandlesEmptyCompounds()
    {
        // Arrange
        string xmlPath = GetTestXmlPath("thermodynamics_empty_compounds.xml");

        // Act
        var thermodynamics = DwsimModelParser.ExtractThermodynamicDataFromXml(xmlPath);

        // Assert
        thermodynamics.Should().NotBeNull();
        thermodynamics.Components.Should().BeEmpty();
        thermodynamics.PropertyPackages.Should().HaveCount(1);
    }

    [Fact]
    public void ExtractThermodynamicDataFromXml_IgnoresInvalidCompounds()
    {
        // Arrange
        string xmlPath = GetTestXmlPath("thermodynamics_invalid_compounds.xml");

        // Act
        var thermodynamics = DwsimModelParser.ExtractThermodynamicDataFromXml(xmlPath);

        // Assert
        thermodynamics.Should().NotBeNull();
        thermodynamics.Components.Should().HaveCount(2); // Only valid compounds
        thermodynamics.Components.Should().Contain("Water");
        thermodynamics.Components.Should().Contain("Ethanol");
        thermodynamics.Components.Should().NotContain("");
    }

    [Fact]
    public void CreateNodeFromXmlOnly_HandlesNodeWithoutGraphics()
    {
        // Arrange
        var simObj = XElement.Parse(@"
            <SimulationObject>
                <Type>DWSIM.UnitOperations.UnitOperations.Mixer</Type>
                <Name>MIX-001</Name>
                <ComponentName>MIX-001</ComponentName>
            </SimulationObject>");

        // Act
        var node = DwsimModelParser.CreateNodeFromXmlOnly(simObj, null);

        // Assert
        node.Should().NotBeNull();
        node!.Id.Should().Be("MIX-001");
        node.Name.Should().Be("MIX-001"); // Falls back to ComponentName
        node.Type.Should().Be("Mixer");
        node.GraphicalObject.Should().BeNull();
    }


    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }
    }
}
