namespace Dynamics365ImportData.Tests.Unit.Fingerprinting;

using Dynamics365ImportData.Fingerprinting;

using Shouldly;

using Xunit;

public class ErrorFingerprinterTests
{
    private readonly ErrorFingerprinter _fingerprinter = new();

    [Fact]
    public void ComputeFingerprint_SameEntityAndMessage_ReturnsSameFingerprint()
    {
        // Arrange
        var entity = "Customers";
        var message = "Record not found in table";

        // Act
        var fingerprint1 = _fingerprinter.ComputeFingerprint(entity, message);
        var fingerprint2 = _fingerprinter.ComputeFingerprint(entity, message);

        // Assert
        fingerprint1.ShouldBe(fingerprint2);
    }

    [Fact]
    public void ComputeFingerprint_DifferentEntities_ReturnsDifferentFingerprints()
    {
        // Arrange
        var message = "Record not found in table";

        // Act
        var fingerprint1 = _fingerprinter.ComputeFingerprint("Customers", message);
        var fingerprint2 = _fingerprinter.ComputeFingerprint("Vendors", message);

        // Assert
        fingerprint1.ShouldNotBe(fingerprint2);
    }

    [Fact]
    public void ComputeFingerprint_MessageWithGuid_StripsGuidBeforeHashing()
    {
        // Arrange
        var entity = "Customers";
        var message1 = "Record 3a4b5c6d-1234-5678-9abc-def012345678 not found";
        var message2 = "Record 7e8f9a0b-abcd-ef01-2345-678901234567 not found";

        // Act
        var fingerprint1 = _fingerprinter.ComputeFingerprint(entity, message1);
        var fingerprint2 = _fingerprinter.ComputeFingerprint(entity, message2);

        // Assert
        fingerprint1.ShouldBe(fingerprint2);
    }

    [Fact]
    public void ComputeFingerprint_MessageWithTimestamp_StripsTimestampBeforeHashing()
    {
        // Arrange
        var entity = "Customers";
        var message1 = "Failed at 2026-01-15T10:30:00Z during import";
        var message2 = "Failed at 2026-02-01T14:00:00+05:00 during import";

        // Act
        var fingerprint1 = _fingerprinter.ComputeFingerprint(entity, message1);
        var fingerprint2 = _fingerprinter.ComputeFingerprint(entity, message2);

        // Assert
        fingerprint1.ShouldBe(fingerprint2);
    }

    [Fact]
    public void ComputeFingerprint_MessageWithRecordId_StripsRecordIdBeforeHashing()
    {
        // Arrange
        var entity = "Customers";
        var message1 = "Row 123456 is invalid in staging table";
        var message2 = "Row 789012 is invalid in staging table";

        // Act
        var fingerprint1 = _fingerprinter.ComputeFingerprint(entity, message1);
        var fingerprint2 = _fingerprinter.ComputeFingerprint(entity, message2);

        // Assert
        fingerprint1.ShouldBe(fingerprint2);
    }

    [Fact]
    public void ComputeFingerprint_MessageWithDifferentWhitespace_ReturnsSameFingerprint()
    {
        // Arrange
        var entity = "Customers";
        var message1 = "Record  not   found  in table";
        var message2 = "Record not found in table";

        // Act
        var fingerprint1 = _fingerprinter.ComputeFingerprint(entity, message1);
        var fingerprint2 = _fingerprinter.ComputeFingerprint(entity, message2);

        // Assert
        fingerprint1.ShouldBe(fingerprint2);
    }

    [Fact]
    public void ComputeFingerprint_MessageWithDifferentCase_ReturnsSameFingerprint()
    {
        // Arrange
        var entity = "Customers";
        var message1 = "Record NOT FOUND in table";
        var message2 = "Record not found in table";

        // Act
        var fingerprint1 = _fingerprinter.ComputeFingerprint(entity, message1);
        var fingerprint2 = _fingerprinter.ComputeFingerprint(entity, message2);

        // Assert
        fingerprint1.ShouldBe(fingerprint2);
    }

    [Fact]
    public void ComputeFingerprint_SameLogicalErrorDifferentRunData_ReturnsSameFingerprint()
    {
        // Arrange -- comprehensive: same error with different GUIDs, timestamps, record IDs
        var entity = "Customers";
        var message1 = "Record 3a4b5c6d-1234-5678-9abc-def012345678 failed at 2026-01-15T10:30:00Z row 123456 NOT FOUND";
        var message2 = "Record 7e8f9a0b-abcd-ef01-2345-678901234567 failed at 2026-02-01T14:00:00+05:00 row 789012 not found";

        // Act
        var fingerprint1 = _fingerprinter.ComputeFingerprint(entity, message1);
        var fingerprint2 = _fingerprinter.ComputeFingerprint(entity, message2);

        // Assert
        fingerprint1.ShouldBe(fingerprint2);
    }

    [Fact]
    public void ComputeFingerprint_DifferentErrors_ReturnsDifferentFingerprints()
    {
        // Arrange
        var entity = "Customers";
        var message1 = "Record not found in table";
        var message2 = "Duplicate key violation in table";

        // Act
        var fingerprint1 = _fingerprinter.ComputeFingerprint(entity, message1);
        var fingerprint2 = _fingerprinter.ComputeFingerprint(entity, message2);

        // Assert
        fingerprint1.ShouldNotBe(fingerprint2);
    }

    [Fact]
    public void ComputeFingerprint_NullMessage_ReturnsValidFingerprint()
    {
        // Arrange & Act
        var fingerprint = _fingerprinter.ComputeFingerprint("Customers", null!);

        // Assert
        fingerprint.ShouldNotBeNullOrEmpty();
        fingerprint.Length.ShouldBe(16);
    }

    [Fact]
    public void ComputeFingerprint_EmptyMessage_ReturnsValidFingerprint()
    {
        // Arrange & Act
        var fingerprint = _fingerprinter.ComputeFingerprint("Customers", string.Empty);

        // Assert
        fingerprint.ShouldNotBeNullOrEmpty();
        fingerprint.Length.ShouldBe(16);
    }

    [Fact]
    public void ComputeFingerprint_Returns16HexChars()
    {
        // Arrange & Act
        var fingerprint = _fingerprinter.ComputeFingerprint("Customers", "Some error message");

        // Assert
        fingerprint.Length.ShouldBe(16);
        fingerprint.ShouldMatch("^[0-9a-f]{16}$");
    }

    [Fact]
    public void ComputeFingerprint_CommonDatetimeFormat_StripsDatetime()
    {
        // Arrange -- US format datetime normalization
        var entity = "Customers";
        var message1 = "Error at 1/15/2026 10:30:00 AM during processing";
        var message2 = "Error at 2/1/2026 2:00:00 PM during processing";

        // Act
        var fingerprint1 = _fingerprinter.ComputeFingerprint(entity, message1);
        var fingerprint2 = _fingerprinter.ComputeFingerprint(entity, message2);

        // Assert
        fingerprint1.ShouldBe(fingerprint2);
    }
}
