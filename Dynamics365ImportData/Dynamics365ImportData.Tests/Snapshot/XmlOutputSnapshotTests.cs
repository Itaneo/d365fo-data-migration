namespace Dynamics365ImportData.Tests.Snapshot;

using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;
using Dynamics365ImportData.XmlOutput;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using System.Text;

using Xunit;

/// <summary>
/// Golden-file snapshot test verifying XML output matches a known-good baseline.
/// Captures the exact byte-level output of XmlWriter with UTF8 encoding (including BOM behavior)
/// to detect any changes during .NET 10 upgrade or XmlWriter configuration changes.
/// </summary>
public class XmlOutputSnapshotTests
{
    [Fact]
    public void ExportToOutput_SingleEntity_MatchesGoldenFile()
    {
        // Arrange
        var goldenFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Snapshot", "GoldenFiles", "single-entity-output.xml");

        var stream = new MemoryStream();
        var logger = Substitute.For<ILogger<XmlOutputPart>>();
        var part = new XmlOutputPart(
            stream,
            "snapshot-test",
            (_, __) => Task.CompletedTask,
            (_, __) => Task.FromResult(ExecutionStatus.Succeeded),
            logger);

        // Act -- reproduce the exact XML structure from SqlToXmlService
        part.Open();
        var writer = part.Writer;
        writer.WriteStartDocument();
        writer.WriteStartElement("Document");
        writer.WriteStartElement("TestEntity");
        writer.WriteStartAttribute("Id");
        writer.WriteValue("1");
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("Name");
        writer.WriteValue("TestRecord");
        writer.WriteEndAttribute();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        // Assert -- compare as strings, trimming BOM from both sides
        // XmlWriter with UTF8 encoding produces a BOM preamble (EF BB BF)
        stream.Position = 0;
        var actualXml = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true).ReadToEnd();
        var expectedXml = File.ReadAllText(goldenFilePath, Encoding.UTF8);

        actualXml.TrimStart('\uFEFF').ShouldBe(expectedXml.TrimStart('\uFEFF'));

        part.Close();
    }
}
