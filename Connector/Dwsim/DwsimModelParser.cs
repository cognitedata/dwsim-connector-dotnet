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
            // Extract the DWXMZ file
            ZipFile.ExtractToDirectory(filePath, tempDir);

            // Find the XML file
            string[] xmlFiles = Directory.GetFiles(tempDir, "*.xml");
            if (xmlFiles.Length == 0)
                throw new FileNotFoundException("No XML file found in DWXMZ archive");

            _logger.LogDebug("Found XML file: {FileName}", Path.GetFileName(xmlFiles[0]));
            return xmlFiles[0];
        }
        catch
        {
            // Clean up on error
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
    /// Parses nodes from XML document
    /// </summary>
    /// <param name="doc">Parsed XML document</param>
    /// <param name="logger">Logger for diagnostic messages</param>
    /// <returns>List of parsed nodes</returns>
    public static List<SimulatorModelRevisionDataObjectNode> ParseNodesFromXml(XDocument doc, ILogger? logger = null)
    {
        var nodes = new List<SimulatorModelRevisionDataObjectNode>();

        try
        {
            // Parse SimulationObjects - only direct children of SimulationObjects element
            List<XElement> simObjects = doc.Root?.Element("SimulationObjects")?.Elements("SimulationObject").ToList() ?? [];

            // Parse GraphicObjects - scope to GraphicObjects parent element
            List<XElement> graphicObjects = doc.Root?.Element("GraphicObjects")?.Elements("GraphicObject").ToList() ?? [];

            // Create a lookup for graphic objects by Name, handling duplicates by taking first occurrence
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

                    // Find corresponding graphic object
                    graphicObjectLookup.TryGetValue(objName, out XElement? graphicObj);

                    SimulatorModelRevisionDataObjectNode? node = CreateNodeFromXml(simObj, graphicObj, logger);
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
    /// <returns>Created node or null if creation fails</returns>
    public static SimulatorModelRevisionDataObjectNode? CreateNodeFromXml(XElement simObj, XElement? graphicObj, ILogger? logger = null)
    {
        try
        {
            // Get basic object information from XML
            string? objectId = simObj.Element("ComponentName")?.Value ?? simObj.Element("Name")?.Value;
            string? objectType = simObj.Element("Type")?.Value?.Split('.').LastOrDefault();
            string? objectName = graphicObj?.Element("Tag")?.Value ?? objectId;

            // Skip nodes without required properties
            if (string.IsNullOrEmpty(objectId) || string.IsNullOrEmpty(objectType))
                return null;

            // Normalize stream types to standardized names (e.g., "MaterialStream" -> "Material Stream")
            string normalizedType = NormalizeStreamType(objectType);

            // Create node with basic information
            var node = new SimulatorModelRevisionDataObjectNode
            {
                Id = objectId,
                Name = objectName,
                Type = normalizedType,
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
    /// <returns>Collection of edges representing connections between nodes</returns>
    public static IEnumerable<SimulatorModelRevisionDataObjectEdge> GenerateFlowsheetEdgesFromXml(
        XDocument doc,
        List<SimulatorModelRevisionDataObjectNode> nodes,
        ILogger? logger = null)
    {
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
                    if (string.IsNullOrEmpty(sourceName) || !nodesByName.TryGetValue(sourceName, out SimulatorModelRevisionDataObjectNode? sourceNode))
                        continue;

                    // Parse output connections from XML
                    IEnumerable<XElement>? outputConnectors = graphicObj.Element("OutputConnectors")?.Elements("Connector");
                    if (outputConnectors == null)
                        continue;

                    foreach (XElement connector in outputConnectors)
                    {
                        if (connector.Attribute("IsAttached")?.Value != "true")
                            continue;

                        string? connectedId = connector.Attribute("AttachedToObjID")?.Value;
                        if (string.IsNullOrEmpty(connectedId) || !nodesByName.TryGetValue(connectedId, out SimulatorModelRevisionDataObjectNode? targetNode))
                            continue;

                        SimulatorModelRevisionDataConnectionType connectionType = DetermineConnectionType(sourceNode, targetNode);
                        var edge = new SimulatorModelRevisionDataObjectEdge
                        {
                            Id = $"{sourceName}_{connectedId}",
                            Name = $"{sourceNode.Name} -> {targetNode.Name}",
                            SourceId = sourceName,
                            TargetId = connectedId,
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

            // Remove duplicate edges
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
        // Check if it's an energy stream
        if (sourceNode.Type == StandardEnergyStreamType || targetNode.Type == StandardEnergyStreamType)
            return SimulatorModelRevisionDataConnectionType.Energy;

        // Check if it's a material stream
        if (sourceNode.Type == StandardMaterialStreamType || targetNode.Type == StandardMaterialStreamType)
            return SimulatorModelRevisionDataConnectionType.Material;

        // Default to information for other connections
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

        // Only create position if both X and Y are present and can be parsed
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
    /// <returns>Thermodynamic data containing components and property packages</returns>
    public static SimulatorModelRevisionDataThermodynamic ExtractThermodynamicDataFromXml(XDocument doc, ILogger? logger = null)
    {
        var components = new List<string>();
        var propertyPackages = new List<string>();

        try
        {
            // Extract components from Compounds section
            IEnumerable<XElement>? compoundElements = doc.Root?.Element("Compounds")?.Elements("Compound");
            if (compoundElements != null)
            {
                foreach (XElement compound in compoundElements)
                {
                    string? componentName = compound.Element("Name")?.Value;
                    if (!string.IsNullOrEmpty(componentName))
                        components.Add(componentName);
                }
            }

            // Extract property packages from PropertyPackages section
            IEnumerable<XElement>? propertyPackageElements = doc.Root?.Element("PropertyPackages")?.Elements("PropertyPackage");
            if (propertyPackageElements != null)
            {
                foreach (XElement package in propertyPackageElements)
                {
                    string? packageName = package.Element("ComponentName")?.Value;
                    if (!string.IsNullOrEmpty(packageName))
                        propertyPackages.Add(packageName);
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

            // Combine all properties, avoiding duplicates
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
            // Get property value from COM object
            // https://dwsim.org/api_help/html/M_DWSIM_SharedClasses_UnitOperations_BaseClass_GetPropertyValue.htm
            dynamic value = obj.GetPropertyValue(propertyKey);
            if (value == null)
                return null;

            SimulatorValue? simulatorValue = ConvertToSimulatorValue(value, propertyKey);
            if (simulatorValue == null)
                return null;

            bool isReadOnly = writeProperties is null || !((IList)writeProperties).Contains(propertyKey);

            return new SimulatorModelRevisionDataProperty
            {
                Name = GetHumanReadablePropertyName(propertyKey),
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
}

/// <summary>
/// Configuration class for DWSIM model parsing
/// </summary>
public class DwsimModelParsingConfig
{
    // will be used later when we start to extract node properties
    public int MaxPropertiesPerNode { get; set; } = 100;
}
