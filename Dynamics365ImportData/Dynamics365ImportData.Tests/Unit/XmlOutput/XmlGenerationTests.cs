namespace Dynamics365ImportData.Tests.Unit.XmlOutput;

using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;
using Dynamics365ImportData.XmlOutput;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using System.Text;
using System.Xml;

using Xunit;

/// <summary>
/// Characterization tests for XML output format patterns used by SqlToXmlService.
/// These tests exercise XmlOutputPart and XmlWriter directly to capture the XML structure,
/// attribute escaping, and null-handling behavior. They do NOT call SqlToXmlService.ExportToOutput
/// directly because that method requires a live SqlConnection (FR32 prohibits external dependencies).
/// The XML writing patterns tested here mirror the production write sequence in SqlToXmlService.
/// </summary>
public class XmlGenerationTests
{
    [Fact]
    public void ExportToOutput_SingleEntity_WritesCorrectXmlStructure()
    {
        // Arrange
        var stream = new MemoryStream();
        var logger = Substitute.For<ILogger<XmlOutputPart>>();
        var part = new XmlOutputPart(
            stream,
            "test-part",
            (_, __) => Task.CompletedTask,
            (_, __) => Task.FromResult(ExecutionStatus.Succeeded),
            logger);

        // Act
        part.Open();
        var writer = part.Writer;
        writer.WriteStartDocument();
        writer.WriteStartElement("Document");
        writer.WriteStartElement("TestEntity");
        writer.WriteStartAttribute("Name");
        writer.WriteValue("Value1");
        writer.WriteEndAttribute();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        // Assert
        stream.Position = 0;
        var xml = Encoding.UTF8.GetString(stream.ToArray());
        xml.ShouldContain("<Document>");
        xml.ShouldContain("<TestEntity");
        xml.ShouldContain("Name=\"Value1\"");
        xml.ShouldContain("</Document>");

        part.Close();
    }

    [Fact]
    public void ExportToOutput_SpecialCharacters_EscapesCorrectly()
    {
        // Arrange
        var stream = new MemoryStream();
        var logger = Substitute.For<ILogger<XmlOutputPart>>();
        var part = new XmlOutputPart(
            stream,
            "test-part",
            (_, __) => Task.CompletedTask,
            (_, __) => Task.FromResult(ExecutionStatus.Succeeded),
            logger);

        // Act
        part.Open();
        var writer = part.Writer;
        writer.WriteStartDocument();
        writer.WriteStartElement("Document");
        writer.WriteStartElement("TestEntity");
        writer.WriteStartAttribute("Special");
        writer.WriteValue("A & B < C > D \"E\" 'F'");
        writer.WriteEndAttribute();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        // Assert
        stream.Position = 0;
        var xml = Encoding.UTF8.GetString(stream.ToArray());
        // XmlWriter escapes & < > " in attributes per XML spec
        xml.ShouldContain("&amp;");
        xml.ShouldContain("&lt;");
        xml.ShouldContain("&gt;");
        xml.ShouldContain("&quot;");

        part.Close();
    }

    [Fact]
    public void ExportToOutput_NullFields_HandlesDbNull()
    {
        // Arrange
        var stream = new MemoryStream();
        var logger = Substitute.For<ILogger<XmlOutputPart>>();
        var part = new XmlOutputPart(
            stream,
            "test-part",
            (_, __) => Task.CompletedTask,
            (_, __) => Task.FromResult(ExecutionStatus.Succeeded),
            logger);

        // Act -- simulate the SqlToXmlService behavior for DBNull: write empty attribute
        part.Open();
        var writer = part.Writer;
        writer.WriteStartDocument();
        writer.WriteStartElement("Document");
        writer.WriteStartElement("TestEntity");
        // When value is DBNull, SqlToXmlService skips WriteValue, producing empty attribute
        writer.WriteStartAttribute("NullField");
        // No WriteValue call -- simulates DBNull handling
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("NonNullField");
        writer.WriteValue("SomeValue");
        writer.WriteEndAttribute();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        // Assert
        stream.Position = 0;
        var xml = Encoding.UTF8.GetString(stream.ToArray());
        xml.ShouldContain("NullField=\"\"");
        xml.ShouldContain("NonNullField=\"SomeValue\"");

        part.Close();
    }

    [Fact]
    public void ExportToOutput_MultipleRecords_WritesAllRows()
    {
        // Arrange
        var stream = new MemoryStream();
        var logger = Substitute.For<ILogger<XmlOutputPart>>();
        var part = new XmlOutputPart(
            stream,
            "test-part",
            (_, __) => Task.CompletedTask,
            (_, __) => Task.FromResult(ExecutionStatus.Succeeded),
            logger);

        // Act
        part.Open();
        var writer = part.Writer;
        writer.WriteStartDocument();
        writer.WriteStartElement("Document");
        for (int i = 1; i <= 3; i++)
        {
            writer.WriteStartElement("TestEntity");
            writer.WriteStartAttribute("Id");
            writer.WriteValue(i);
            writer.WriteEndAttribute();
            writer.WriteStartAttribute("Name");
            writer.WriteValue($"Record{i}");
            writer.WriteEndAttribute();
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var xml = reader.ReadToEnd();
        xml.ShouldContain("Id=\"1\"");
        xml.ShouldContain("Id=\"2\"");
        xml.ShouldContain("Id=\"3\"");
        xml.ShouldContain("Name=\"Record1\"");
        xml.ShouldContain("Name=\"Record2\"");
        xml.ShouldContain("Name=\"Record3\"");

        // Verify the XML is well-formed by parsing it (strip BOM for XmlDocument)
        var doc = new XmlDocument();
        doc.LoadXml(xml.TrimStart('\uFEFF'));
        doc.DocumentElement!.ChildNodes.Count.ShouldBe(3);

        part.Close();
    }
}
