namespace Dynamics365ImportData.Tests.Snapshot;

using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;
using Dynamics365ImportData.XmlOutput;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using System.Text;

using Xunit;

/// <summary>
/// Snapshot test for mixed-type XML output. Uses test-generated XML compared against a
/// golden file because SqlToXmlService.ExportToOutput requires live SQL (FR32 constraint).
/// Guards against regressions in XmlOutputPart's XmlWriter settings (encoding, indent,
/// attribute escaping) and serves as a reference for the expected D365FO XML format.
/// </summary>
public class XmlFieldTypeSnapshotTests
{
    [Fact]
    public void ExportToOutput_MixedFieldTypes_MatchesGoldenFile()
    {
        // Arrange
        var goldenFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Snapshot", "GoldenFiles", "mixed-field-types-output.xml");

        var stream = new MemoryStream();
        var logger = Substitute.For<ILogger<XmlOutputPart>>();
        var part = new XmlOutputPart(
            stream,
            "mixed-type-snapshot",
            (_, __) => Task.CompletedTask,
            (_, __) => Task.FromResult(ExecutionStatus.Succeeded),
            logger);

        // Act -- write two records with mixed field types
        part.Open();
        var writer = part.Writer;
        writer.WriteStartDocument();
        writer.WriteStartElement("Document");

        // Record 1: integer, string, datetime, empty, null, special chars
        writer.WriteStartElement("TestEntity");
        writer.WriteStartAttribute("IntField");
        writer.WriteValue(42);
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("StringField");
        writer.WriteValue("Hello World");
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("DateField");
        writer.WriteValue(new DateTime(2026, 1, 15, 14, 30, 0, DateTimeKind.Utc));
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("EmptyField");
        writer.WriteValue("");
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("NullField");
        // No WriteValue -- simulates DBNull
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("SpecialChars");
        writer.WriteValue("A & B < C > D \"E\"");
        writer.WriteEndAttribute();
        writer.WriteEndElement();

        // Record 2: different values
        writer.WriteStartElement("TestEntity");
        writer.WriteStartAttribute("IntField");
        writer.WriteValue(99);
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("StringField");
        writer.WriteValue("Second Record");
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("DateField");
        writer.WriteValue(new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc));
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("EmptyField");
        writer.WriteValue("not empty");
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("NullField");
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("SpecialChars");
        writer.WriteValue("Plain text");
        writer.WriteEndAttribute();
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        // Assert -- compare as strings, trimming BOM from both sides
        stream.Position = 0;
        var actualXml = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true).ReadToEnd();
        var expectedXml = File.ReadAllText(goldenFilePath, Encoding.UTF8);

        actualXml.TrimStart('\uFEFF').ShouldBe(expectedXml.TrimStart('\uFEFF'));

        part.Close();
    }
}
