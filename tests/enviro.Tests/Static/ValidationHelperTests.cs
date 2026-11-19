using enviro.Static;
using Xunit;

namespace enviro.Tests.Static;

public class ValidationHelperTests
{
    [Theory]
    [InlineData("FOO", "bar", true)]
    [InlineData("FOO", "a=b;c\"d", true)]   // '=', ';', quotes are legal inside a value
    [InlineData("FOO", "  spaced  ", true)]
    [InlineData("", "bar", false)]
    [InlineData("   ", "bar", false)]
    [InlineData("FOO", "", false)]
    [InlineData("FOO", "   ", false)]
    [InlineData("A=B", "bar", false)]       // '=' is forbidden in a name
    [InlineData("A\0B", "bar", false)]       // NUL is forbidden in a name
    [InlineData("FOO", "ba\0r", false)]      // NUL is forbidden in a value
    public void IsValidEnvironmentVariableInput_applies_the_input_rules(string name, string value, bool expected) =>
        Assert.Equal(expected, ValidationHelper.IsValidEnvironmentVariableInput(name, value));

    [Theory]
    [InlineData("PATH", "anything", true)]
    [InlineData("path", "anything", true)]  // case-insensitive
    [InlineData(" Path ", "anything", true)]
    [InlineData("FOO", "a;b", true)]         // a separator makes it a list
    [InlineData("FOO", "abc", false)]
    public void IsPathList_detects_path_semantics(string name, string value, bool expected) =>
        Assert.Equal(expected, ValidationHelper.IsPathList(name, value));

    [Fact]
    public void SplitPathList_drops_blank_segments_and_keeps_order()
    {
        Assert.Equal(["a", "b", "c"], ValidationHelper.SplitPathList("a;;b; ;c;"));
        Assert.Equal(["a", " b "], ValidationHelper.SplitPathList("a; b ")); // segments are not trimmed
        Assert.Empty(ValidationHelper.SplitPathList(";; ;"));
        Assert.Empty(ValidationHelper.SplitPathList(""));
    }

    [Fact]
    public void JoinPathList_drops_blank_segments() =>
        Assert.Equal("a;b", ValidationHelper.JoinPathList(["a", "", "  ", "b"]));

    [Theory]
    [InlineData(@"\\host\share", false)]     // UNC is never treated as a local path
    [InlineData("//host/share", false)]
    [InlineData("plain text", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsPathToDirectory_rejects_unc_and_plain_text(string path, bool expected) =>
        Assert.Equal(expected, ValidationHelper.IsPathToDirectory(path));

    [Fact]
    public void IsPathToDirectory_accepts_a_rooted_local_path() =>
        Assert.True(ValidationHelper.IsPathToDirectory(
            Path.GetPathRoot(Directory.GetCurrentDirectory())!));

    [Fact]
    public void IsMissingPath_and_IsExistingDirectory_resolve_local_locations()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var file = Path.Combine(dir.FullName, "file.txt");
        File.WriteAllText(file, "x");
        var missing = Path.Combine(dir.FullName, "nope");

        try
        {
            Assert.False(ValidationHelper.IsMissingPath(dir.FullName));
            Assert.True(ValidationHelper.IsMissingPath(missing));
            Assert.False(ValidationHelper.IsMissingPath(@"\\host\share")); // UNC is not probed, reads as text
            Assert.False(ValidationHelper.IsMissingPath("plain text"));

            Assert.True(ValidationHelper.IsExistingDirectory(dir.FullName));
            Assert.False(ValidationHelper.IsExistingDirectory(file));      // a file is not a folder
            Assert.False(ValidationHelper.IsExistingDirectory(missing));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("FOO", "bar", null)]
    [InlineData("", "bar", "Name and value cannot be empty or consist only of spaces.")]
    [InlineData("FOO", "   ", "Name and value cannot be empty or consist only of spaces.")]
    [InlineData("A=B", "bar", "Name cannot contain '=' or a null character.")]
    [InlineData("FOO", "ba\0r", "Value cannot contain a null character.")]
    public void InputError_names_the_rejection_reason(string name, string value, string? expected) =>
        Assert.Equal(expected, ValidationHelper.InputError(name, value));

    [Theory]
    [InlineData("FOO", "bar")]
    [InlineData("A=B", "bar")]
    [InlineData("", "bar")]
    public void InputError_agrees_with_IsValidEnvironmentVariableInput(string name, string value) =>
        Assert.Equal(
            ValidationHelper.IsValidEnvironmentVariableInput(name, value),
            ValidationHelper.InputError(name, value) is null);
}
