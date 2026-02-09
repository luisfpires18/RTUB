using RTUB.Core.Helpers;

namespace RTUB.Core.Tests.Helpers;

public class NumberFormatterTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(999, "999")]
    [InlineData(1000, "1K")]
    [InlineData(1500, "1.5K")]
    [InlineData(1234, "1.23K")]
    [InlineData(10000, "10K")]
    [InlineData(99999, "100K")]
    [InlineData(100000, "100K")]
    [InlineData(999999, "1000K")]
    [InlineData(1000000, "1M")]
    [InlineData(1500000, "1.5M")]
    [InlineData(2500000, "2.5M")]
    [InlineData(1000000000, "1B")]
    [InlineData(1500000000, "1.5B")]
    public void Compact_Int_FormatsCorrectly(int value, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Compact(value));
    }

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(1000L, "1K")]
    [InlineData(1000000000000L, "1T")]
    [InlineData(2500000000000L, "2.5T")]
    public void Compact_Long_FormatsCorrectly(long value, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Compact(value));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(0.50, "0.5")]
    [InlineData(1.23, "1.23")]
    [InlineData(999, "999")]
    [InlineData(1000, "1K")]
    [InlineData(1500, "1.5K")]
    [InlineData(12345.67, "12.35K")]
    [InlineData(1000000, "1M")]
    [InlineData(1000000000, "1B")]
    [InlineData(1000000000000, "1T")]
    public void Compact_Decimal_FormatsCorrectly(decimal value, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Compact(value));
    }

    [Fact]
    public void Compact_NegativeInt_FormatsWithMinus()
    {
        Assert.Equal("-1.5K", NumberFormatter.Compact(-1500));
    }

    [Fact]
    public void Compact_NullableInt_ReturnsZero()
    {
        int? nullValue = null;
        Assert.Equal("0", NumberFormatter.Compact(nullValue));
    }

    [Fact]
    public void Compact_NullableInt_WithValue_Formats()
    {
        int? value = 2500;
        Assert.Equal("2.5K", NumberFormatter.Compact(value));
    }

    [Fact]
    public void Compact_Double_FormatsLikeDecimal()
    {
        Assert.Equal("1.5K", NumberFormatter.Compact(1500.0));
    }

    [Fact]
    public void Compact_SmallDecimal_ShowsDecimals()
    {
        Assert.Equal("42", NumberFormatter.Compact(42m));
        Assert.Equal("3.14", NumberFormatter.Compact(3.14m));
    }

    [Fact]
    public void Compact_WholeDecimal_NoTrailingDecimals()
    {
        Assert.Equal("100", NumberFormatter.Compact(100m));
        Assert.Equal("0", NumberFormatter.Compact(0m));
    }
}
