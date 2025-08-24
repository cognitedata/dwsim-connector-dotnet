# DWSIM Connector Tests

This directory contains comprehensive unit tests for the DWSIM Connector, focusing on model extraction and parsing functionality without requiring actual DWSIM dependencies.

## Test Structure

### Test Projects
- **Connector.Tests.csproj** - Main test project using xUnit, FluentAssertions, and Moq
- **TestData/** - External test data files (XML flowsheets, sample data)
- **Mocks/** - Mock implementations of DWSIM COM objects

### Test Categories

#### 1. XML Parsing Tests (`DwsimModelParserXmlTests.cs`)
Tests pure XML parsing functionality without COM dependencies:
- Node extraction from XML flowsheets
- Edge generation from connections
- Thermodynamic data parsing
- Graphics object integration
- Error handling for malformed XML

#### 2. COM Object Tests (`DwsimModelParserComTests.cs`)
Tests COM object interaction using mocks:
- Property extraction from COM objects
- Unit system integration
- Data type handling (double, string, arrays, bool)
- Property name mapping (`PROP_MS_0` → `Temperature`)
- Error handling and graceful degradation
- Performance limits (100 properties per node)
- Full integration with XML data

## Mock System

### MockDwsimFlowsheetObject
Simulates DWSIM COM objects with:
- Property value storage with units
- Read/write property modes
- Error injection for testing failure scenarios
- Support for various data types

### MockDwsimFlowsheet
Container for mock COM objects with `GetFlowsheetSimulationObject()` method.

### MockUnitSystem
Real unit-to-quantity mappings based on JSON examples:
- 80+ unit types (temperature, pressure, flow, etc.)
- Proper quantity classification
- Unknown unit handling

## Test Data

### XML Test Files (`TestData/Xml/`)
- `basic_flowsheet.xml` - Simple nodes with graphics
- `connections_flowsheet.xml` - Connected flowsheet objects
- `thermodynamics_complete.xml` - Full thermodynamic data
- `empty_flowsheet.xml` - Edge case testing
- Additional specialized test cases

## Running Tests

### All Tests
```bash
dotnet test
```

### Specific Test Categories
```bash
# XML-only tests
dotnet test --filter "DwsimModelParserXmlTests"

# COM interaction tests
dotnet test --filter "DwsimModelParserComTests"

# Specific test method
dotnet test --filter "ExtractNodePropertiesFromCom_ExtractsPropertiesCorrectly"
```

### Test Output
```bash
# Verbose output
dotnet test --logger "console;verbosity=detailed"

# Minimal output
dotnet test --logger "console;verbosity=minimal"
```

## Test Architecture

### Dependency Injection for Testing
The `DwsimModelParser` supports constructor injection for testing:
```csharp
protected DwsimModelParser(ILogger<DwsimClient> logger,
    Dictionary<string, string> propMap,
    string dwsimInstallationPath,
    dynamic? unitSystem)
```

### TestableModelParser
Inner class that:
- Exposes private methods via reflection
- Injects mock dependencies
- Creates temporary DWXMZ files for integration testing
- Handles cleanup automatically

## Key Test Scenarios

### Property Extraction
- ✅ Mapped property names (`PROP_MS_0` → `Temperature`)
- ✅ Unit system integration with quantity mapping
- ✅ Various data types (double, string, arrays, boolean)
- ✅ Read-only vs writable properties

### Error Handling
- ✅ NaN and Infinity value filtering
- ✅ Empty array removal
- ✅ COM exception handling
- ✅ Malformed XML graceful degradation

### Integration Testing
- ✅ XML graphics + COM properties combined
- ✅ DWXMZ file format handling
- ✅ Node-property association
- ✅ Missing COM object scenarios

### Performance & Limits
- ✅ Maximum properties per node (100)
- ✅ Large property set handling
- ✅ Temporary file cleanup

## Benefits

1. **No DWSIM Dependencies** - Tests run independently of DWSIM installation
2. **Fast Execution** - Mock objects eliminate COM overhead
3. **Comprehensive Coverage** - Both happy path and error scenarios
4. **Realistic Data** - Unit mappings from actual JSON examples
5. **Easy Maintenance** - External XML files, not embedded strings
6. **Incremental Development** - Supports small PR workflow

## Development Workflow

1. **Add new functionality** to `DwsimModelParser`
2. **Create corresponding tests** with appropriate mocks
3. **Use external test data** for complex scenarios
4. **Verify error handling** with mock error injection
5. **Test integration** with XML + COM scenarios

This test architecture enables confident development and refactoring while maintaining fast feedback cycles for developers.