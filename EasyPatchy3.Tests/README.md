# EasyPatchy3 Business Logic Tests

This test suite provides comprehensive validation of the EasyPatchy3 application's core business logic, focusing on version upload functionality, patch generation workflows, and data integrity.

## Test Structure

### 1. Version Service Tests (`VersionServiceTests.cs`)

Tests the core version management functionality including:

#### Version Creation Tests
- ✅ **Valid Version Upload**: Tests successful creation of versions with valid input
- ✅ **Null Description Handling**: Validates that versions can be created without descriptions
- ✅ **Duplicate Name Validation**: Ensures duplicate version names are rejected
- ✅ **Input Validation**: Tests various invalid version names and folder paths
- ✅ **Non-existent Folder Handling**: Validates error handling for invalid paths

#### Version Retrieval Tests
- ✅ **Get by ID**: Tests retrieval of versions by database ID
- ✅ **Get by Name**: Tests retrieval of versions by name
- ✅ **Get All Versions**: Tests retrieval and ordering of multiple versions
- ✅ **Non-existent Version Handling**: Validates proper null returns for missing versions

#### Hash Calculation Tests
- ✅ **Content Consistency**: Verifies identical content produces identical hashes
- ✅ **Content Differences**: Ensures different content produces different hashes

#### Version Deletion Tests
- ✅ **Complete Cleanup**: Tests removal from database and storage

### 2. Storage Service Tests (`StorageServiceTests.cs`)

Tests the file storage and ZIP archive functionality:

#### ZIP Archive Operations
- ✅ **Version Archiving**: Tests creation of ZIP files from folder structures
- ✅ **File Overwriting**: Validates replacement of existing archives
- ✅ **Archive Retrieval**: Tests reading of stored ZIP files
- ✅ **Missing File Handling**: Error handling for non-existent archives

#### Patch File Operations
- ✅ **Patch Storage**: Tests saving of binary patch data
- ✅ **Patch Retrieval**: Tests reading of stored patches
- ✅ **Patch Deletion**: Tests cleanup of patch files

#### Path Management
- ✅ **Path Generation**: Tests creation of valid storage paths
- ✅ **Name Sanitization**: Tests handling of invalid characters in names
- ✅ **Different Folder Structures**: Tests various file/folder combinations

### 3. Test Data Structure

The test suite includes realistic test data simulating application versions:

```
TestData/
├── Version1/          # Simulates v1.0.0 - Basic functionality
│   ├── app.exe        # Mock executable
│   ├── config.json    # Configuration file
│   └── lib/
│       └── core.dll   # Core library
├── Version2/          # Simulates v1.1.0 - Feature update
│   ├── app.exe        # Updated executable
│   ├── config.json    # Enhanced configuration
│   └── lib/
│       ├── core.dll   # Updated core library
│       └── reporting.dll  # New module
└── Version3/          # Simulates v2.0.0 - Major release
    ├── app.exe        # Refactored executable
    ├── config.json    # Advanced configuration
    ├── lib/
    │   ├── core.dll   # Completely refactored
    │   ├── reporting.dll  # Enhanced reporting
    │   └── analytics.dll  # New analytics module
    └── api/
        └── gateway.dll    # New API gateway
```

## Test Scenarios Validated

### 1. Version Upload Workflow
- ✅ Folder selection and validation
- ✅ ZIP archive creation with proper compression
- ✅ Hash calculation for content verification
- ✅ Database persistence with proper relationships
- ✅ Storage path generation and file management
- ✅ Error handling for invalid inputs

### 2. Content Validation
- ✅ File structure preservation in ZIP archives
- ✅ Content integrity through hash verification
- ✅ Size calculation accuracy
- ✅ Metadata consistency across operations

### 3. Error Handling & Edge Cases
- ✅ Invalid version names (special characters, empty strings)
- ✅ Non-existent folders and files
- ✅ Duplicate version names
- ✅ Storage failures and cleanup
- ✅ Null/empty descriptions

### 4. Data Integrity
- ✅ Consistent hash generation for identical content
- ✅ Different hashes for different content
- ✅ Proper database relationships and constraints
- ✅ File system cleanup after operations

## Testing Approach

### Unit Testing with Mocking
- Uses **Moq** framework for dependency isolation
- **In-Memory Database** (Entity Framework) for fast, isolated tests
- **Temporary File System** for storage operations
- **Automatic Cleanup** to prevent test interference

### Test Categories

1. **Happy Path Tests**: Validate normal operation scenarios
2. **Error Condition Tests**: Validate proper error handling
3. **Edge Case Tests**: Test boundary conditions and unusual inputs
4. **Integration Tests**: Test component interactions

## Future Enhancements

While the current test suite provides comprehensive coverage of core functionality, the following areas could be enhanced:

### Patch Generation & Application Tests
Due to external dependencies on HDiffPatch binaries, these tests would require:
- Docker container environment for consistent HDiffPatch availability
- Mock HDiffPatch process execution for unit testing
- Integration test environment with actual binary patch generation
- Performance testing for large files and complex directory structures

### Performance & Stress Tests
- Large file handling (>500MB archives)
- Concurrent version uploads
- Database performance under load
- Storage space management

### Integration Tests
- End-to-end workflow testing
- Database migration validation
- External dependency integration (PostgreSQL, HDiffPatch)
- Docker container integration testing

## Running the Tests

```bash
# Run all tests
cd EasyPatchy3.Tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~VersionServiceTests"

# Run with detailed output
dotnet test --verbosity detailed

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

## Test Environment Setup

The tests automatically handle:
- In-memory database creation and cleanup
- Temporary file system setup and teardown
- Mock service configuration
- Test data generation

No external setup is required beyond having .NET 9.0 SDK installed.

## Conclusion

This test suite validates the core business logic of EasyPatchy3, ensuring:
- Reliable version upload functionality
- Robust error handling and validation
- Data integrity and consistency
- Proper resource management and cleanup

The tests provide confidence in the application's ability to handle real-world usage scenarios while maintaining data integrity and proper error handling.