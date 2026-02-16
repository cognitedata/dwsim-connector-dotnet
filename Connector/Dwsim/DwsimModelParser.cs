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
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using CogniteSdk.Alpha;
using Microsoft.Extensions.Logging;

namespace Connector.Dwsim;

public class DwsimModelParser
{
    private readonly ILogger<DwsimClient> _logger;
    private readonly Dictionary<string, string> _propMap;
    private readonly DwsimModelParsingConfig _modelParsingConfig;
    private readonly dynamic? _unitSystem;

    /// <summary>
    /// Known material stream type names in DWSIM
    /// </summary>
    private static readonly HashSet<string> MaterialStreamTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "MaterialStream",
        "Material Stream",
        "CorrentedeMatria"
    };

    /// <summary>
    /// Known energy stream type names in DWSIM
    /// </summary>
    private static readonly HashSet<string> EnergyStreamTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "EnergyStream",
        "Energy Stream"
    };

    /// <summary>
    /// Standardized type names for streams
    /// </summary>
    private const string StandardMaterialStreamType = "Material Stream";
    private const string StandardEnergyStreamType = "Energy Stream";

    /// <summary>
    /// Normalizes a stream type to its standardized form.
    /// Material stream variants become "Material Stream", energy stream variants become "Energy Stream".
    /// Other types are returned unchanged.
    /// </summary>
    /// <param name="originalType">The original type from XML</param>
    /// <returns>Normalized type string</returns>
    private static string NormalizeStreamType(string originalType)
    {
        if (MaterialStreamTypes.Contains(originalType))
            return StandardMaterialStreamType;

        if (EnergyStreamTypes.Contains(originalType))
            return StandardEnergyStreamType;

        return originalType;
    }

    public DwsimModelParser(ILogger<DwsimClient> logger, Dictionary<string, string> propMap, string dwsimInstallationPath)
        : this(logger, propMap, dwsimInstallationPath, null)
    {
    }

    protected DwsimModelParser(ILogger<DwsimClient> logger, Dictionary<string, string> propMap, string dwsimInstallationPath, dynamic? unitSystem)
    {
        _logger = logger;
        _propMap = propMap;
        _modelParsingConfig = new DwsimModelParsingConfig
        {
            MaxPropertiesPerNode = 100
        };

        // Allow injecting the unit system for testing purposes
        if (unitSystem != null)
        {
            _unitSystem = unitSystem;
        }
        else
        {
            // Initialize unit system using DWSIM unit conversion manager
            Type unitSystemType = GetDwsimType(dwsimInstallationPath, "DWSIM.SharedClasses.dll", "DWSIM.SharedClasses.SystemsOfUnits.Units");
            _unitSystem = Activator.CreateInstance(unitSystemType) ?? throw new InvalidOperationException("Failed to create unit system instance");
        }
    }

    /// <summary>
    /// Parses the DWSIM model and returns the flowsheet as SimulatorModelRevisionDataFlowsheet.
    /// This is the main orchestration method that combines XML parsing, COM property extraction,
    /// edge generation, and thermodynamic data extraction.
    /// </summary>
    /// <param name="sim">The DWSIM flowsheet COM object</param>
    /// <param name="filePath">Path to the DWXMZ file</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>Flowsheet object or null if parsing fails</returns>
    public virtual SimulatorModelRevisionDataFlowsheet? Parse(dynamic sim, string filePath, CancellationToken token)
    {
        _logger.LogDebug("DWSIM model parsing started for: {FilePath}", filePath);

        string? xmlFilePath = null;

        try
        {
            if (!ValidateFilePath(filePath))
                return null;

            xmlFilePath = ExtractXmlFromDwxmz(filePath);
            XDocument doc = XDocument.Load(xmlFilePath);

            _logger.LogDebug("Parsing simulation objects with properties");
            List<SimulatorModelRevisionDataObjectNode> nodes = ParseNodesWithProperties(doc, sim, token);
            _logger.LogDebug("Extracted {NodesCount} nodes from the model", nodes.Count);

            _logger.LogDebug("Generating flowsheet edges");
            IEnumerable<SimulatorModelRevisionDataObjectEdge> edges = GenerateFlowsheetEdgesFromXml(doc, nodes, _logger, _modelParsingConfig);

            // TODO: Remove EnsureAtLeastOneEdge call when API lifts the minItems restriction
            edges = EnsureAtLeastOneEdge(edges, nodes);

            _logger.LogDebug("Extracting thermodynamic data");
            SimulatorModelRevisionDataThermodynamic thermodynamics = ExtractThermodynamicDataFromXml(doc, _logger, _modelParsingConfig);

            var flowsheet = new SimulatorModelRevisionDataFlowsheet
            {
                SimulatorObjectNodes = nodes,
                SimulatorObjectEdges = edges,
                Thermodynamics = thermodynamics
            };

            _logger.LogDebug("DWSIM model parsing completed successfully");
            return flowsheet;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Model parsing was cancelled");
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while parsing DWSIM model: {EMessage}", e.Message);
            return null;
        }
        finally
        {
            if (!string.IsNullOrEmpty(xmlFilePath))
                CleanupTempFiles(xmlFilePath);
        }
    }

    /// <summary>
    /// Extracts XML file from DWXMZ archive
    /// </summary>
    /// <param name="filePath">Path to the DWXMZ file</param>
    /// <returns>Path to the extracted XML file</returns>
    protected virtual string ExtractXmlFromDwxmz(string filePath)
    {
        _logger.LogDebug("Extracting XML from DWXMZ file: {FilePath}", filePath);

        // Use same directory as the dwxmz file for temporary extraction
        string directory = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("Could not determine DWXMZ file directory");
        string tempDir = Path.Combine(directory, $"temp_{Path.GetFileNameWithoutExtension(filePath)}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            ZipFile.ExtractToDirectory(filePath, tempDir);

            string[] xmlFiles = Directory.GetFiles(tempDir, "*.xml");
            if (xmlFiles.Length == 0)
                throw new FileNotFoundException("No XML file found in DWXMZ archive");

            _logger.LogDebug("Found XML file: {FileName}", Path.GetFileName(xmlFiles[0]));
            return xmlFiles[0];
        }
        catch
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            throw;
        }
    }

    /// <summary>
    /// Cleans up temporary files created during XML extraction
    /// </summary>
    /// <param name="xmlFilePath">Path to the extracted XML file</param>
    protected virtual void CleanupTempFiles(string xmlFilePath)
    {
        try
        {
            string? tempDir = Path.GetDirectoryName(xmlFilePath);
            if (tempDir == null || !Directory.Exists(tempDir) || !Path.GetFileName(tempDir).StartsWith("temp_"))
                return;

            Directory.Delete(tempDir, true);
            _logger.LogDebug("Cleaned up temporary directory: {TempDir}", tempDir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to cleanup temporary files: {EMessage}", ex.Message);
        }
    }

    /// <summary>
    /// Gets a DWSIM type from the specified assembly
    /// </summary>
    /// <param name="dwsimInstallationPath">Path to DWSIM installation</param>
    /// <param name="assemblyName">Name of the assembly</param>
    /// <param name="typeName">Full name of the type</param>
    /// <returns>The requested type</returns>
    private static Type GetDwsimType(string dwsimInstallationPath, string assemblyName, string typeName)
    {
        string assemblyPath = Path.Combine(dwsimInstallationPath, assemblyName);
        Assembly assembly = Assembly.LoadFrom(assemblyPath);
        Type? type = assembly.GetType(typeName);
        return type ?? throw new InvalidOperationException($"Could not find type {typeName} in assembly {assemblyName}");
    }

    /// <summary>
    /// Validates a file path exists and is accessible
    /// </summary>
    /// <param name="filePath">The file path to validate</param>
    /// <returns>True if file exists and is accessible</returns>
    protected virtual bool ValidateFilePath(string filePath)
    {
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            return true;

        _logger.LogError("Cannot access file path: {FilePath}", filePath);
        return false;
    }

    /// <summary>
    /// Parses nodes from XML document and extracts properties from COM objects.
    /// This is an instance method that combines XML parsing with COM property extraction.
    /// </summary>
    /// <param name="doc">Parsed XML document</param>
    /// <param name="sim">DWSIM flowsheet COM object</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>List of parsed nodes with properties</returns>
    private List<SimulatorModelRevisionDataObjectNode> ParseNodesWithProperties(XDocument doc, dynamic sim, CancellationToken token)
    {
        var nodes = new List<SimulatorModelRevisionDataObjectNode>();

        try
        {
            List<XElement> simObjects = doc.Root?.Element("SimulationObjects")?.Elements("SimulationObject").ToList() ?? [];
            _logger.LogDebug("Found {Count} simulation objects in XML", simObjects.Count);

            List<XElement> graphicObjects = doc.Root?.Element("GraphicObjects")?.Elements("GraphicObject").ToList() ?? [];
            _logger.LogDebug("Found {Count} graphic objects in XML", graphicObjects.Count);

            Dictionary<string, XElement> graphicObjectLookup = graphicObjects
                .Where(g => g.Element("Name")?.Value != null)
                .GroupBy(g => g.Element("Name")!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (XElement simObj in simObjects)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    string? objName = simObj.Element("ComponentName")?.Value ??
                                     simObj.Element("Name")?.Value;

                    if (string.IsNullOrEmpty(objName))
                    {
                        _logger.LogDebug("Skipping simulation object without ComponentName or Name");
                        continue;
                    }

                    graphicObjectLookup.TryGetValue(objName, out XElement? graphicObj);

                    SimulatorModelRevisionDataObjectNode? node = CreateNodeFromXml(simObj, graphicObj, _logger, _modelParsingConfig);
                    if (node == null)
                        continue;

                    // Extract properties from COM object using GetFlowsheetSimulationObject
                    // This method takes the Tag (display name) as parameter, not the internal ComponentName
                    // https://dwsim.org/api_help/html/M_DWSIM_Interfaces_IFlowsheet_GetFlowsheetSimulationObject.htm
                    string nodeName = node.Name ?? objName;
                    try
                    {
                        dynamic? comObject = sim.GetFlowsheetSimulationObject(nodeName);
                        if (comObject != null)
                        {
                            try
                            {
                                string? productName = comObject.ProductName?.ToString();
                                if (!string.IsNullOrEmpty(productName))
                                    node.Type = EnsureMaxLength(productName, _modelParsingConfig.MaxNodeTypeLength);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug("Could not get ProductName for {NodeName}: {EMessage}", nodeName, ex.Message);
                            }

                            List<SimulatorModelRevisionDataProperty> properties =
                                ExtractNodePropertiesFromCom(comObject, node.Type ?? "Unknown", nodeName);
                            node.Properties = properties;
                            _logger.LogDebug("Extracted {PropCount} properties for node {NodeName}",
                                properties.Count, nodeName);
                        }
                        else
                        {
                            _logger.LogWarning("Could not get COM object for {NodeName}", nodeName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Could not extract properties for {NodeName}: {EMessage}", nodeName, ex.Message);
                    }

                    // TODO: Remove EnsureAtLeastOneProperty call when API lifts the minItems restriction
                    node.Properties = EnsureAtLeastOneProperty(node.Properties?.ToList() ?? []);

                    nodes.Add(node);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse simulation object, skipping node");
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse nodes with properties");
        }

        return nodes;
    }

    /// <summary>
    /// Parses nodes from XML document (static, XML-only version without COM property extraction)
    /// </summary>
    /// <param name="doc">Parsed XML document</param>
    /// <param name="logger">Logger for diagnostic messages</param>
    /// <param name="config">Optional parsing configuration for length limits</param>
    /// <returns>List of parsed nodes</returns>
    public static List<SimulatorModelRevisionDataObjectNode> ParseNodesFromXml(
        XDocument doc,
        ILogger? logger = null,
        DwsimModelParsingConfig? config = null)
    {
        config ??= new DwsimModelParsingConfig();

        var nodes = new List<SimulatorModelRevisionDataObjectNode>();

        try
        {
            List<XElement> simObjects = doc.Root?.Element("SimulationObjects")?.Elements("SimulationObject").ToList() ?? [];

            List<XElement> graphicObjects = doc.Root?.Element("GraphicObjects")?.Elements("GraphicObject").ToList() ?? [];

            Dictionary<string, XElement> graphicObjectLookup = graphicObjects
                .Where(g => g.Element("Name")?.Value != null)
                .GroupBy(g => g.Element("Name")!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (XElement simObj in simObjects)
            {
                try
                {
                    string? objName = simObj.Element("ComponentName")?.Value ??
                                     simObj.Element("Name")?.Value;

                    if (string.IsNullOrEmpty(objName))
                    {
                        logger?.LogDebug("Skipping simulation object without ComponentName or Name");
                        continue;
                    }

                    graphicObjectLookup.TryGetValue(objName, out XElement? graphicObj);

                    SimulatorModelRevisionDataObjectNode? node = CreateNodeFromXml(simObj, graphicObj, logger, config);
                    if (node != null)
                        nodes.Add(node);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to parse simulation object, skipping node");
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to parse XML document");
        }

        return nodes;
    }

    /// <summary>
    /// Creates a node from XML elements
    /// </summary>
    /// <param name="simObj">Simulation object XML element</param>
    /// <param name="graphicObj">Graphic object XML element</param>
    /// <param name="logger">Logger for diagnostic messages</param>
    /// <param name="config">Optional parsing configuration for length limits</param>
    /// <returns>Created node or null if creation fails</returns>
    public static SimulatorModelRevisionDataObjectNode? CreateNodeFromXml(
        XElement simObj,
        XElement? graphicObj,
        ILogger? logger = null,
        DwsimModelParsingConfig? config = null)
    {
        config ??= new DwsimModelParsingConfig();

        try
        {
            string? objectId = simObj.Element("ComponentName")?.Value ?? simObj.Element("Name")?.Value;
            string? objectType = simObj.Element("Type")?.Value?.Split('.').LastOrDefault();
            string? objectName = graphicObj?.Element("Tag")?.Value ?? objectId;

            if (string.IsNullOrEmpty(objectId) || string.IsNullOrEmpty(objectType))
                return null;

            // Normalize stream types to standardized names (e.g., "MaterialStream" -> "Material Stream")
            string normalizedType = NormalizeStreamType(objectType);

            string safeId = EnsureMaxLength(objectId, config.MaxNodeIdLength);
            string? safeName = objectName != null ? EnsureMaxLength(objectName, config.MaxNodeNameLength) : null;
            string safeType = EnsureMaxLength(normalizedType, config.MaxNodeTypeLength);

            var node = new SimulatorModelRevisionDataObjectNode
            {
                Id = safeId,
                Name = safeName,
                Type = safeType,
                Properties = new List<SimulatorModelRevisionDataProperty>(),
                GraphicalObject = CreateGraphicalObjectFromXml(graphicObj)
            };

            return node;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Unexpected error creating node from XML");
            return null;
        }
    }

    /// <summary>
    /// Generates flowsheet edges from XML graphic object connections
    /// </summary>
    /// <param name="doc">Parsed XML document</param>
    /// <param name="nodes">List of parsed nodes to reference for edge creation</param>
    /// <param name="logger">Logger for diagnostic messages</param>
    /// <param name="config">Optional parsing configuration for length limits</param>
    /// <returns>Collection of edges representing connections between nodes</returns>
    public static IEnumerable<SimulatorModelRevisionDataObjectEdge> GenerateFlowsheetEdgesFromXml(
        XDocument doc,
        List<SimulatorModelRevisionDataObjectNode> nodes,
        ILogger? logger = null,
        DwsimModelParsingConfig? config = null)
    {
        config ??= new DwsimModelParsingConfig();

        if (nodes == null || nodes.Count == 0)
            return [];

        var edges = new List<SimulatorModelRevisionDataObjectEdge>();

        try
        {
            // Build node lookup, handling potential null IDs and duplicates safely
            var duplicateIds = nodes
                .Where(n => n.Id != null)
                .GroupBy(n => n.Id!)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Count > 0)
            {
                logger?.LogWarning("Found duplicate node IDs in input: {DuplicateIds}. Using first occurrence of each.", string.Join(", ", duplicateIds));
            }

            Dictionary<string, SimulatorModelRevisionDataObjectNode> nodesByName = nodes
                .Where(n => n.Id != null)
                .GroupBy(n => n.Id!)
                .ToDictionary(g => g.Key, g => g.First());
            List<XElement> graphicObjects = doc.Root?.Element("GraphicObjects")?.Elements("GraphicObject").ToList() ?? [];

            foreach (XElement graphicObj in graphicObjects)
            {
                try
                {
                    string? sourceName = graphicObj.Element("Name")?.Value;
                    if (string.IsNullOrEmpty(sourceName))
                        continue;

                    // Apply EnsureMaxLength to lookup key to match processed node IDs
                    string safeSourceName = EnsureMaxLength(sourceName, config.MaxNodeIdLength);
                    if (!nodesByName.TryGetValue(safeSourceName, out SimulatorModelRevisionDataObjectNode? sourceNode))
                        continue;

                    IEnumerable<XElement>? outputConnectors = graphicObj.Element("OutputConnectors")?.Elements("Connector");
                    if (outputConnectors == null)
                        continue;

                    foreach (XElement connector in outputConnectors)
                    {
                        if (connector.Attribute("IsAttached")?.Value != "true")
                            continue;

                        string? connectedId = connector.Attribute("AttachedToObjID")?.Value;
                        if (string.IsNullOrEmpty(connectedId))
                            continue;

                        // Apply EnsureMaxLength to lookup key to match processed node IDs
                        string safeConnectedId = EnsureMaxLength(connectedId, config.MaxNodeIdLength);
                        if (!nodesByName.TryGetValue(safeConnectedId, out SimulatorModelRevisionDataObjectNode? targetNode))
                            continue;

                        SimulatorModelRevisionDataConnectionType connectionType = DetermineConnectionType(sourceNode, targetNode);

                        string edgeId = EnsureMaxLength($"{sourceNode.Id}_{targetNode.Id}", config.MaxEdgeIdLength);

                        var edge = new SimulatorModelRevisionDataObjectEdge
                        {
                            Id = edgeId,
                            Name = $"{sourceNode.Name} -> {targetNode.Name}",
                            SourceId = sourceNode.Id,
                            TargetId = targetNode.Id,
                            ConnectionType = connectionType
                        };
                        edges.Add(edge);
                    }
                }
                catch (Exception e)
                {
                    logger?.LogDebug("Error processing connections for graphic object: {EMessage}", e.Message);
                }
            }

            edges = RemoveDuplicateEdges(edges, logger);
            logger?.LogDebug("Generated {EdgesCount} edges from XML connections", edges.Count);
        }
        catch (Exception e)
        {
            logger?.LogWarning(e, "Error generating flowsheet edges from XML");
        }

        return edges;
    }

    /// <summary>
    /// Determines the connection type based on source and target node types.
    /// </summary>
    private static SimulatorModelRevisionDataConnectionType DetermineConnectionType(
        SimulatorModelRevisionDataObjectNode sourceNode,
        SimulatorModelRevisionDataObjectNode targetNode)
    {
        if (sourceNode.Type == StandardEnergyStreamType || targetNode.Type == StandardEnergyStreamType)
            return SimulatorModelRevisionDataConnectionType.Energy;

        if (sourceNode.Type == StandardMaterialStreamType || targetNode.Type == StandardMaterialStreamType)
            return SimulatorModelRevisionDataConnectionType.Material;

        return SimulatorModelRevisionDataConnectionType.Information;
    }

    /// <summary>
    /// Removes duplicate edges based on source and target IDs
    /// </summary>
    private static List<SimulatorModelRevisionDataObjectEdge> RemoveDuplicateEdges(
        List<SimulatorModelRevisionDataObjectEdge> edges,
        ILogger? logger = null)
    {
        var uniqueEdges = new Dictionary<string, SimulatorModelRevisionDataObjectEdge>();

        foreach (SimulatorModelRevisionDataObjectEdge edge in edges)
        {
            string key = $"{edge.SourceId}_{edge.TargetId}";
            uniqueEdges.TryAdd(key, edge);
        }

        if (edges.Count != uniqueEdges.Count)
            logger?.LogDebug("Removed {Count} duplicate edges", edges.Count - uniqueEdges.Count);

        return [.. uniqueEdges.Values];
    }

    /// <summary>
    /// Creates a graphical object from XML element
    /// </summary>
    /// <param name="graphicObj">Graphic object XML element</param>
    /// <returns>Created graphical object or null if graphicObj is null</returns>
    public static SimulatorModelRevisionDataGraphicalObject? CreateGraphicalObjectFromXml(XElement? graphicObj)
    {
        if (graphicObj == null)
            return null;

        SimulatorModelRevisionDataPosition? position = null;
        string? xElement = graphicObj.Element("X")?.Value;
        string? yElement = graphicObj.Element("Y")?.Value;
        if (double.TryParse(xElement, NumberStyles.Any, CultureInfo.InvariantCulture, out double x) &&
            double.TryParse(yElement, NumberStyles.Any, CultureInfo.InvariantCulture, out double y))
        {
            position = new SimulatorModelRevisionDataPosition
            {
                X = x,
                Y = y
            };
        }

        string? widthElement = graphicObj.Element("Width")?.Value;
        string? heightElement = graphicObj.Element("Height")?.Value;
        string? rotationElement = graphicObj.Element("Rotation")?.Value;
        string? activeElement = graphicObj.Element("Active")?.Value;

        return new SimulatorModelRevisionDataGraphicalObject
        {
            Position = position,
            Width = double.TryParse(widthElement, NumberStyles.Any, CultureInfo.InvariantCulture, out double w) ? w : null,
            Height = double.TryParse(heightElement, NumberStyles.Any, CultureInfo.InvariantCulture, out double h) ? h : null,
            Angle = double.TryParse(rotationElement, NumberStyles.Any, CultureInfo.InvariantCulture, out double angle) ? angle : null,
            // TODO: Add ScaleX/ScaleY once SDK is updated to use double type instead of bool
            // ScaleX should be -1.0 when FlippedH is true, 1.0 when false
            // ScaleY should be -1.0 when FlippedV is true, 1.0 when false
            Active = bool.TryParse(activeElement, out bool active) ? active : null
        };
    }

    /// <summary>
    /// Extracts thermodynamic data (compounds and property packages) from XML
    /// </summary>
    /// <param name="doc">Parsed XML document</param>
    /// <param name="logger">Logger for diagnostic messages</param>
    /// <param name="config">Optional parsing configuration for length limits</param>
    /// <returns>Thermodynamic data containing components and property packages</returns>
    public static SimulatorModelRevisionDataThermodynamic ExtractThermodynamicDataFromXml(
        XDocument doc,
        ILogger? logger = null,
        DwsimModelParsingConfig? config = null)
    {
        config ??= new DwsimModelParsingConfig();

        var components = new List<string>();
        var propertyPackages = new List<string>();

        try
        {
            IEnumerable<XElement>? compoundElements = doc.Root?.Element("Compounds")?.Elements("Compound");
            if (compoundElements != null)
            {
                foreach (XElement compound in compoundElements)
                {
                    string? componentName = compound.Element("Name")?.Value;
                    if (!string.IsNullOrEmpty(componentName))
                        components.Add(EnsureMaxLength(componentName, config.MaxThermodynamicStringLength));
                }
            }

            IEnumerable<XElement>? propertyPackageElements = doc.Root?.Element("PropertyPackages")?.Elements("PropertyPackage");
            if (propertyPackageElements != null)
            {
                foreach (XElement package in propertyPackageElements)
                {
                    string? packageName = package.Element("ComponentName")?.Value;
                    if (!string.IsNullOrEmpty(packageName))
                        propertyPackages.Add(EnsureMaxLength(packageName, config.MaxThermodynamicStringLength));
                }
            }

            logger?.LogDebug("Extracted {ComponentsCount} components and {PackagesCount} property packages from XML",
                components.Count, propertyPackages.Count);
        }
        catch (Exception e)
        {
            logger?.LogWarning(e, "Error extracting thermodynamic data from XML");
        }

        return new SimulatorModelRevisionDataThermodynamic
        {
            Components = components,
            PropertyPackages = propertyPackages
        };
    }

    /// <summary>
    /// Extracts properties from a DWSIM COM object
    /// </summary>
    /// <param name="obj">The COM object to extract properties from</param>
    /// <param name="objectType">Type of the object</param>
    /// <param name="nodeName">Name of the node</param>
    /// <returns>List of extracted properties</returns>
    internal List<SimulatorModelRevisionDataProperty> ExtractNodePropertiesFromCom(
        dynamic obj, string objectType, string nodeName)
    {
        var properties = new List<SimulatorModelRevisionDataProperty>();
        int extractedCount = 0;

        try
        {
            // Get write and read properties using GetProperties method exposed by the BaseClass interface
            // https://dwsim.org/api_help/html/T_DWSIM_SharedClasses_UnitOperations_BaseClass.htm
            // GetProperties(1) returns only properties that can be written to
            // GetProperties(3) returns all properties that can be read from
            dynamic? writeProperties = obj.GetProperties(1);
            dynamic? readProperties = obj.GetProperties(3);

            var allProperties = new HashSet<string>();
            if (writeProperties != null)
                foreach (string prop in writeProperties) allProperties.Add(prop);

            if (readProperties != null)
                foreach (string prop in readProperties) allProperties.Add(prop);

            foreach (string property in allProperties)
            {
                if (extractedCount >= _modelParsingConfig.MaxPropertiesPerNode)
                {
                    _logger.LogDebug("Reached max properties limit ({MaxProperties}) for node {NodeName}",
                        _modelParsingConfig.MaxPropertiesPerNode, nodeName);
                    break;
                }

                try
                {
                    SimulatorModelRevisionDataProperty? modelProperty =
                        CreateModelProperty(property, obj, objectType, nodeName, writeProperties);
                    if (modelProperty != null)
                    {
                        properties.Add(modelProperty);
                        extractedCount++;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogDebug("Skipped property {PropName} for node {NodeName}: {EMessage}",
                        property, nodeName, e.Message);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning("Error extracting properties from COM object for node {NodeName}: {EMessage}",
                nodeName, e.Message);
        }

        return properties;
    }

    /// <summary>
    /// Creates a model property from a DWSIM COM object property
    /// </summary>
    /// <param name="propertyKey">The property key</param>
    /// <param name="obj">The COM object</param>
    /// <param name="objectType">Type of the object</param>
    /// <param name="objectName">Name of the object</param>
    /// <param name="writeProperties">List of writable properties</param>
    /// <returns>Created property or null if property cannot be processed</returns>
    internal SimulatorModelRevisionDataProperty? CreateModelProperty(
        string propertyKey, dynamic obj, string objectType,
        string objectName, dynamic writeProperties)
    {
        try
        {
            // https://dwsim.org/api_help/html/M_DWSIM_SharedClasses_UnitOperations_BaseClass_GetPropertyValue.htm
            dynamic value = obj.GetPropertyValue(propertyKey);
            if (value == null)
                return null;

            SimulatorValue? simulatorValue = ConvertToSimulatorValue(value, propertyKey);
            if (simulatorValue == null)
                return null;

            bool isReadOnly = writeProperties is null || !((IList)writeProperties).Contains(propertyKey);

            string safeName = EnsureMaxLength(GetHumanReadablePropertyName(propertyKey) ?? propertyKey, _modelParsingConfig.MaxPropertyNameLength);

            return new SimulatorModelRevisionDataProperty
            {
                Name = safeName,
                ValueType = simulatorValue.Type,
                Value = simulatorValue,
                Unit = GetUnitReference(obj, propertyKey),
                ReadOnly = isReadOnly,
                ReferenceObject = new Dictionary<string, string>
                {
                    { "objectType", objectType },
                    { "objectName", objectName },
                    { "objectProperty", propertyKey }
                }
            };
        }
        catch (Exception e)
        {
            _logger.LogDebug("Error creating model property for {PropKey}: {EMessage}", propertyKey, e.Message);
            return null;
        }
    }

    /// <summary>
    /// Translates DWSIM internal property keys to human-readable names.
    /// Example: "PROP_MS_0" -> "Temperature", "PROP_MS_105/Oxygen" -> "Mass Flow (Mixture)/Oxygen"
    /// https://dwsim.org/wiki/index.php?title=Object_Property_Codes
    /// </summary>
    internal string GetHumanReadablePropertyName(string propertyKey)
    {
        if (!propertyKey.StartsWith("PROP"))
            return propertyKey;

        // Mixture properties contain a suffix after a slash (e.g., PROP_MS_105/Oxygen)
        string[] parts = propertyKey.Split('/', 2);
        string propCode = parts[0];
        string? suffix = parts.Length > 1 ? parts[1] : null;

        if (_propMap.TryGetValue(propCode, out string? humanReadableName))
        {
            return suffix != null ? $"{humanReadableName}/{suffix}" : humanReadableName;
        }
        return propertyKey;
    }

    /// <summary>
    /// Gets unit reference information from a COM object for a given property.
    /// </summary>
    internal SimulatorValueUnitReference? GetUnitReference(dynamic obj, string propertyKey)
    {
        // https://dwsim.org/api_help/html/M_DWSIM_SharedClasses_UnitOperations_BaseClass_GetPropertyUnit.htm
        string unit = obj.GetPropertyUnit(propertyKey) ?? "";
        if (string.IsNullOrEmpty(unit))
            return null;

        // Map unit to unit type using DWSIM unit system
        // https://dwsim.org/api_help/html/M_DWSIM_SharedClasses_SystemsOfUnits_Units_GetUnitType.htm
        string unitType = _unitSystem?.GetUnitType(unit)?.ToString() ?? "";
        if (unitType == "none" || string.IsNullOrEmpty(unitType))
            return null;

        return new SimulatorValueUnitReference { Name = unit, Quantity = unitType };
    }

    /// <summary>
    /// Converts a raw COM property value to a SimulatorValue.
    /// Returns null for invalid values (NaN, Infinity, empty arrays, unsupported types).
    /// </summary>
    internal SimulatorValue? ConvertToSimulatorValue(dynamic value, string propertyKey)
    {
        switch (value)
        {
            case double d when double.IsNaN(d) || double.IsInfinity(d):
            case float f when float.IsNaN(f) || float.IsInfinity(f):
            case Array { Length: 0 }:
                return null;

            case double d:
                return SimulatorValue.Create(d);
            case float f:
                return SimulatorValue.Create((double)f);
            case int i:
                return SimulatorValue.Create(i);
            // TODO: Update logic if we extend the API to support boolean type natively
            case bool b:
                return SimulatorValue.Create(b ? 1.0 : 0.0);
            case string s:
                return SimulatorValue.Create(s);

            case Array arr:
                // All elements in arrays are of the same type in DWSIM
                object? first = arr.GetValue(0);
                if (first is double or float or int)
                {
                    double[] doubles = new double[arr.Length];
                    for (int i = 0; i < arr.Length; i++)
                    {
                        double val = Convert.ToDouble(arr.GetValue(i));
                        if (double.IsNaN(val) || double.IsInfinity(val))
                        {
                            _logger.LogDebug("Array for property {PropKey} contains invalid double value at index {Index}", propertyKey, i);
                            return null;
                        }
                        doubles[i] = val;
                    }
                    return SimulatorValue.Create(doubles);
                }
                else
                {
                    string[] strings = new string[arr.Length];
                    for (int i = 0; i < arr.Length; i++)
                        strings[i] = arr.GetValue(i)?.ToString() ?? "";
                    return SimulatorValue.Create(strings);
                }

            default:
                string typeName = value.GetType().Name;
                _logger.LogDebug("Unsupported value type {ValueType} for property {PropKey}",
                    typeName, propertyKey);
                return null;
        }
    }

    #region API Constraints

    /// <summary>
    /// Ensures a string is at most maxLength characters long. If longer, creates a SHA256 hash.
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <param name="maxLength">Maximum allowed length</param>
    /// <returns>Original string if within limit, otherwise a hash of the string</returns>
    private static string EnsureMaxLength(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        // Create a hash of the full value to ensure uniqueness
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        // Convert to base64 and remove special characters to make it URL-safe
        string hash = Convert.ToBase64String(hashBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");

        // Take first maxLength characters of the hash (ensures uniqueness)
        return hash[..Math.Min(hash.Length, maxLength)];
    }

    /// <summary>
    /// Ensures at least one property exists for a node.
    /// TODO: Remove this workaround when API lifts the minItems restriction on properties.
    /// </summary>
    /// <param name="properties">The list of properties to check</param>
    /// <returns>Original list if not empty, otherwise a list with a placeholder property</returns>
    private static List<SimulatorModelRevisionDataProperty> EnsureAtLeastOneProperty(
        List<SimulatorModelRevisionDataProperty> properties)
    {
        if (properties.Count > 0)
            return properties;

        properties.Add(new SimulatorModelRevisionDataProperty
        {
            Name = "_placeholder",
            ValueType = SimulatorValueType.STRING,
            Value = SimulatorValue.Create("No properties available"),
            ReadOnly = true,
            ReferenceObject = new Dictionary<string, string>
            {
                { "objectType", "Placeholder" },
                { "objectName", "Placeholder" },
                { "objectProperty", "_placeholder" }
            }
        });

        return properties;
    }

    /// <summary>
    /// Ensures at least one edge exists in the flowsheet.
    /// TODO: Remove this workaround when API lifts the minItems restriction on edges.
    /// </summary>
    /// <param name="edges">The collection of edges to check</param>
    /// <param name="nodes">The list of nodes (used to create placeholder edge if needed)</param>
    /// <returns>Original edges if not empty, otherwise a collection with a placeholder edge</returns>
    private static IEnumerable<SimulatorModelRevisionDataObjectEdge> EnsureAtLeastOneEdge(
        IEnumerable<SimulatorModelRevisionDataObjectEdge> edges,
        List<SimulatorModelRevisionDataObjectNode> nodes)
    {
        List<SimulatorModelRevisionDataObjectEdge> edgeList = edges.ToList();
        if (edgeList.Count > 0)
            return edgeList;

        if (nodes.Count > 0 && nodes[0].Id != null)
        {
            edgeList.Add(new SimulatorModelRevisionDataObjectEdge
            {
                Id = "_placeholder",
                Name = "Placeholder Edge",
                SourceId = nodes[0].Id,
                TargetId = nodes[0].Id,
                ConnectionType = SimulatorModelRevisionDataConnectionType.Information
            });
        }

        return edgeList;
    }

    #endregion
}

/// <summary>
/// Configuration class for DWSIM model parsing.
/// Default values are based on the Simulators API constraints.
/// </summary>
public class DwsimModelParsingConfig
{
    // Item count limits
    public int MaxNodesPerFlowsheet { get; set; } = 2000;
    public int MaxEdgesPerFlowsheet { get; set; } = 2000;
    public int MaxPropertiesPerNode { get; set; } = 100;

    // Character length limits for identifiers and names
    public int MaxNodeIdLength { get; set; } = 50;
    public int MaxNodeNameLength { get; set; } = 50;
    public int MaxNodeTypeLength { get; set; } = 50;
    public int MaxEdgeIdLength { get; set; } = 50;
    public int MaxPropertyNameLength { get; set; } = 50;
    public int MaxThermodynamicStringLength { get; set; } = 50;
}
