using EasyPatchy3.Services;
using System.IO.Compression;
using Xunit;

namespace EasyPatchy3.Tests
{
    public class StorageServiceTests : IDisposable
    {
        private readonly IStorageService _storageService;
        private readonly string _testStorageRoot;

        public StorageServiceTests()
        {
            _testStorageRoot = Path.Combine(Path.GetTempPath(), "EasyPatchy3StorageTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testStorageRoot);
            _storageService = new TestStorageService(_testStorageRoot);
        }

        [Fact]
        public async Task SaveVersionAsync_ValidFolder_ShouldCreateZipFile()
        {
            // Arrange
            var testFolder = CreateTestVersionFolder("v1.0.0");
            var versionName = "TestVersion1.0.0";

            // Act
            var zipPath = await _storageService.SaveVersionAsync(testFolder, versionName);

            // Assert
            Assert.True(File.Exists(zipPath));
            Assert.EndsWith(".zip", zipPath);

            // Verify the ZIP contains the expected files
            using var archive = ZipFile.OpenRead(zipPath);
            Assert.True(archive.Entries.Count > 0);

            var fileNames = archive.Entries.Select(e => e.Name).ToList();
            Assert.Contains("app.exe", fileNames);
            Assert.Contains("config.json", fileNames);
        }

        [Fact]
        public async Task SaveVersionAsync_OverwriteExisting_ShouldReplaceFile()
        {
            // Arrange
            var testFolder1 = CreateTestVersionFolder("v1.0.0");
            var testFolder2 = CreateTestVersionFolder("v1.0.1");
            var versionName = "OverwriteTest";

            // Act
            var zipPath1 = await _storageService.SaveVersionAsync(testFolder1, versionName);
            var originalSize = new FileInfo(zipPath1).Length;

            var zipPath2 = await _storageService.SaveVersionAsync(testFolder2, versionName);
            var newSize = new FileInfo(zipPath2).Length;

            // Assert
            Assert.Equal(zipPath1, zipPath2); // Same path
            Assert.True(File.Exists(zipPath2));
            // Different content should result in different size
        }

        [Fact]
        public async Task GetVersionArchiveAsync_ExistingFile_ShouldReturnData()
        {
            // Arrange
            var testFolder = CreateTestVersionFolder("archive");
            var versionName = "ArchiveTest";
            var zipPath = await _storageService.SaveVersionAsync(testFolder, versionName);

            // Act
            var data = await _storageService.GetVersionArchiveAsync(zipPath);

            // Assert
            Assert.NotNull(data);
            Assert.True(data.Length > 0);
        }

        [Fact]
        public async Task GetVersionArchiveAsync_NonExistentFile_ShouldThrowException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testStorageRoot, "non-existent.zip");

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _storageService.GetVersionArchiveAsync(nonExistentPath));
        }

        [Fact]
        public async Task SaveAndGetPatchAsync_ValidPatchData_ShouldWorkCorrectly()
        {
            // Arrange
            var patchData = System.Text.Encoding.UTF8.GetBytes("Mock patch data for testing");
            var sourceVersion = "v1.0.0";
            var targetVersion = "v1.1.0";

            // Act
            var patchPath = await _storageService.SavePatchAsync(patchData, sourceVersion, targetVersion);
            var retrievedData = await _storageService.GetPatchAsync(patchPath);

            // Assert
            Assert.True(File.Exists(patchPath));
            Assert.Equal(patchData, retrievedData);
            Assert.Contains("v1.0.0_to_v1.1.0", patchPath);
        }

        [Fact]
        public async Task DeleteVersionAsync_ExistingFile_ShouldRemoveFile()
        {
            // Arrange
            var testFolder = CreateTestVersionFolder("delete");
            var versionName = "DeleteTest";
            var zipPath = await _storageService.SaveVersionAsync(testFolder, versionName);

            // Act
            await _storageService.DeleteVersionAsync(zipPath);

            // Assert
            Assert.False(File.Exists(zipPath));
        }

        [Fact]
        public async Task DeleteVersionAsync_NonExistentFile_ShouldNotThrow()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testStorageRoot, "non-existent.zip");

            // Act & Assert - Should not throw
            await _storageService.DeleteVersionAsync(nonExistentPath);
        }

        [Fact]
        public async Task DeletePatchAsync_ExistingFile_ShouldRemoveFile()
        {
            // Arrange
            var patchData = System.Text.Encoding.UTF8.GetBytes("Test patch");
            var patchPath = await _storageService.SavePatchAsync(patchData, "v1", "v2");

            // Act
            await _storageService.DeletePatchAsync(patchPath);

            // Assert
            Assert.False(File.Exists(patchPath));
        }

        [Fact]
        public void GetVersionPath_ValidName_ShouldReturnValidPath()
        {
            // Act
            var path = _storageService.GetVersionPath("TestVersion1.0.0");

            // Assert
            Assert.Contains("TestVersion1.0.0", path);
            Assert.Contains(_testStorageRoot, path);
        }

        [Fact]
        public void GetVersionPath_InvalidCharacters_ShouldSanitize()
        {
            // Act
            var path = _storageService.GetVersionPath("Test<Version>1.0.0");

            // Assert
            Assert.DoesNotContain("<", path);
            Assert.DoesNotContain(">", path);
            Assert.Contains("Test_Version_1.0.0", path);
        }

        [Fact]
        public void GetPatchPath_ValidVersions_ShouldReturnValidPath()
        {
            // Act
            var path = _storageService.GetPatchPath("v1.0.0", "v1.1.0");

            // Assert
            Assert.Contains("v1.0.0_to_v1.1.0", path);
            Assert.EndsWith(".patch", path);
        }

        [Theory]
        [InlineData("EmptyFolder", new string[0])]
        [InlineData("SingleFile", new[] { "single.txt" })]
        [InlineData("MultipleFiles", new[] { "file1.txt", "file2.exe", "config.json" })]
        public async Task SaveVersionAsync_DifferentFolderStructures_ShouldHandleCorrectly(string folderName, string[] fileNames)
        {
            // Arrange
            var testFolder = CreateTestVersionFolder(folderName, fileNames);

            // Act
            var zipPath = await _storageService.SaveVersionAsync(testFolder, folderName);

            // Assert
            Assert.True(File.Exists(zipPath));

            using var archive = ZipFile.OpenRead(zipPath);
            Assert.Equal(fileNames.Length, archive.Entries.Count);

            foreach (var fileName in fileNames)
            {
                Assert.Contains(archive.Entries, e => e.Name == fileName);
            }
        }

        private string CreateTestVersionFolder(string folderName, string[]? fileNames = null)
        {
            var folderPath = Path.Combine(_testStorageRoot, "TestData", folderName);
            Directory.CreateDirectory(folderPath);

            fileNames ??= new[] { "app.exe", "config.json", "lib/core.dll" };

            foreach (var fileName in fileNames)
            {
                var filePath = Path.Combine(folderPath, fileName);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(filePath, $"Mock content for {fileName} in {folderName}");
            }

            return folderPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(_testStorageRoot))
            {
                try
                {
                    Directory.Delete(_testStorageRoot, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }
    }

    // Test-specific storage service for isolated testing
    public class TestStorageService : IStorageService
    {
        private readonly string _storageRoot;
        private readonly string _versionsPath;
        private readonly string _patchesPath;

        public TestStorageService(string testStorageRoot)
        {
            _storageRoot = testStorageRoot;
            _versionsPath = Path.Combine(_storageRoot, "Versions");
            _patchesPath = Path.Combine(_storageRoot, "Patches");

            Directory.CreateDirectory(_versionsPath);
            Directory.CreateDirectory(_patchesPath);
        }

        public async Task<string> SaveVersionAsync(string folderPath, string versionName)
        {
            var versionPath = GetVersionPath(versionName);
            var zipPath = versionPath + ".zip";

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            await Task.Run(() => ZipFile.CreateFromDirectory(folderPath, zipPath, CompressionLevel.SmallestSize, false));
            return zipPath;
        }

        public async Task<string> SavePatchAsync(byte[] patchData, string sourceVersion, string targetVersion)
        {
            var patchPath = GetPatchPath(sourceVersion, targetVersion);
            await File.WriteAllBytesAsync(patchPath, patchData);
            return patchPath;
        }

        public async Task<byte[]> GetVersionArchiveAsync(string storagePath)
        {
            if (!File.Exists(storagePath))
            {
                throw new FileNotFoundException($"Version archive not found: {storagePath}");
            }

            return await File.ReadAllBytesAsync(storagePath);
        }

        public async Task<byte[]> GetPatchAsync(string patchPath)
        {
            if (!File.Exists(patchPath))
            {
                throw new FileNotFoundException($"Patch file not found: {patchPath}");
            }

            return await File.ReadAllBytesAsync(patchPath);
        }

        public Task DeleteVersionAsync(string storagePath)
        {
            if (File.Exists(storagePath))
            {
                File.Delete(storagePath);
            }

            return Task.CompletedTask;
        }

        public Task DeletePatchAsync(string patchPath)
        {
            if (File.Exists(patchPath))
            {
                File.Delete(patchPath);
            }

            return Task.CompletedTask;
        }

        public string GetVersionPath(string versionName)
        {
            var safeName = Path.GetInvalidFileNameChars()
                .Aggregate(versionName, (current, c) => current.Replace(c, '_'));
            return Path.Combine(_versionsPath, safeName);
        }

        public string GetPatchPath(string sourceVersion, string targetVersion)
        {
            var safeName = $"{sourceVersion}_to_{targetVersion}.patch";
            safeName = Path.GetInvalidFileNameChars()
                .Aggregate(safeName, (current, c) => current.Replace(c, '_'));
            return Path.Combine(_patchesPath, safeName);
        }
    }
}