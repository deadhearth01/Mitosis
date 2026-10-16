using Mitosis.Domain;

namespace Mitosis.Domain.Tests;

[TestClass]
public sealed class CloneNameTests
{
    [TestMethod]
    public void Compose_AppAndLabel_FormatsAsAppParensLabel()
    {
        Assert.AreEqual("Slack (Work)", CloneName.Compose("Slack", "Work"));
    }

    [TestMethod]
    public void Compose_EmptyLabel_ReturnsAppName()
    {
        Assert.AreEqual("Slack", CloneName.Compose("Slack", "   "));
    }

    [TestMethod]
    public void Compose_TrimsWhitespace()
    {
        Assert.AreEqual("Slack (Work)", CloneName.Compose("  Slack ", " Work  "));
    }

    [TestMethod]
    public void Validate_ReturnsTrimmedName()
    {
        Assert.AreEqual("Slack (Work)", CloneName.Validate("  Slack (Work)  "));
    }

    [TestMethod]
    public void Validate_EmptyInput_ThrowsEmpty()
    {
        foreach (var raw in new string?[] { null, "", "   ", "\n" })
        {
            var ex = Assert.ThrowsExactly<CloneNameException>(() => CloneName.Validate(raw));
            Assert.AreEqual(CloneNameError.Empty, ex.Error);
            Assert.AreEqual("Give the clone a name.", ex.Message);
        }
    }

    [TestMethod]
    public void Validate_TooLong_ThrowsTooLong()
    {
        var ex = Assert.ThrowsExactly<CloneNameException>(() => CloneName.Validate(new string('x', 61)));
        Assert.AreEqual(CloneNameError.TooLong, ex.Error);
        Assert.AreEqual("Clone names can be at most 60 characters.", ex.Message);
    }

    [TestMethod]
    public void Validate_MaxLength_Passes()
    {
        var name = new string('x', 60);
        Assert.AreEqual(name, CloneName.Validate(name));
    }

    [TestMethod]
    public void Validate_LeadingDot_Throws()
    {
        var ex = Assert.ThrowsExactly<CloneNameException>(() => CloneName.Validate(".hidden"));
        Assert.AreEqual(CloneNameError.LeadingDot, ex.Error);
        Assert.AreEqual("Clone names can't start with a dot.", ex.Message);
    }

    [DataTestMethod]
    [DataRow('/')]
    [DataRow(':')]
    [DataRow('\n')]
    [DataRow('\r')]
    [DataRow('\0')]
    public void Validate_ForbiddenCharacter_ThrowsInvalidCharacter(char bad)
    {
        var ex = Assert.ThrowsExactly<CloneNameException>(() => CloneName.Validate($"App{bad}Label"));
        Assert.AreEqual(CloneNameError.InvalidCharacter, ex.Error);
        Assert.AreEqual(bad, ex.InvalidCharacter);
    }

    [TestMethod]
    public void Validate_ReportsFirstInvalidCharacter()
    {
        var ex = Assert.ThrowsExactly<CloneNameException>(() => CloneName.Validate("a/b:c"));
        Assert.AreEqual('/', ex.InvalidCharacter);
    }
}
