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

    public DwsimModelParser(ILogger<DwsimClient> logger, Dictionary<string, string> propMap, string dwsimInstallationPath)
        : this(logger, propMap, dwsimInstallationPath, null)
    {
    }

    // Constructor for testing with injectable unit system
    protected DwsimModelParser(ILogger<DwsimClient> logger, Dictionary<string, string> propMap, string dwsimInstallationPath, dynamic? unitSystem)
    {
        _logger = logger;
        _propMap = propMap;
        _modelParsingConfig = new DwsimModelParsingConfig
        {
            MaxPropertiesPerNode = 100
        };

        if (unitSystem != null)
        {
            _unitSystem = unitSystem;
        }
        else
        {
            // Initialize unit system
            Type unitSystemType = GetDwsimType(dwsimInstallationPath, "DWSIM.SharedClasses.dll", "DWSIM.SharedClasses.SystemsOfUnits.Units");
            _unitSystem = Activator.CreateInstance(unitSystemType) ?? throw new InvalidOperationException("Failed to create unit system instance");
        }
    }

    /// <summary>
    /// Parses the DWSIM model and returns the flowsheet as SimulatorModelRevisionDataFlowsheet
    /// </summary>
    /// <param name="sim">The DWSIM flowsheet interface</param>
    /// <param name="filePath">Path to the DWXMZ file</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>FlowSheet object or null if parsing fails</returns>
    public virtual SimulatorModelRevisionDataFlowsheet? Parse(dynamic sim, string filePath, CancellationToken token)
    {
        _logger.LogDebug("DWSIM model parsing started");

        string? xmlFilePath = null;

        try
        {
            // Validate the provided file path
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                _logger.LogError("Cannot access DWXMZ file path: {FilePath}", filePath);
                return null;
            }

            // Extract XML from DWXMZ file
            _logger.LogDebug("Extracting XML from DWXMZ file: {FilePath}", filePath);
            xmlFilePath = ExtractXmlFromDwxmz(filePath);

            // Parse nodes
            _logger.LogDebug("Parsing simulation objects");
            List<SimulatorModelRevisionDataObjectNode> nodes = ParseNodesFromXml(xmlFilePath, sim, token);

            _logger.LogDebug("Extracted {NodesCount} nodes from the model", nodes.Count);

            // Generate edges from connections
            _logger.LogDebug("Generating flowsheet edges");
            IEnumerable<SimulatorModelRevisionDataObjectEdge> edges = GenerateFlowsheetEdgesFromXml(xmlFilePath, nodes, _logger);

            // Extract thermodynamic data
            SimulatorModelRevisionDataThermodynamic thermodynamics = ExtractThermodynamicDataFromXml(xmlFilePath, _logger);

            var flowsheet = new SimulatorModelRevisionDataFlowsheet
            {
                SimulatorObjectNodes = nodes,
                SimulatorObjectEdges = edges,
                Thermodynamics = thermodynamics
            };

            _logger.LogDebug("DWSIM model parsing completed successfully");
            return flowsheet;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while parsing DWSIM model: {EMessage}", e.Message);
            return null;
        }
        finally
        {
            // Always cleanup temporary files
            if (!string.IsNullOrEmpty(xmlFilePath))
                CleanupTempFiles(xmlFilePath);
        }
    }

    private string ExtractXmlFromDwxmz(string dwxmzPath)
    {
        _logger.LogDebug("Extracting XML from DWXMZ file: {FilePath}", dwxmzPath);

        // Use same directory as the dwxmz file for temporary extraction
        string dwxmzDirectory = Path.GetDirectoryName(dwxmzPath) ?? throw new InvalidOperationException("Could not determine DWXMZ file directory");
        string tempDir = Path.Combine(dwxmzDirectory, $"temp_{Path.GetFileNameWithoutExtension(dwxmzPath)}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Extract the DWXMZ file
            ZipFile.ExtractToDirectory(dwxmzPath, tempDir);

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

    private void CleanupTempFiles(string xmlFilePath)
    {
        try
        {
            string? tempDir = Path.GetDirectoryName(xmlFilePath);
            if (!Directory.Exists(tempDir))
                return;

            // Only delete directories that match our temp pattern: temp_<filename>_<guid>
            string dirName = Path.GetFileName(tempDir);
            if (!dirName.StartsWith("temp_") || !dirName.Contains("_"))
                return;

            _logger.LogDebug("Cleaning up temporary files from: {TempDir}", tempDir);
            Directory.Delete(tempDir, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to cleanup temporary files: {EMessage}", ex.Message);
        }
    }

    private List<SimulatorModelRevisionDataObjectNode> ParseNodesFromXml(string xmlFilePath, dynamic sim, CancellationToken token)
    {
        _logger.LogDebug("Parsing XML flowsheet from: {FilePath}", xmlFilePath);

        var nodes = new List<SimulatorModelRevisionDataObjectNode>();

        try
        {
            XDocument doc = XDocument.Load(xmlFilePath);

            // Parse SimulationObjects
            List<XElement> simObjects = doc.Descendants("SimulationObject").ToList();
            _logger.LogDebug("Found {Count} simulation objects in XML", simObjects.Count);

            // Parse GraphicObjects
            List<XElement> graphicObjects = doc.Descendants("GraphicObjects").FirstOrDefault()?.Elements("GraphicObject").ToList() ?? [];
            _logger.LogDebug("Found {Count} graphic objects in XML", graphicObjects.Count);

            // Create a lookup for graphic objects by Name
            Dictionary<string, XElement> graphicObjectLookup = graphicObjects
                .Where(g => g.Element("Name")?.Value != null)
                .ToDictionary(g => g.Element("Name")!.Value, g => g);

            foreach (XElement simObj in simObjects)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    string objName = simObj.Element("ComponentName")?.Value ??
                                   simObj.Element("Name")?.Value ??
                                   $"Object_{nodes.Count}";

                    // Find corresponding graphic object
                    graphicObjectLookup.TryGetValue(objName, out XElement? graphicObj);

                    dynamic? node = CreateNodeFromXml(simObj, graphicObj, sim);
                    if (node != null)
                    {
                        nodes.Add(node);
                        _logger.LogDebug("Processed object from XML: {Name} ({Type})", (string)node.Name, (string)node.Type);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error processing simulation object: {EMessage}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error parsing XML: {EMessage}", ex.Message);
            throw;
        }

        return nodes;
    }

    public static List<SimulatorModelRevisionDataObjectNode> ParseNodesFromXmlOnly(string xmlFilePath)
    {
        var nodes = new List<SimulatorModelRevisionDataObjectNode>();

        try
        {
            XDocument doc = XDocument.Load(xmlFilePath);

            // Parse SimulationObjects
            List<XElement> simObjects = doc.Descendants("SimulationObject").ToList();

            // Parse GraphicObjects
            List<XElement> graphicObjects = doc.Descendants("GraphicObjects").FirstOrDefault()?.Elements("GraphicObject").ToList() ?? [];

            // Create a lookup for graphic objects by Name
            Dictionary<string, XElement> graphicObjectLookup = graphicObjects
                .Where(g => g.Element("Name")?.Value != null)
                .ToDictionary(g => g.Element("Name")!.Value, g => g);

            foreach (XElement simObj in simObjects)
            {
                try
                {
                    string objName = simObj.Element("ComponentName")?.Value ??
                                   simObj.Element("Name")?.Value ??
                                   $"Object_{nodes.Count}";

                    // Find corresponding graphic object
                    graphicObjectLookup.TryGetValue(objName, out XElement? graphicObj);

                    SimulatorModelRevisionDataObjectNode? node = CreateNodeFromXmlOnly(simObj, graphicObj);
                    if (node != null)
                    {
                        nodes.Add(node);
                    }
                }
                catch
                {
                    // Skip problematic nodes
                }
            }
        }
        catch
        {
            // Return empty list on error
        }

        return nodes;
    }

    public static SimulatorModelRevisionDataObjectNode? CreateNodeFromXmlOnly(XElement simObj, XElement? graphicObj)
    {
        try
        {
            // Get basic object information from XML
            string objectName = graphicObj?.Element("Tag")?.Value ??
                              simObj.Element("ComponentName")?.Value ??
                              "Unknown";
            string objectType = simObj.Element("Type")?.Value.Split('.').LastOrDefault() ?? "Unknown";
            string objectId = simObj.Element("ComponentName")?.Value ??
                            simObj.Element("Name")?.Value ??
                            Guid.NewGuid().ToString();

            // Create node with basic information
            var node = new SimulatorModelRevisionDataObjectNode
            {
                Id = objectId,
                Name = objectName,
                Type = objectType,
                Properties = new List<SimulatorModelRevisionDataProperty>()
            };

            // Set graphic properties from XML if available
            if (graphicObj != null)
            {
                node.GraphicalObject = new SimulatorModelRevisionDataGraphicalObject
                {
                    Position = new SimulatorModelRevisionDataPosition
                    {
                        X = double.Parse(graphicObj.Element("X")?.Value ?? "0"),
                        Y = double.Parse(graphicObj.Element("Y")?.Value ?? "0")
                    },
                    Width = double.Parse(graphicObj.Element("Width")?.Value ?? "20"),
                    Height = double.Parse(graphicObj.Element("Height")?.Value ?? "20"),
                    Angle = double.Parse(graphicObj.Element("Rotation")?.Value ?? "0"),
                    ScaleX = bool.Parse(graphicObj.Element("FlippedH")?.Value ?? "false"),
                    ScaleY = bool.Parse(graphicObj.Element("FlippedV")?.Value ?? "false"),
                    Active = bool.Parse(graphicObj.Element("Active")?.Value ?? "true")
                };
            }

            return node;
        }
        catch
        {
            return null;
        }
    }

    private SimulatorModelRevisionDataObjectNode? CreateNodeFromXml(XElement simObj, XElement? graphicObj, dynamic sim)
    {
        try
        {
            // Get basic object information from XML
            string objectName = graphicObj?.Element("Tag")?.Value ??
                              simObj.Element("ComponentName")?.Value ??
                              "Unknown";
            string objectType = simObj.Element("Type")?.Value.Split('.').LastOrDefault() ?? "Unknown";
            string objectId = simObj.Element("ComponentName")?.Value ??
                            simObj.Element("Name")?.Value ??
                            Guid.NewGuid().ToString();

            // Create node with basic information
            var node = new SimulatorModelRevisionDataObjectNode
            {
                Id = objectId,
                Name = objectName,
                Type = objectType
            };

            // Set graphic properties from XML if available
            if (graphicObj != null)
            {
                node.GraphicalObject = new SimulatorModelRevisionDataGraphicalObject
                {
                    Position = new SimulatorModelRevisionDataPosition
                    {
                        X = double.Parse(graphicObj.Element("X")?.Value ?? "0"),
                        Y = double.Parse(graphicObj.Element("Y")?.Value ?? "0")
                    },
                    Width = double.Parse(graphicObj.Element("Width")?.Value ?? "20"),
                    Height = double.Parse(graphicObj.Element("Height")?.Value ?? "20"),
                    Angle = double.Parse(graphicObj.Element("Rotation")?.Value ?? "0"),
                    ScaleX = bool.Parse(graphicObj.Element("FlippedH")?.Value ?? "false"),
                    ScaleY = bool.Parse(graphicObj.Element("FlippedV")?.Value ?? "false"),
                    Active = bool.Parse(graphicObj.Element("Active")?.Value ?? "true")
                };
            }

            // Extract properties using COM (not available in XML)
            try
            {
                // Try to get the COM object for this simulation object
                dynamic? comObj = sim.GetFlowsheetSimulationObject(objectName);
                if (comObj != null)
                {
                    // Update object type using ProductName (human friendly name)
                    try
                    {
                        string? productName = comObj.ProductName?.ToString();
                        if (!string.IsNullOrEmpty(productName))
                            node.Type = productName;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Could not get ProductName for {ObjectName}: {EMessage}", objectName, ex.Message);
                    }

                    // Extract properties
                    node.Properties = ExtractNodePropertiesFromCom(comObj, node.Type, objectName);
                }
                else
                {
                    _logger.LogWarning("Could not get COM object for {ObjectName}", objectName);
                    node.Properties = new List<SimulatorModelRevisionDataProperty>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not extract COM properties for {ObjectName}: {EMessage}", objectName, ex.Message);
                node.Properties = new List<SimulatorModelRevisionDataProperty>();
            }

            return node;
        }
        catch (Exception e)
        {
            _logger.LogWarning("Error creating node from XML: {EMessage}", e.Message);
            return null;
        }
    }

    private List<SimulatorModelRevisionDataProperty> ExtractNodePropertiesFromCom(dynamic obj, string objectType, string nodeName)
    {
        var properties = new List<SimulatorModelRevisionDataProperty>();
        int extractedCount = 0;

        try
        {
            // Get write and read properties using GetProperties method
            dynamic? writeProperties = obj.GetProperties(1);
            dynamic? readProperties = obj.GetProperties(3);
            var allProperties = new HashSet<string>(writeProperties);
            foreach (string prop in readProperties)
            {
                allProperties.Add(prop);
            }

            foreach (string property in allProperties)
            {
                if (extractedCount >= _modelParsingConfig.MaxPropertiesPerNode)
                {
                    _logger.LogDebug("Reached max properties limit ({MaxPropertiesPerNode}) for {ObjectName}", _modelParsingConfig.MaxPropertiesPerNode, nodeName);
                    break;
                }

                try
                {
                    dynamic? modelProperty = CreateModelProperty(property, obj, objectType, nodeName, writeProperties);
                    if (modelProperty != null)
                    {
                        properties.Add(modelProperty);
                        extractedCount++;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogDebug("Skipped property {PropName} on {ObjectName}: {EMessage}", property, nodeName, e.Message);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning("Error extracting properties for {ObjectName}: {EMessage}", nodeName, e.Message);
        }

        _logger.LogDebug("Extracted {PropertiesCount} properties for {ObjectType} '{ObjectName}'", properties.Count, objectType, nodeName);
        return properties;
    }

    private SimulatorModelRevisionDataProperty? CreateModelProperty(string propertyKey, dynamic obj, string objectType, string objectName, dynamic writeProperties)
    {
        try
        {
            // Get property value
            dynamic value = obj.GetPropertyValue(propertyKey);
            if (value == null)
                return null;

            // Determine property name (handle PROP_ prefixed properties)
            string propertyName = propertyKey;
            if (propertyKey.StartsWith("PROP"))
            {
                string propName = propertyKey.Contains('/') ? propertyKey.Split('/')[0] : propertyKey;
                if (_propMap.TryGetValue(propName, out string? propKey))
                {
                    propertyName = propertyKey.Contains('/') ? propKey + "/" + propertyKey.Split('/')[1] : propKey;
                }
            }

            // Get unit information
            string unit = obj.GetPropertyUnit(propertyKey) ?? "";
            SimulatorValueUnitReference? unitReference = null;

            if (!string.IsNullOrEmpty(unit))
            {
                try
                {
                    string unitType = _unitSystem?.GetUnitType(unit)?.ToString() ?? "";
                    if (unitType != "none" && !string.IsNullOrEmpty(unitType))
                    {
                        unitReference = new SimulatorValueUnitReference
                        {
                            Name = unit,
                            Quantity = unitType
                        };
                    }
                }
                catch
                {
                    // We should never get here, but just in case
                }
            }

            // Determine value type and create SimulatorValue
            SimulatorValueType valueType;
            SimulatorValue simulatorValue;

            switch (value)
            {
                // Skip NaN or Infinity values
                case double doubleValue when double.IsNaN(doubleValue) || double.IsInfinity(doubleValue):
                    return null;
                case double doubleValue:
                    valueType = SimulatorValueType.DOUBLE;
                    simulatorValue = SimulatorValue.Create(doubleValue);
                    break;
                case float floatValue when float.IsNaN(floatValue) || float.IsInfinity(floatValue):
                    return null;
                case float floatValue:
                    valueType = SimulatorValueType.DOUBLE;
                    simulatorValue = SimulatorValue.Create(floatValue);
                    break;
                case int intValue:
                    valueType = SimulatorValueType.DOUBLE;
                    simulatorValue = SimulatorValue.Create(intValue);
                    break;
                case bool boolValue:
                    valueType = SimulatorValueType.DOUBLE;
                    simulatorValue = SimulatorValue.Create(boolValue ? 1.0 : 0.0);
                    break;
                case string stringValue:
                    valueType = SimulatorValueType.STRING;
                    simulatorValue = SimulatorValue.Create(stringValue);
                    break;
                // Handle arrays
                case Array { Length: 0 }:
                    return null;
                case Array arrayValue:
                {
                    object? firstElement = arrayValue.GetValue(0);
                    if (firstElement is double or float or int)
                    {
                        valueType = SimulatorValueType.DOUBLE_ARRAY;
                        double[] doubleArray = new double[arrayValue.Length];
                        for (int i = 0; i < arrayValue.Length; i++)
                        {
                            doubleArray[i] = Convert.ToDouble(arrayValue.GetValue(i));
                        }
                        simulatorValue = SimulatorValue.Create(doubleArray);
                    }
                    else
                    {
                        valueType = SimulatorValueType.STRING_ARRAY;
                        string[] stringArray = new string[arrayValue.Length];
                        for (int i = 0; i < arrayValue.Length; i++)
                        {
                            stringArray[i] = arrayValue.GetValue(i)?.ToString() ?? "";
                        }
                        simulatorValue = SimulatorValue.Create(stringArray);
                    }

                    break;
                }
                default:
                    // Convert other types to string
                    valueType = SimulatorValueType.STRING;
                    simulatorValue = SimulatorValue.Create(value.ToString() ?? "");
                    break;
            }

            // Check if property is read-only
            bool isReadOnly = !((IList)writeProperties).Contains(propertyKey);

            return new SimulatorModelRevisionDataProperty
            {
                Name = propertyName,
                ValueType = valueType,
                Value = simulatorValue,
                Unit = unitReference,
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
            _logger.LogDebug("Error creating model property {PropertyName} for {ObjectName}: {EMessage}", propertyKey, objectName, e.Message);
            return null;
        }
    }

    public static IEnumerable<SimulatorModelRevisionDataObjectEdge> GenerateFlowsheetEdgesFromXml(string xmlFilePath, List<SimulatorModelRevisionDataObjectNode> nodes, ILogger<DwsimClient>? logger = null)
    {
        var edges = new List<SimulatorModelRevisionDataObjectEdge>();
        Dictionary<string,SimulatorModelRevisionDataObjectNode> nodesByName = nodes.ToDictionary(n => n.Id, n => n);

        try
        {
            XDocument doc = XDocument.Load(xmlFilePath);
            List<XElement> graphicObjects = doc.Descendants("GraphicObjects").FirstOrDefault()?.Elements("GraphicObject").ToList() ?? [];

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
            logger?.LogWarning("Error generating flowsheet edges from XML: {EMessage}", e.Message);
        }

        return edges;
    }

    private static SimulatorModelRevisionDataConnectionType DetermineConnectionType(
        SimulatorModelRevisionDataObjectNode sourceNode, SimulatorModelRevisionDataObjectNode targetNode)
    {
        // Check if it's an energy stream
        if (sourceNode.Type?.Contains("Energy") == true || targetNode.Type?.Contains("Energy") == true)
            return SimulatorModelRevisionDataConnectionType.Energy;

        // Check if it's a material stream
        if (sourceNode.Type?.Contains("Material") == true || targetNode.Type?.Contains("Material") == true)
            return SimulatorModelRevisionDataConnectionType.Material;

        // Default to information for other connections
        return SimulatorModelRevisionDataConnectionType.Information;
    }

    private static List<SimulatorModelRevisionDataObjectEdge> RemoveDuplicateEdges(List<SimulatorModelRevisionDataObjectEdge> edges, ILogger<DwsimClient>? logger = null)
    {
        var uniqueEdges = new Dictionary<string, SimulatorModelRevisionDataObjectEdge>();

        foreach (SimulatorModelRevisionDataObjectEdge edge in edges)
        {
            string key = $"{edge.SourceId}_{edge.TargetId}";
            uniqueEdges.TryAdd(key, edge);
        }

        logger?.LogDebug("Reduced {EdgesCount} edges to {UniqueEdgesCount} unique edges after removing duplicates", edges.Count, uniqueEdges.Count);
        return [.. uniqueEdges.Values];
    }

    public static SimulatorModelRevisionDataThermodynamic ExtractThermodynamicDataFromXml(string xmlFilePath, ILogger<DwsimClient>? logger = null)
    {
        var components = new List<string>();
        var propertyPackages = new List<string>();

        try
        {
            XDocument doc = XDocument.Load(xmlFilePath);

            // Extract components from Compounds section
            IEnumerable<XElement>? compoundElements = doc.Descendants("Compounds").FirstOrDefault()?.Elements("Compound");
            if (compoundElements != null)
                components.AddRange(compoundElements.Select(compound => compound.Element("Name")?.Value).Where(componentName => !string.IsNullOrEmpty(componentName))!);

            // Extract property packages from PropertyPackages section
            IEnumerable<XElement>? propertyPackageElements = doc.Descendants("PropertyPackages").FirstOrDefault()?.Elements("PropertyPackage");
            if (propertyPackageElements != null)
                propertyPackages.AddRange(propertyPackageElements.Select(package => package.Element("ComponentName")?.Value).Where(packageName => !string.IsNullOrEmpty(packageName))!);

            logger?.LogDebug("Extracted {ComponentsCount} components and {PackagesCount} property packages from XML", components.Count, propertyPackages.Count);
        }
        catch (Exception e)
        {
            logger?.LogWarning("Error extracting thermodynamic data from XML: {EMessage}", e.Message);
        }

        return new SimulatorModelRevisionDataThermodynamic
        {
            Components = components,
            PropertyPackages = propertyPackages
        };
    }


    private static Type GetDwsimType(string installationPath, string dllName, string typeName)
    {
        string dllPath = Path.Combine(installationPath, dllName);
        Assembly assembly = Assembly.LoadFrom(dllPath);
        Type? type = assembly.GetType(typeName);
        return type ?? throw new InvalidOperationException($"Cannot find type {typeName} in {dllName}");
    }
}

public record DwsimModelParsingConfig
{
    public int MaxPropertiesPerNode { get; init; } = 100;
}
