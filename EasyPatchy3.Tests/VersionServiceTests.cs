using EasyPatchy3.Data;
using EasyPatchy3.Data.Models;
using EasyPatchy3.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO.Compression;
using Xunit;

namespace EasyPatchy3.Tests
{
    public class VersionServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly IVersionService _versionService;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly Mock<ILogger<VersionService>> _mockLogger;
        private readonly string _testDataRoot;

        public VersionServiceTests()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            _mockStorageService = new Mock<IStorageService>();
            _mockLogger = new Mock<ILogger<VersionService>>();

            _testDataRoot = Path.Combine(Path.GetTempPath(), "EasyPatchy3Tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDataRoot);

            _versionService = new VersionService(_context, _mockStorageService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task CreateVersionAsync_ValidInput_ShouldCreateVersion()
        {
            // Arrange
            var testFolder = CreateTestFolder("v1.0.0");
            var versionName = "TestApp_v1.0.0";
            var description = "Initial test version";
            var expectedStoragePath = "/storage/test.zip";

            _mockStorageService
                .Setup(x => x.SaveVersionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedStoragePath);

            // Act
            var result = await _versionService.CreateVersionAsync(versionName, description, testFolder);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(versionName, result.Name);
            Assert.Equal(description, result.Description);
            Assert.Equal(expectedStoragePath, result.StoragePath);
            Assert.True(result.Size > 0);
            Assert.False(string.IsNullOrEmpty(result.Hash));
            Assert.True(result.Id > 0);

            // Verify the version was saved to database
            var savedVersion = await _context.Versions.FindAsync(result.Id);
            Assert.NotNull(savedVersion);
            Assert.Equal(versionName, savedVersion.Name);
        }

        [Fact]
        public async Task CreateVersionAsync_WithNullDescription_ShouldSucceed()
        {
            // Arrange
            var testFolder = CreateTestFolder("v1.1.0");
            var versionName = "TestApp_v1.1.0_NoDesc";
            var expectedStoragePath = "/storage/test2.zip";

            _mockStorageService
                .Setup(x => x.SaveVersionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedStoragePath);

            // Act
            var result = await _versionService.CreateVersionAsync(versionName, null, testFolder);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(versionName, result.Name);
            Assert.Null(result.Description);
        }

        [Fact]
        public async Task CreateVersionAsync_DuplicateName_ShouldThrowException()
        {
            // Arrange
            var testFolder = CreateTestFolder("duplicate");
            var versionName = "DuplicateVersion";
            _mockStorageService
                .Setup(x => x.SaveVersionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("/storage/first.zip");

            await _versionService.CreateVersionAsync(versionName, "First version", testFolder);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _versionService.CreateVersionAsync(versionName, "Second version", testFolder));

            Assert.Contains("already exists", exception.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Test<Name")]
        [InlineData("Test>Name")]
        [InlineData("Test|Name")]
        [InlineData("Test*Name")]
        [InlineData("Test?Name")]
        public async Task CreateVersionAsync_InvalidVersionName_ShouldThrowException(string invalidName)
        {
            // Arrange
            var testFolder = CreateTestFolder("invalid");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _versionService.CreateVersionAsync(invalidName, "Description", testFolder));
        }

        [Fact]
        public async Task CreateVersionAsync_NonExistentFolder_ShouldThrowException()
        {
            // Arrange
            var nonExistentFolder = Path.Combine(_testDataRoot, "non-existent");

            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                () => _versionService.CreateVersionAsync("TestVersion", "Description", nonExistentFolder));
        }

        [Fact]
        public async Task GetVersionAsync_ExistingId_ShouldReturnVersion()
        {
            // Arrange
            var testFolder = CreateTestFolder("existing");
            _mockStorageService
                .Setup(x => x.SaveVersionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("/storage/existing.zip");

            var createdVersion = await _versionService.CreateVersionAsync("ExistingVersion", "Test", testFolder);

            // Act
            var result = await _versionService.GetVersionAsync(createdVersion.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(createdVersion.Id, result.Id);
            Assert.Equal(createdVersion.Name, result.Name);
        }

        [Fact]
        public async Task GetVersionAsync_NonExistentId_ShouldReturnNull()
        {
            // Act
            var result = await _versionService.GetVersionAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetVersionByNameAsync_ExistingName_ShouldReturnVersion()
        {
            // Arrange
            var testFolder = CreateTestFolder("byname");
            var versionName = "TestByName";
            _mockStorageService
                .Setup(x => x.SaveVersionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("/storage/byname.zip");

            var createdVersion = await _versionService.CreateVersionAsync(versionName, "Test", testFolder);

            // Act
            var result = await _versionService.GetVersionByNameAsync(versionName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(versionName, result.Name);
        }

        [Fact]
        public async Task GetAllVersionsAsync_WithMultipleVersions_ShouldReturnOrderedByCreatedAt()
        {
            // Arrange
            var testFolder1 = CreateTestFolder("multi1");
            var testFolder2 = CreateTestFolder("multi2");

            _mockStorageService
                .SetupSequence(x => x.SaveVersionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("/storage/multi1.zip")
                .ReturnsAsync("/storage/multi2.zip");

            var version1 = await _versionService.CreateVersionAsync("Version1", "First", testFolder1);
            await Task.Delay(10); // Ensure different timestamps
            var version2 = await _versionService.CreateVersionAsync("Version2", "Second", testFolder2);

            // Act
            var result = await _versionService.GetAllVersionsAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(version2.Id, result[0].Id); // Should be newest first
            Assert.Equal(version1.Id, result[1].Id);
        }

        [Fact]
        public async Task DeleteVersionAsync_ExistingVersion_ShouldDeleteFromDbAndStorage()
        {
            // Arrange
            var testFolder = CreateTestFolder("delete");
            var storagePath = "/storage/delete.zip";
            _mockStorageService
                .Setup(x => x.SaveVersionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(storagePath);

            var createdVersion = await _versionService.CreateVersionAsync("ToDelete", "Test", testFolder);

            // Act
            await _versionService.DeleteVersionAsync(createdVersion.Id);

            // Assert
            var deletedVersion = await _context.Versions.FindAsync(createdVersion.Id);
            Assert.Null(deletedVersion);

            _mockStorageService.Verify(x => x.DeleteVersionAsync(storagePath), Times.Once);
        }

        [Fact]
        public void HashCalculation_SameContent_ShouldProduceSameHash()
        {
            // Arrange
            var folder1 = CreateTestFolder("hash1", new Dictionary<string, string>
            {
                ["file1.txt"] = "Content A",
                ["file2.txt"] = "Content B"
            });

            var folder2 = CreateTestFolder("hash2", new Dictionary<string, string>
            {
                ["file1.txt"] = "Content A",
                ["file2.txt"] = "Content B"
            });

            _mockStorageService
                .SetupSequence(x => x.SaveVersionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("/storage/hash1.zip")
                .ReturnsAsync("/storage/hash2.zip");

            // Act
            var version1 = _versionService.CreateVersionAsync("Hash1", "Test", folder1).Result;
            var version2 = _versionService.CreateVersionAsync("Hash2", "Test", folder2).Result;

            // Assert
            Assert.Equal(version1.Hash, version2.Hash);
        }

        [Fact]
        public void HashCalculation_DifferentContent_ShouldProduceDifferentHash()
        {
            // Arrange
            var folder1 = CreateTestFolder("different1", new Dictionary<string, string>
            {
                ["file1.txt"] = "Content A",
                ["file2.txt"] = "Content B"
            });

            var folder2 = CreateTestFolder("different2", new Dictionary<string, string>
            {
                ["file1.txt"] = "Content A",
                ["file2.txt"] = "Content C" // Different content
            });

            _mockStorageService
                .SetupSequence(x => x.SaveVersionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("/storage/different1.zip")
                .ReturnsAsync("/storage/different2.zip");

            // Act
            var version1 = _versionService.CreateVersionAsync("Different1", "Test", folder1).Result;
            var version2 = _versionService.CreateVersionAsync("Different2", "Test", folder2).Result;

            // Assert
            Assert.NotEqual(version1.Hash, version2.Hash);
        }

        private string CreateTestFolder(string folderName, Dictionary<string, string>? files = null)
        {
            var folderPath = Path.Combine(_testDataRoot, folderName);
            Directory.CreateDirectory(folderPath);

            files ??= new Dictionary<string, string>
            {
                ["app.exe"] = $"Mock executable for {folderName}",
                ["config.json"] = $"{{\"version\": \"{folderName}\"}}"
            };

            foreach (var file in files)
            {
                var filePath = Path.Combine(folderPath, file.Key);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(filePath, file.Value);
            }

            return folderPath;
        }

        public void Dispose()
        {
            _context?.Dispose();

            if (Directory.Exists(_testDataRoot))
            {
                try
                {
                    Directory.Delete(_testDataRoot, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }
    }
}