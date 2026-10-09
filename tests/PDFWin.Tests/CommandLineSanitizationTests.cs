using System;
using System.IO;
using Xunit;

namespace PDFWin.Tests;

public class CommandLineSanitizationTests
{
    [Theory]
    [InlineData(@"C:\Users\Test\Pictures\", @"C:\Users\Test\Pictures")]
    [InlineData(@"C:\Users\Test\Pictures\""", @"C:\Users\Test\Pictures")]
    [InlineData(@"""C:\Users\Test\Pictures""", @"C:\Users\Test\Pictures")]
    public void FolderPath_Sanitization_StripsTrailingQuotesAndSlashes(string rawInput, string expectedNormalized)
    {
        // When Windows Explorer passes directory verbs with quotes and trailing slashes,
        // Explorer invokes: "app.exe" --folder "C:\Folder\"
        // In Windows argv parsing, \" becomes an escaped quote!
        // The resulting argv[1] is C:\Folder"
        string normalized = rawInput.Trim().Trim('"', '\\');
        Assert.Equal(expectedNormalized, normalized);
    }
}
