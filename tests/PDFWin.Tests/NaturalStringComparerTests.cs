using System.Collections.Generic;
using PDFWin.Native;
using Xunit;

namespace PDFWin.Tests;

public class NaturalStringComparerTests
{
    [Fact]
    public void Compares_NumericSuffixes_InExplorerOrder()
    {
        var input = new List<string> { "item10.jpg", "item1.jpg", "item2.jpg", "item20.jpg" };
        input.Sort(NaturalStringComparer.Instance);

        Assert.Equal(new[] { "item1.jpg", "item2.jpg", "item10.jpg", "item20.jpg" }, input);
    }

    [Fact]
    public void Compares_PaddedZeros_Properly()
    {
        var input = new List<string> { "doc_002.pdf", "doc_001.pdf", "doc_010.pdf" };
        input.Sort(NaturalStringComparer.Instance);

        Assert.Equal(new[] { "doc_001.pdf", "doc_002.pdf", "doc_010.pdf" }, input);
    }

    [Fact]
    public void Handles_NullsGracefully()
    {
        Assert.Equal(0, NaturalStringComparer.Instance.Compare(null, null));
        Assert.True(NaturalStringComparer.Instance.Compare(null, "a") < 0);
        Assert.True(NaturalStringComparer.Instance.Compare("a", null) > 0);
    }

    [Fact]
    public void IsCaseInsensitive()
    {
        Assert.Equal(0, NaturalStringComparer.Instance.Compare("FILE.PNG", "file.png"));
    }
}
