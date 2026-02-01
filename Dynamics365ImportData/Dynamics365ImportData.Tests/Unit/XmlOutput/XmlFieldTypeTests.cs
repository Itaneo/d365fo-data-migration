namespace Dynamics365ImportData.Tests.Unit.XmlOutput;

using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;
using Dynamics365ImportData.XmlOutput;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using System.Text;

using Xunit;

public class XmlFieldTypeTests
{
    private static (XmlOutputPart part, MemoryStream stream) CreateTestPart()
    {
        var stream = new MemoryStream();
        var logger = Substitute.For<ILogger<XmlOutputPart>>();
        var part = new XmlOutputPart(
            stream,
            "field-type-test",
            (_, __) => Task.CompletedTask,
            (_, __) => Task.FromResult(ExecutionStatus.Succeeded),
            logger);
        return (part, stream);
    }

    private static string GetXml(MemoryStream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd();
    }

    [Fact]
    public void XmlWriter_IntegerFieldValue_WritesNumericString()
    {
        // Arrange
        var (part, stream) = CreateTestPart();

        // Act
        part.Open();
        var writer = part.Writer;
        writer.WriteStartDocument();
        writer.WriteStartElement("Document");
        writer.WriteStartElement("TestEntity");
        writer.WriteStartAttribute("IntField");
        writer.WriteValue(42);
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("LongField");
        writer.WriteValue(9876543210L);
        writer.WriteEndAttribute();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        // Assert
        var xml = GetXml(stream);
        xml.ShouldContain("IntField=\"42\"");
        xml.ShouldContain("LongField=\"9876543210\"");

        part.Close();
    }

    [Fact]
    public void XmlWriter_DateTimeFieldValue_WritesIso8601Format()
    {
        // Arrange
        var (part, stream) = CreateTestPart();
        var testDate = new DateTime(2026, 1, 15, 14, 30, 0, DateTimeKind.Utc);

        // Act
        part.Open();
        var writer = part.Writer;
        writer.WriteStartDocument();
        writer.WriteStartElement("Document");
        writer.WriteStartElement("TestEntity");
        writer.WriteStartAttribute("DateField");
        writer.WriteValue(testDate);
        writer.WriteEndAttribute();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        // Assert
        var xml = GetXml(stream);
        xml.ShouldContain("DateField=\"2026-01-15T14:30:00Z\"");

        part.Close();
    }

    [Fact]
    public void XmlWriter_LongStringFieldValue_WritesCompletely()
    {
        // Arrange
        var (part, stream) = CreateTestPart();
        var longString = new string('X', 5000); // > 4000 chars (MAX VARCHAR boundary)

        // Act
        part.Open();
        var writer = part.Writer;
        writer.WriteStartDocument();
        writer.WriteStartElement("Document");
        writer.WriteStartElement("TestEntity");
        writer.WriteStartAttribute("LongField");
        writer.WriteValue(longString);
        writer.WriteEndAttribute();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        // Assert
        var xml = GetXml(stream);
        xml.ShouldContain(longString);
        xml.Length.ShouldBeGreaterThan(5000);

        part.Close();
    }

    [Fact]
    public void XmlWriter_UnicodeCharacters_PreservesEncoding()
    {
        // Arrange
        var (part, stream) = CreateTestPart();

        // Act
        part.Open();
        var writer = part.Writer;
        writer.WriteStartDocument();
        writer.WriteStartElement("Document");
        writer.WriteStartElement("TestEntity");
        // CJK characters
        writer.WriteStartAttribute("CJK");
        writer.WriteValue("\u4F60\u597D\u4E16\u754C"); // 你好世界
        writer.WriteEndAttribute();
        // Emoji
        writer.WriteStartAttribute("Emoji");
        writer.WriteValue("\uD83D\uDE00\uD83D\uDE80"); // 😀🚀
        writer.WriteEndAttribute();
        // RTL (Arabic)
        writer.WriteStartAttribute("RTL");
        writer.WriteValue("\u0645\u0631\u062D\u0628\u0627"); // مرحبا
        writer.WriteEndAttribute();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        // Assert
        var xml = GetXml(stream);
        xml.ShouldContain("\u4F60\u597D\u4E16\u754C");
        xml.ShouldContain("\uD83D\uDE00\uD83D\uDE80");
        xml.ShouldContain("\u0645\u0631\u062D\u0628\u0627");

        part.Close();
    }

    [Fact]
    public void XmlWriter_EmptyStringField_WritesEmptyAttribute()
    {
        // Arrange
        var (part, stream) = CreateTestPart();

        // Act
        part.Open();
        var writer = part.Writer;
        writer.WriteStartDocument();
        writer.WriteStartElement("Document");
        writer.WriteStartElement("TestEntity");
        // Empty string: write value with empty string
        writer.WriteStartAttribute("EmptyField");
        writer.WriteValue("");
        writer.WriteEndAttribute();
        // Null (DBNull): skip WriteValue entirely
        writer.WriteStartAttribute("NullField");
        writer.WriteEndAttribute();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        // Assert -- both should produce empty attribute, but verify the pattern
        var xml = GetXml(stream);
        xml.ShouldContain("EmptyField=\"\"");
        xml.ShouldContain("NullField=\"\"");

        part.Close();
    }

    [Fact]
    public void XmlWriter_XmlReservedCharsInData_EscapesAllCorrectly()
    {
        // Arrange
        var (part, stream) = CreateTestPart();

        // Act
        part.Open();
        var writer = part.Writer;
        writer.WriteStartDocument();
        writer.WriteStartElement("Document");
        writer.WriteStartElement("TestEntity");
        // Comprehensive: & < > " ' plus CDATA-like sequences
        writer.WriteStartAttribute("Ampersand");
        writer.WriteValue("Tom & Jerry");
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("LessThan");
        writer.WriteValue("a < b");
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("GreaterThan");
        writer.WriteValue("a > b");
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("DoubleQuote");
        writer.WriteValue("He said \"hello\"");
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("SingleQuote");
        writer.WriteValue("It's fine");
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("CdataLike");
        writer.WriteValue("]]>");
        writer.WriteEndAttribute();
        writer.WriteStartAttribute("Mixed");
        writer.WriteValue("<script>alert(\"x&y\")</script>");
        writer.WriteEndAttribute();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        // Assert
        var xml = GetXml(stream);
        xml.ShouldContain("Ampersand=\"Tom &amp; Jerry\"");
        xml.ShouldContain("LessThan=\"a &lt; b\"");
        xml.ShouldContain("GreaterThan=\"a &gt; b\"");
        xml.ShouldContain("DoubleQuote=\"He said &quot;hello&quot;\"");
        // Single quotes in double-quoted attributes are valid XML -- no escaping needed
        xml.ShouldContain("SingleQuote=\"It's fine\"");
        // ]]> in attributes needs proper escaping
        xml.ShouldContain("CdataLike=\"]]&gt;\"");
        // Mixed content
        xml.ShouldContain("&lt;script&gt;alert(&quot;x&amp;y&quot;)&lt;/script&gt;");

        part.Close();
    }
}
