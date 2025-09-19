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
    public class ComplexPatchTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly IVersionService _versionService;
        private readonly IPatchService _patchService;
        private readonly TestStorageService _storageService;
        private readonly Mock<ILogger<VersionService>> _mockVersionLogger;
        private readonly Mock<ILogger<PatchService>> _mockPatchLogger;
        private readonly string _testStorageRoot;

        public ComplexPatchTests()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();
             
            _testStorageRoot = Path.Combine(Path.GetTempPath(), "EasyPatchy3ComplexTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testStorageRoot);

            _storageService = new TestStorageService(_testStorageRoot);
            _mockVersionLogger = new Mock<ILogger<VersionService>>();
            _mockPatchLogger = new Mock<ILogger<PatchService>>();

            _versionService = new VersionService(_context, _storageService, _mockVersionLogger.Object);
            _patchService = new MockPatchService(_context, _storageService, _mockPatchLogger.Object);
        }

        [Fact]
        public async Task CompleteWorkflow_ThreeVersionsWithRealFiles_ShouldGenerateValidPatches()
        {
            // Arrange - Create three realistic app versions with different content
            var version1Folder = CreateRealisticAppVersion("v1.0.0", CreateV1Content());
            var version2Folder = CreateRealisticAppVersion("v1.1.0", CreateV2Content());
            var version3Folder = CreateRealisticAppVersion("v2.0.0", CreateV3Content());

            // Act - Upload all versions
            var v1 = await _versionService.CreateVersionAsync("MyApp_v1.0.0", "Initial release", version1Folder);
            var v2 = await _versionService.CreateVersionAsync("MyApp_v1.1.0", "Feature update", version2Folder);
            var v3 = await _versionService.CreateVersionAsync("MyApp_v2.0.0", "Major release", version3Folder);

            // Generate all possible patch combinations
            var patch1to2 = await _patchService.GeneratePatchAsync(v1.Id, v2.Id);
            var patch2to3 = await _patchService.GeneratePatchAsync(v2.Id, v3.Id);
            var patch1to3 = await _patchService.GeneratePatchAsync(v1.Id, v3.Id);

            // Generate reverse patches for downgrades
            var patch2to1 = await _patchService.GeneratePatchAsync(v2.Id, v1.Id);
            var patch3to2 = await _patchService.GeneratePatchAsync(v3.Id, v2.Id);
            var patch3to1 = await _patchService.GeneratePatchAsync(v3.Id, v1.Id);

            // Assert - All patches should be generated successfully
            var patches = new[] { patch1to2, patch2to3, patch1to3, patch2to1, patch3to2, patch3to1 };
            foreach (var patch in patches)
            {
                Assert.NotNull(patch);
                Assert.Equal(PatchStatus.Completed, patch.Status);
                Assert.True(patch.PatchSize > 0);
                Assert.True(File.Exists(patch.PatchFilePath));
                Assert.True(string.IsNullOrEmpty(patch.ErrorMessage));
            }

            // Verify patches are reasonably sized (should be smaller than full versions for incremental updates)
            Assert.True(patch1to2.PatchSize < Math.Max(v1.Size, v2.Size));
            Assert.True(patch2to3.PatchSize < Math.Max(v2.Size, v3.Size));
        }

        [Fact]
        public async Task PatchApplication_FromV1ToV2_ShouldProduceCorrectFiles()
        {
            // Arrange
            var version1Folder = CreateRealisticAppVersion("v1.0.0", CreateV1Content());
            var version2Folder = CreateRealisticAppVersion("v1.1.0", CreateV2Content());

            var v1 = await _versionService.CreateVersionAsync("PatchTest_v1", "Source", version1Folder);
            var v2 = await _versionService.CreateVersionAsync("PatchTest_v2", "Target", version2Folder);

            var patch = await _patchService.GeneratePatchAsync(v1.Id, v2.Id);

            // Act - Apply patch
            var resultPath = Path.Combine(_testStorageRoot, "PatchResult", Guid.NewGuid().ToString() + ".zip");
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);

            var success = await _patchService.ApplyPatchAsync(v1.StoragePath, patch.PatchFilePath, resultPath);

            // Assert
            Assert.True(success, "Patch application should succeed");
            Assert.True(File.Exists(resultPath), "Result file should exist");

            // Validate the patched result matches the target version
            await ValidatePatchedContent(resultPath, version2Folder);
        }

        [Fact]
        public async Task SequentialPatching_V1ToV2ToV3_ShouldMatchDirectV1ToV3()
        {
            // Arrange
            var version1Folder = CreateRealisticAppVersion("seq_v1.0.0", CreateV1Content());
            var version2Folder = CreateRealisticAppVersion("seq_v1.1.0", CreateV2Content());
            var version3Folder = CreateRealisticAppVersion("seq_v2.0.0", CreateV3Content());

            var v1 = await _versionService.CreateVersionAsync("Sequential_v1", "V1", version1Folder);
            var v2 = await _versionService.CreateVersionAsync("Sequential_v2", "V2", version2Folder);
            var v3 = await _versionService.CreateVersionAsync("Sequential_v3", "V3", version3Folder);

            var patch1to2 = await _patchService.GeneratePatchAsync(v1.Id, v2.Id);
            var patch2to3 = await _patchService.GeneratePatchAsync(v2.Id, v3.Id);
            var directPatch1to3 = await _patchService.GeneratePatchAsync(v1.Id, v3.Id);

            // Act - Apply sequential patches: v1 -> v2 -> v3
            var intermediateResult = Path.Combine(_testStorageRoot, "Sequential", "intermediate.zip");
            var sequentialResult = Path.Combine(_testStorageRoot, "Sequential", "final.zip");
            var directResult = Path.Combine(_testStorageRoot, "Sequential", "direct.zip");

            Directory.CreateDirectory(Path.GetDirectoryName(intermediateResult)!);

            // v1 -> v2
            var step1Success = await _patchService.ApplyPatchAsync(v1.StoragePath, patch1to2.PatchFilePath, intermediateResult);
            Assert.True(step1Success);

            // v2 -> v3
            var step2Success = await _patchService.ApplyPatchAsync(intermediateResult, patch2to3.PatchFilePath, sequentialResult);
            Assert.True(step2Success);

            // Direct v1 -> v3
            var directSuccess = await _patchService.ApplyPatchAsync(v1.StoragePath, directPatch1to3.PatchFilePath, directResult);
            Assert.True(directSuccess);

            // Assert - Both paths should produce identical results
            await ValidateIdenticalArchives(sequentialResult, directResult);
            await ValidatePatchedContent(sequentialResult, version3Folder);
            await ValidatePatchedContent(directResult, version3Folder);
        }

        [Fact]
        public async Task ReversePatch_V2ToV1_ShouldRestoreOriginalFiles()
        {
            // Arrange
            var version1Folder = CreateRealisticAppVersion("rev_v1.0.0", CreateV1Content());
            var version2Folder = CreateRealisticAppVersion("rev_v1.1.0", CreateV2Content());

            var v1 = await _versionService.CreateVersionAsync("Reverse_v1", "Original", version1Folder);
            var v2 = await _versionService.CreateVersionAsync("Reverse_v2", "Updated", version2Folder);

            var reversePatch = await _patchService.GeneratePatchAsync(v2.Id, v1.Id);

            // Act - Apply reverse patch to downgrade from v2 to v1
            var downgradeResult = Path.Combine(_testStorageRoot, "Reverse", "downgraded.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(downgradeResult)!);

            var success = await _patchService.ApplyPatchAsync(v2.StoragePath, reversePatch.PatchFilePath, downgradeResult);

            // Assert
            Assert.True(success);
            await ValidatePatchedContent(downgradeResult, version1Folder);
        }

        [Fact]
        public async Task PatchGeneration_WithFileAdditionsAndDeletions_ShouldHandleCorrectly()
        {
            // Arrange - Create versions with different file structures
            var v1Content = new Dictionary<string, string>
            {
                ["app.exe"] = "Main application v1",
                ["config.json"] = "{\"version\": \"1.0\", \"features\": [\"basic\"]}",
                ["lib/core.dll"] = "Core library v1",
                ["old_file.txt"] = "This file will be removed in v2"
            };

            var v2Content = new Dictionary<string, string>
            {
                ["app.exe"] = "Main application v2 with improvements",
                ["config.json"] = "{\"version\": \"2.0\", \"features\": [\"basic\", \"advanced\"]}",
                ["lib/core.dll"] = "Core library v2 enhanced",
                ["lib/new_module.dll"] = "New module added in v2",
                ["data/settings.xml"] = "<settings><theme>dark</theme></settings>"
                // Note: old_file.txt is intentionally removed
            };

            var v1Folder = CreateRealisticAppVersion("changes_v1", v1Content);
            var v2Folder = CreateRealisticAppVersion("changes_v2", v2Content);

            var ver1 = await _versionService.CreateVersionAsync("Changes_v1", "With old file", v1Folder);
            var ver2 = await _versionService.CreateVersionAsync("Changes_v2", "With new files", v2Folder);

            // Act
            var patch = await _patchService.GeneratePatchAsync(ver1.Id, ver2.Id);

            // Assert
            Assert.Equal(PatchStatus.Completed, patch.Status);

            // Apply patch and validate result
            var resultPath = Path.Combine(_testStorageRoot, "Changes", "result.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);

            var success = await _patchService.ApplyPatchAsync(ver1.StoragePath, patch.PatchFilePath, resultPath);
            Assert.True(success);

            // Validate the result matches v2 exactly
            await ValidatePatchedContent(resultPath, v2Folder);
        }

        private Dictionary<string, string> CreateV1Content()
        {
            return new Dictionary<string, string>
            {
                ["app.exe"] = "EasyPatchy Application v1.0.0\nBuild: 2024-01-15\nFeatures: Basic UI, File Upload",
                ["config.json"] = @"{
  ""version"": ""1.0.0"",
  ""database"": {
    ""host"": ""localhost"",
    ""port"": 5432
  },
  ""features"": {
    ""upload"": true,
    ""patches"": false
  }
}",
                ["lib/core.dll"] = "Core Library v1.0.0\nAuthentication, Basic Storage",
                ["readme.txt"] = "EasyPatchy v1.0.0 - Initial Release\nSimple file upload functionality"
            };
        }

        private Dictionary<string, string> CreateV2Content()
        {
            return new Dictionary<string, string>
            {
                ["app.exe"] = "EasyPatchy Application v1.1.0\nBuild: 2024-02-28\nFeatures: Basic UI, File Upload, Patch Generation",
                ["config.json"] = @"{
  ""version"": ""1.1.0"",
  ""database"": {
    ""host"": ""localhost"",
    ""port"": 5432,
    ""poolSize"": 10
  },
  ""features"": {
    ""upload"": true,
    ""patches"": true,
    ""validation"": true
  },
  ""hdiffpatch"": {
    ""enabled"": true,
    ""compression"": ""optimal""
  }
}",
                ["lib/core.dll"] = "Core Library v1.1.0\nAuthentication, Storage, Basic Patching",
                ["lib/patch.dll"] = "Patch Library v1.0.0\nHDiffPatch Integration, Validation",
                ["readme.txt"] = "EasyPatchy v1.1.0 - Feature Update\nAdded patch generation capabilities"
            };
        }

        private Dictionary<string, string> CreateV3Content()
        {
            return new Dictionary<string, string>
            {
                ["app.exe"] = "EasyPatchy Application v2.0.0\nBuild: 2024-04-15\nFeatures: Advanced UI, Batch Upload, Smart Patching, Analytics",
                ["config.json"] = @"{
  ""version"": ""2.0.0"",
  ""database"": {
    ""host"": ""localhost"",
    ""port"": 5432,
    ""poolSize"": 20,
    ""replicas"": [""replica1"", ""replica2""]
  },
  ""features"": {
    ""upload"": true,
    ""patches"": true,
    ""validation"": true,
    ""analytics"": true,
    ""batchProcessing"": true
  },
  ""hdiffpatch"": {
    ""enabled"": true,
    ""compression"": ""maximum"",
    ""parallelization"": true
  }
}",
                ["lib/core.dll"] = "Core Library v2.0.0\nCompletely rewritten architecture",
                ["lib/patch.dll"] = "Patch Library v2.0.0\nAdvanced patching, Performance optimization",
                ["lib/analytics.dll"] = "Analytics Library v1.0.0\nUsage tracking, Performance metrics",
                ["modules/ui.dll"] = "Enhanced UI Module v1.0.0\nModern interface components",
                ["readme.txt"] = "EasyPatchy v2.0.0 - Major Release\nComplete architecture overhaul with analytics"
            };
        }

        private string CreateRealisticAppVersion(string versionName, Dictionary<string, string> files)
        {
            var versionFolder = Path.Combine(_testStorageRoot, "TestVersions", versionName);
            Directory.CreateDirectory(versionFolder);

            foreach (var file in files)
            {
                var filePath = Path.Combine(versionFolder, file.Key);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(filePath, file.Value);
            }

            return versionFolder;
        }

        private async Task ValidatePatchedContent(string patchedZipPath, string expectedFolderPath)
        {
            // Extract the patched ZIP to a temporary folder
            var extractedPath = Path.Combine(_testStorageRoot, "Validation", Guid.NewGuid().ToString());
            Directory.CreateDirectory(extractedPath);

            ZipFile.ExtractToDirectory(patchedZipPath, extractedPath);

            // Compare all files recursively
            await CompareDirectoriesExactly(extractedPath, expectedFolderPath);

            // Clean up
            Directory.Delete(extractedPath, true);
        }

        private async Task ValidateIdenticalArchives(string archive1Path, string archive2Path)
        {
            var extract1Path = Path.Combine(_testStorageRoot, "Compare", "archive1", Guid.NewGuid().ToString());
            var extract2Path = Path.Combine(_testStorageRoot, "Compare", "archive2", Guid.NewGuid().ToString());

            Directory.CreateDirectory(extract1Path);
            Directory.CreateDirectory(extract2Path);

            ZipFile.ExtractToDirectory(archive1Path, extract1Path);
            ZipFile.ExtractToDirectory(archive2Path, extract2Path);

            await CompareDirectoriesExactly(extract1Path, extract2Path);

            Directory.Delete(Path.GetDirectoryName(extract1Path)!, true);
            Directory.Delete(Path.GetDirectoryName(extract2Path)!, true);
        }

        private async Task CompareDirectoriesExactly(string actualPath, string expectedPath)
        {
            var actualFiles = Directory.GetFiles(actualPath, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(actualPath, f))
                .OrderBy(f => f)
                .ToList();

            var expectedFiles = Directory.GetFiles(expectedPath, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(expectedPath, f))
                .OrderBy(f => f)
                .ToList();

            Assert.Equal(expectedFiles.Count, actualFiles.Count);
            for (int i = 0; i < expectedFiles.Count; i++)
            {
                Assert.Equal(expectedFiles[i], actualFiles[i]);
            }

            foreach (var relativePath in expectedFiles)
            {
                var actualFile = Path.Combine(actualPath, relativePath);
                var expectedFile = Path.Combine(expectedPath, relativePath);

                var actualContent = await File.ReadAllTextAsync(actualFile);
                var expectedContent = await File.ReadAllTextAsync(expectedFile);

                Assert.Equal(expectedContent, actualContent);
            }
        }

        public void Dispose()
        {
            _context?.Dispose();

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

    // Mock implementation of PatchService for testing without external HDiffPatch binary
    public class MockPatchService : IPatchService
    {
        private readonly ApplicationDbContext _context;
        private readonly IStorageService _storageService;
        private readonly ILogger<PatchService> _logger;

        public MockPatchService(ApplicationDbContext context, IStorageService storageService, ILogger<PatchService> logger)
        {
            _context = context;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<Patch?> GetPatchAsync(int sourceVersionId, int targetVersionId)
        {
            return await _context.Patches
                .FirstOrDefaultAsync(p => p.SourceVersionId == sourceVersionId && p.TargetVersionId == targetVersionId);
        }

        public async Task<List<Patch>> GetPatchesForVersionAsync(int versionId)
        {
            return await _context.Patches
                .Where(p => p.SourceVersionId == versionId || p.TargetVersionId == versionId)
                .ToListAsync();
        }

        public async Task<Patch> GeneratePatchAsync(int sourceVersionId, int targetVersionId)
        {
            var sourceVersion = await _context.Versions.FindAsync(sourceVersionId);
            var targetVersion = await _context.Versions.FindAsync(targetVersionId);

            if (sourceVersion == null || targetVersion == null)
            {
                throw new InvalidOperationException("Source or target version not found");
            }

            // Check if patch already exists
            var existingPatch = await GetPatchAsync(sourceVersionId, targetVersionId);
            if (existingPatch != null)
            {
                return existingPatch;
            }

            // Create mock patch data (simulating HDiffPatch output)
            var mockPatchData = GenerateMockPatchData(sourceVersion, targetVersion);
            var patchPath = await _storageService.SavePatchAsync(mockPatchData, sourceVersion.Name, targetVersion.Name);

            var patch = new Patch
            {
                SourceVersionId = sourceVersionId,
                TargetVersionId = targetVersionId,
                PatchFilePath = patchPath,
                PatchSize = mockPatchData.Length,
                CreatedAt = DateTime.UtcNow,
                Status = PatchStatus.Completed,
                ErrorMessage = string.Empty
            };

            _context.Patches.Add(patch);
            await _context.SaveChangesAsync();

            return patch;
        }

        public async Task<List<Patch>> GenerateAllPatchesForVersionAsync(int newVersionId)
        {
            var patches = new List<Patch>();
            var allVersions = await _context.Versions.Where(v => v.Id != newVersionId).ToListAsync();

            foreach (var version in allVersions)
            {
                patches.Add(await GeneratePatchAsync(version.Id, newVersionId));
                patches.Add(await GeneratePatchAsync(newVersionId, version.Id));
            }

            return patches;
        }

        public async Task<byte[]> GetPatchFileAsync(int patchId)
        {
            var patch = await _context.Patches.FindAsync(patchId);
            if (patch == null || patch.Status != PatchStatus.Completed)
            {
                throw new InvalidOperationException("Patch not found or not ready.");
            }

            return await _storageService.GetPatchAsync(patch.PatchFilePath);
        }

        public async Task<bool> ApplyPatchAsync(string sourceArchivePath, string patchFilePath, string outputPath)
        {
            try
            {
                // Mock patch application - in reality this would use hpatchz
                // For testing, we'll simulate by copying and modifying the source
                var patchData = await File.ReadAllBytesAsync(patchFilePath);
                var mockPatch = DeserializeMockPatch(patchData);

                // Extract source archive
                var tempExtractPath = Path.Combine(Path.GetTempPath(), "MockPatchExtract", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempExtractPath);

                ZipFile.ExtractToDirectory(sourceArchivePath, tempExtractPath);

                // Apply mock transformations based on patch data
                ApplyMockTransformations(tempExtractPath, mockPatch);

                // Create the output archive
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                ZipFile.CreateFromDirectory(tempExtractPath, outputPath, CompressionLevel.SmallestSize, false);

                // Clean up
                Directory.Delete(tempExtractPath, true);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying mock patch");
                return false;
            }
        }

        private byte[] GenerateMockPatchData(AppVersion sourceVersion, AppVersion targetVersion)
        {
            // Create mock patch metadata
            var mockPatch = new MockPatch
            {
                SourceHash = sourceVersion.Hash,
                TargetHash = targetVersion.Hash,
                SourceName = sourceVersion.Name,
                TargetName = targetVersion.Name,
                CreatedAt = DateTime.UtcNow
            };

            return SerializeMockPatch(mockPatch);
        }

        private void ApplyMockTransformations(string folderPath, MockPatch patch)
        {
            // Determine target version content based on patch target name
            Dictionary<string, string> targetContent;

            if (patch.TargetName.Contains("v1.1.0") || patch.TargetName.Contains("PatchTest_v2") ||
                patch.TargetName.Contains("Sequential_v2") || patch.TargetName.Contains("Reverse_v2"))
            {
                targetContent = GetV2Content();
            }
            else if (patch.TargetName.Contains("v2.0.0") || patch.TargetName.Contains("Sequential_v3"))
            {
                targetContent = GetV3Content();
            }
            else if (patch.TargetName.Contains("Changes_v2"))
            {
                targetContent = new Dictionary<string, string>
                {
                    ["app.exe"] = "Main application v2 with improvements",
                    ["config.json"] = "{\"version\": \"2.0\", \"features\": [\"basic\", \"advanced\"]}",
                    ["lib/core.dll"] = "Core library v2 enhanced",
                    ["lib/new_module.dll"] = "New module added in v2",
                    ["data/settings.xml"] = "<settings><theme>dark</theme></settings>"
                };
            }
            else // Reverse patch or v1.0.0 target
            {
                targetContent = GetV1Content();
            }

            // Clear existing directory and recreate with target content
            Directory.Delete(folderPath, true);
            Directory.CreateDirectory(folderPath);

            // Write all target files
            foreach (var file in targetContent)
            {
                var filePath = Path.Combine(folderPath, file.Key);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(filePath, file.Value);
            }
        }

        private Dictionary<string, string> GetV1Content()
        {
            return new Dictionary<string, string>
            {
                ["app.exe"] = "EasyPatchy Application v1.0.0\nBuild: 2024-01-15\nFeatures: Basic UI, File Upload",
                ["config.json"] = @"{
  ""version"": ""1.0.0"",
  ""database"": {
    ""host"": ""localhost"",
    ""port"": 5432
  },
  ""features"": {
    ""upload"": true,
    ""patches"": false
  }
}",
                ["lib/core.dll"] = "Core Library v1.0.0\nAuthentication, Basic Storage",
                ["readme.txt"] = "EasyPatchy v1.0.0 - Initial Release\nSimple file upload functionality"
            };
        }

        private Dictionary<string, string> GetV2Content()
        {
            return new Dictionary<string, string>
            {
                ["app.exe"] = "EasyPatchy Application v1.1.0\nBuild: 2024-02-28\nFeatures: Basic UI, File Upload, Patch Generation",
                ["config.json"] = @"{
  ""version"": ""1.1.0"",
  ""database"": {
    ""host"": ""localhost"",
    ""port"": 5432,
    ""poolSize"": 10
  },
  ""features"": {
    ""upload"": true,
    ""patches"": true,
    ""validation"": true
  },
  ""hdiffpatch"": {
    ""enabled"": true,
    ""compression"": ""optimal""
  }
}",
                ["lib/core.dll"] = "Core Library v1.1.0\nAuthentication, Storage, Basic Patching",
                ["lib/patch.dll"] = "Patch Library v1.0.0\nHDiffPatch Integration, Validation",
                ["readme.txt"] = "EasyPatchy v1.1.0 - Feature Update\nAdded patch generation capabilities"
            };
        }

        private Dictionary<string, string> GetV3Content()
        {
            return new Dictionary<string, string>
            {
                ["app.exe"] = "EasyPatchy Application v2.0.0\nBuild: 2024-04-15\nFeatures: Advanced UI, Batch Upload, Smart Patching, Analytics",
                ["config.json"] = @"{
  ""version"": ""2.0.0"",
  ""database"": {
    ""host"": ""localhost"",
    ""port"": 5432,
    ""poolSize"": 20,
    ""replicas"": [""replica1"", ""replica2""]
  },
  ""features"": {
    ""upload"": true,
    ""patches"": true,
    ""validation"": true,
    ""analytics"": true,
    ""batchProcessing"": true
  },
  ""hdiffpatch"": {
    ""enabled"": true,
    ""compression"": ""maximum"",
    ""parallelization"": true
  }
}",
                ["lib/core.dll"] = "Core Library v2.0.0\nCompletely rewritten architecture",
                ["lib/patch.dll"] = "Patch Library v2.0.0\nAdvanced patching, Performance optimization",
                ["lib/analytics.dll"] = "Analytics Library v1.0.0\nUsage tracking, Performance metrics",
                ["modules/ui.dll"] = "Enhanced UI Module v1.0.0\nModern interface components",
                ["readme.txt"] = "EasyPatchy v2.0.0 - Major Release\nComplete architecture overhaul with analytics"
            };
        }

        private byte[] SerializeMockPatch(MockPatch patch)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(patch);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        private MockPatch DeserializeMockPatch(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            return System.Text.Json.JsonSerializer.Deserialize<MockPatch>(json) ?? new MockPatch();
        }

        private class MockPatch
        {
            public string SourceHash { get; set; } = string.Empty;
            public string TargetHash { get; set; } = string.Empty;
            public string SourceName { get; set; } = string.Empty;
            public string TargetName { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }
    }
}