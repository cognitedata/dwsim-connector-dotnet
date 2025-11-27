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

using Connector.Dwsim;
using Microsoft.Extensions.Logging;
using Moq;

namespace Connector.Tests.Dwsim;

public class DwsimModelParserTests
{
    private readonly Mock<ILogger<DwsimClient>> _mockLogger;
    private readonly Dictionary<string, string> _propMap;
    private readonly string _testDataPath;
    private readonly TestableParser _parser;

    public DwsimModelParserTests()
    {
        _mockLogger = new Mock<ILogger<DwsimClient>>();
        _propMap = new Dictionary<string, string>();
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");
        _parser = new TestableParser(_mockLogger.Object, _propMap, "dummy-path");
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitialize()
    {
        // Arrange & Act
        var parser = new TestableParser(_mockLogger.Object, _propMap, "test-path");

        // Assert
        Assert.NotNull(parser);
    }

    [Fact]
    public void ExtractXmlFromDwxmz_WithValidDwxmzFile_ShouldReturnXmlPath()
    {
        // Arrange
        string dwxmzPath = Path.Combine(_testDataPath, "minimal_simulation.dwxmz");
        Assert.True(File.Exists(dwxmzPath), "Test DWXMZ file should exist");

        // Act
        string xmlPath = _parser.ExtractXmlFromDwxmz(dwxmzPath);

        // Assert
        Assert.NotNull(xmlPath);
        Assert.NotEmpty(xmlPath);
        Assert.True(File.Exists(xmlPath));
        Assert.EndsWith(".xml", xmlPath);

        // Verify XML content
        string xmlContent = File.ReadAllText(xmlPath);
        Assert.Contains("DWSIM_Simulation_Data", xmlContent);
        Assert.Contains("SimulationObjects", xmlContent);

        // Cleanup
        _parser.CleanupTempFiles(xmlPath);
    }

    [Fact]
    public void ExtractXmlFromDwxmz_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        string nonExistentPath = Path.Combine(_testDataPath, "nonexistent.dwxmz");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _parser.ExtractXmlFromDwxmz(nonExistentPath));
    }

    [Fact]
    public void CleanupTempFiles_WithValidTempPath_ShouldRemoveDirectory()
    {
        // Arrange
        string dwxmzPath = Path.Combine(_testDataPath, "minimal_simulation.dwxmz");
        string xmlPath = _parser.ExtractXmlFromDwxmz(dwxmzPath);
        string tempDir = Path.GetDirectoryName(xmlPath)!;

        // Verify temp directory exists
        Assert.True(Directory.Exists(tempDir));

        // Act
        _parser.CleanupTempFiles(xmlPath);

        // Assert
        Assert.False(Directory.Exists(tempDir));
    }

    [Fact]
    public void ValidateFilePath_WithExistingFile_ShouldReturnTrue()
    {
        // Arrange
        string existingFile = Path.Combine(_testDataPath, "minimal_simulation.dwxmz");

        // Act
        bool result = _parser.ValidateFilePath(existingFile);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateFilePath_WithNonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        string nonExistentFile = Path.Combine(_testDataPath, "nonexistent.xml");

        // Act
        bool result = _parser.ValidateFilePath(nonExistentFile);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ValidateFilePath_WithInvalidPath_ShouldReturnFalse(string invalidPath)
    {
        // Act
        bool result = _parser.ValidateFilePath(invalidPath);

        // Assert
        Assert.False(result);
    }

    private class TestableParser(
        ILogger<DwsimClient> logger,
        Dictionary<string, string> propMap,
        string dwsimInstallationPath)
        : DwsimModelParser(logger, propMap, dwsimInstallationPath, new MockUnitSystem())
    {
        public new string ExtractXmlFromDwxmz(string filePath) => base.ExtractXmlFromDwxmz(filePath);
        public new void CleanupTempFiles(string xmlPath) => base.CleanupTempFiles(xmlPath);
        public new bool ValidateFilePath(string filePath) => base.ValidateFilePath(filePath);
    }

    private class MockUnitSystem
    {
        // Mock unit system for testing without DWSIM dependencies
        // will come in future PRs
    }
}
