namespace Dynamics365ImportData.Tests.Integration.Packaging;

using Dynamics365ImportData.Tests.TestHelpers;

using Shouldly;

using System.IO.Compression;

using Xunit;

/// <summary>
/// Characterization tests for ZIP package structure used by XmlPackageOutputFactoryBase.
/// These tests replicate the ZIP creation logic directly because XmlPackageOutputFactoryBase
/// is internal abstract and requires IServiceProvider + ILogger dependencies that tightly couple
/// to the DI container. The tests verify the ZIP structure pattern (Manifest.xml, PackageHeader.xml,
/// {EntityName}.xml entries with SmallestSize compression) that must be preserved during .NET 10 upgrade.
/// </summary>
public class ZipPackageTests
{
    [Fact]
    public void CreateAsync_PackageFile_ContainsManifestAndHeader()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "ZipPackageTests_" + Guid.NewGuid().ToString("N"));
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

            var zipPath = Path.Combine(tempDir, "test-package.zip");

            // Act -- create a ZIP package mimicking XmlPackageOutputFactoryBase behavior
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

            // Assert -- verify ZIP contains expected entries
            using var readStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
            using var readZip = new ZipArchive(readStream, ZipArchiveMode.Read);

            readZip.Entries.Count.ShouldBe(3);

            var manifestEntry = readZip.GetEntry("Manifest.xml");
            manifestEntry.ShouldNotBeNull();

            var headerEntry = readZip.GetEntry("PackageHeader.xml");
            headerEntry.ShouldNotBeNull();

            var dataEntryRead = readZip.GetEntry("TestEntity.xml");
            dataEntryRead.ShouldNotBeNull();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CreateAsync_PackageFile_ManifestMatchesSourceFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "ZipPackageTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var manifestContent = "<Manifest><Entity>TestEntity</Entity><Version>2.0</Version></Manifest>";
            var headerContent = "<PackageHeader><Version>1.0</Version></PackageHeader>";

            var manifestPath = Path.Combine(tempDir, "Manifest.xml");
            var headerPath = Path.Combine(tempDir, "PackageHeader.xml");
            File.WriteAllText(manifestPath, manifestContent);
            File.WriteAllText(headerPath, headerContent);

            var queryItem = TestFixtures.CreateTestQueryItem(
                entityName: "TestEntity",
                definitionGroupId: "TestGroup",
                outputDirectory: tempDir,
                manifestFileName: manifestPath,
                packageHeaderFileName: headerPath);

            var zipPath = Path.Combine(tempDir, "test-manifest-match.zip");

            // Act
            using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(queryItem.ManifestFileName, "Manifest.xml", CompressionLevel.SmallestSize);
                zip.CreateEntryFromFile(queryItem.PackageHeaderFileName, "PackageHeader.xml", CompressionLevel.SmallestSize);
            }

            // Assert -- read back manifest and verify content matches source file
            using var readStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
            using var readZip = new ZipArchive(readStream, ZipArchiveMode.Read);

            var manifestEntry = readZip.GetEntry("Manifest.xml");
            manifestEntry.ShouldNotBeNull();

            using var manifestStream = manifestEntry!.Open();
            using var reader = new StreamReader(manifestStream);
            var extractedManifest = reader.ReadToEnd();

            extractedManifest.ShouldBe(manifestContent);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
