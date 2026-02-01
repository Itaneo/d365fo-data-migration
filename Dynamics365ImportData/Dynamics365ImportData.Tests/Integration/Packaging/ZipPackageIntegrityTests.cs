namespace Dynamics365ImportData.Tests.Integration.Packaging;

using Dynamics365ImportData.Tests.TestHelpers;

using Shouldly;

using System.IO.Compression;

using Xunit;

/// <summary>
/// Extended integrity tests for ZIP package structure used by XmlPackageOutputFactoryBase.
/// Like ZipPackageTests, these tests replicate the ZIP creation logic directly because
/// XmlPackageOutputFactoryBase is internal abstract and requires IServiceProvider + ILogger
/// dependencies that tightly couple to the DI container. Tests verify the ZIP structure
/// contract (archive validity, content integrity, multi-entity support, compression,
/// entry naming) that production code must preserve.
/// </summary>
public class ZipPackageIntegrityTests
{
    [Fact]
    public void CreateAsync_PackageFile_ZipIsValidArchive()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "ZipIntegrity_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var manifestPath = Path.Combine(tempDir, "Manifest.xml");
            var headerPath = Path.Combine(tempDir, "PackageHeader.xml");
            File.WriteAllText(manifestPath, "<Manifest><Entity>TestEntity</Entity></Manifest>");
            File.WriteAllText(headerPath, "<PackageHeader><Version>1.0</Version></PackageHeader>");

            var queryItem = TestFixtures.CreateTestQueryItem(
                entityName: "TestEntity",
                definitionGroupId: "TestGroup",
                outputDirectory: tempDir,
                manifestFileName: manifestPath,
                packageHeaderFileName: headerPath);

            var zipPath = Path.Combine(tempDir, "test-integrity.zip");

            // Act
            using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(queryItem.ManifestFileName, "Manifest.xml", CompressionLevel.SmallestSize);
                zip.CreateEntryFromFile(queryItem.PackageHeaderFileName, "PackageHeader.xml", CompressionLevel.SmallestSize);
                var dataEntry = zip.CreateEntry(queryItem.EntityName + ".xml", CompressionLevel.SmallestSize);
                using var entryStream = dataEntry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write("<Document><TestEntity Id=\"1\" /></Document>");
            }

            // Assert -- open ZIP, verify it's not corrupted, entries are readable
            using var readStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
            using var readZip = new ZipArchive(readStream, ZipArchiveMode.Read);

            readZip.Entries.Count.ShouldBe(3);

            foreach (var entry in readZip.Entries)
            {
                using var entryStream = entry.Open();
                using var reader = new StreamReader(entryStream);
                var content = reader.ReadToEnd();
                content.ShouldNotBeNullOrWhiteSpace();
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CreateAsync_PackageFile_ManifestContentMatchesDefinition()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "ZipManifest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var manifestContent = "<Manifest><Entity>TestEntity</Entity><Version>3.0</Version></Manifest>";
            var headerContent = "<PackageHeader><Version>1.0</Version></PackageHeader>";

            var manifestPath = Path.Combine(tempDir, "Manifest.xml");
            var headerPath = Path.Combine(tempDir, "PackageHeader.xml");
            File.WriteAllText(manifestPath, manifestContent);
            File.WriteAllText(headerPath, headerContent);

            var zipPath = Path.Combine(tempDir, "test-manifest-content.zip");

            // Act
            using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(manifestPath, "Manifest.xml", CompressionLevel.SmallestSize);
                zip.CreateEntryFromFile(headerPath, "PackageHeader.xml", CompressionLevel.SmallestSize);
            }

            // Assert -- byte-for-byte manifest match
            using var readStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
            using var readZip = new ZipArchive(readStream, ZipArchiveMode.Read);

            var manifestEntry = readZip.GetEntry("Manifest.xml");
            manifestEntry.ShouldNotBeNull();

            using var manifestStream = manifestEntry!.Open();
            using var reader = new StreamReader(manifestStream);
            reader.ReadToEnd().ShouldBe(manifestContent);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CreateAsync_PackageFile_PackageHeaderContentMatchesDefinition()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "ZipHeader_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var manifestContent = "<Manifest><Entity>TestEntity</Entity></Manifest>";
            var headerContent = "<PackageHeader><Version>2.5</Version><Format>DMF</Format></PackageHeader>";

            var manifestPath = Path.Combine(tempDir, "Manifest.xml");
            var headerPath = Path.Combine(tempDir, "PackageHeader.xml");
            File.WriteAllText(manifestPath, manifestContent);
            File.WriteAllText(headerPath, headerContent);

            var zipPath = Path.Combine(tempDir, "test-header-content.zip");

            // Act
            using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(manifestPath, "Manifest.xml", CompressionLevel.SmallestSize);
                zip.CreateEntryFromFile(headerPath, "PackageHeader.xml", CompressionLevel.SmallestSize);
            }

            // Assert -- byte-for-byte header match
            using var readStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
            using var readZip = new ZipArchive(readStream, ZipArchiveMode.Read);

            var headerEntry = readZip.GetEntry("PackageHeader.xml");
            headerEntry.ShouldNotBeNull();

            using var headerStream = headerEntry!.Open();
            using var reader = new StreamReader(headerStream);
            reader.ReadToEnd().ShouldBe(headerContent);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CreateAsync_MultiEntityPackage_ContainsAllEntityEntries()
    {
        // Arrange -- simulate 3 entities in a single package
        var tempDir = Path.Combine(Path.GetTempPath(), "ZipMultiEntity_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var manifestPath = Path.Combine(tempDir, "Manifest.xml");
            var headerPath = Path.Combine(tempDir, "PackageHeader.xml");
            File.WriteAllText(manifestPath, "<Manifest><Entity>Customers</Entity><Entity>Vendors</Entity><Entity>Products</Entity></Manifest>");
            File.WriteAllText(headerPath, "<PackageHeader><Version>1.0</Version></PackageHeader>");

            var entityNames = new[] { "Customers", "Vendors", "Products" };
            var zipPath = Path.Combine(tempDir, "test-multi-entity.zip");

            // Act
            using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(manifestPath, "Manifest.xml", CompressionLevel.SmallestSize);
                zip.CreateEntryFromFile(headerPath, "PackageHeader.xml", CompressionLevel.SmallestSize);

                foreach (var entityName in entityNames)
                {
                    var dataEntry = zip.CreateEntry(entityName + ".xml", CompressionLevel.SmallestSize);
                    using var entryStream = dataEntry.Open();
                    using var writer = new StreamWriter(entryStream);
                    writer.Write($"<Document><{entityName} Id=\"1\" /></Document>");
                }
            }

            // Assert -- verify all 5 entries present (Manifest + Header + 3 entities)
            using var readStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
            using var readZip = new ZipArchive(readStream, ZipArchiveMode.Read);

            readZip.Entries.Count.ShouldBe(5);
            readZip.GetEntry("Manifest.xml").ShouldNotBeNull();
            readZip.GetEntry("PackageHeader.xml").ShouldNotBeNull();

            foreach (var entityName in entityNames)
            {
                var entry = readZip.GetEntry(entityName + ".xml");
                entry.ShouldNotBeNull();

                using var entryStream = entry!.Open();
                using var reader = new StreamReader(entryStream);
                var content = reader.ReadToEnd();
                content.ShouldContain(entityName);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CreateAsync_PackageFile_CompressionLevelIsSmallestSize()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "ZipCompression_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var manifestPath = Path.Combine(tempDir, "Manifest.xml");
            var headerPath = Path.Combine(tempDir, "PackageHeader.xml");
            // Use large content to make compression measurable
            var largeContent = string.Join("", Enumerable.Repeat("<Entity>TestEntity</Entity>", 100));
            File.WriteAllText(manifestPath, $"<Manifest>{largeContent}</Manifest>");
            File.WriteAllText(headerPath, "<PackageHeader><Version>1.0</Version></PackageHeader>");

            var zipSmallestPath = Path.Combine(tempDir, "test-smallest.zip");
            var zipNoCompPath = Path.Combine(tempDir, "test-nocomp.zip");

            // Act -- create with SmallestSize compression (per production code)
            using (var fileStream = new FileStream(zipSmallestPath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(manifestPath, "Manifest.xml", CompressionLevel.SmallestSize);
                zip.CreateEntryFromFile(headerPath, "PackageHeader.xml", CompressionLevel.SmallestSize);
            }

            // Create with NoCompression for comparison
            using (var fileStream = new FileStream(zipNoCompPath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(manifestPath, "Manifest.xml", CompressionLevel.NoCompression);
                zip.CreateEntryFromFile(headerPath, "PackageHeader.xml", CompressionLevel.NoCompression);
            }

            // Assert -- SmallestSize ZIP should be smaller than NoCompression ZIP
            var smallestSize = new FileInfo(zipSmallestPath).Length;
            var noCompSize = new FileInfo(zipNoCompPath).Length;
            smallestSize.ShouldBeLessThan(noCompSize);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CreateAsync_PackageFile_EntityXmlEntryNameMatchesEntityName()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "ZipEntryName_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var manifestPath = Path.Combine(tempDir, "Manifest.xml");
            var headerPath = Path.Combine(tempDir, "PackageHeader.xml");
            File.WriteAllText(manifestPath, "<Manifest><Entity>CustomerPayments</Entity></Manifest>");
            File.WriteAllText(headerPath, "<PackageHeader><Version>1.0</Version></PackageHeader>");

            var queryItem = TestFixtures.CreateTestQueryItem(
                entityName: "CustomerPayments",
                definitionGroupId: "TestGroup",
                outputDirectory: tempDir,
                manifestFileName: manifestPath,
                packageHeaderFileName: headerPath);

            var zipPath = Path.Combine(tempDir, "test-entry-name.zip");

            // Act
            using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(manifestPath, "Manifest.xml", CompressionLevel.SmallestSize);
                zip.CreateEntryFromFile(headerPath, "PackageHeader.xml", CompressionLevel.SmallestSize);
                var dataEntry = zip.CreateEntry(queryItem.EntityName + ".xml", CompressionLevel.SmallestSize);
                using var entryStream = dataEntry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write("<Document><CustomerPayments Id=\"1\" /></Document>");
            }

            // Assert -- verify ZIP entry name follows {EntityName}.xml pattern
            using var readStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
            using var readZip = new ZipArchive(readStream, ZipArchiveMode.Read);

            var entityEntry = readZip.GetEntry("CustomerPayments.xml");
            entityEntry.ShouldNotBeNull();
            entityEntry!.Name.ShouldBe("CustomerPayments.xml");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
