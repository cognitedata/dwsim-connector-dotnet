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
using System.Reflection;
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
}

/// <summary>
/// Configuration class for DWSIM model parsing
/// </summary>
public class DwsimModelParsingConfig
{
    // will be used later when we start to extract nodes
    public int MaxPropertiesPerNode { get; set; } = 100;
}
