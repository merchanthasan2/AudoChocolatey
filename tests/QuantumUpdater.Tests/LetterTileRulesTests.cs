using QuantumUpdater.Views.Controls;
using Xunit;

namespace QuantumUpdater.Tests;

public class LetterTileRulesTests
{
    [Theory]
    [InlineData("7-Zip", '7')]
    [InlineData("  firefox", 'F')]
    [InlineData("", '?')]
    [InlineData(null, '?')]
    [InlineData("  --", '?')]
    public void Letter_is_first_letter_or_digit_in_upper_case(string? name, char expected)
    {
        Assert.Equal(expected, LetterTileRules.Letter(name));
    }

    [Fact]
    public void Mozilla_Firefox_has_fixed_colour_index()
    {
        // FNV-1a 32-bit of "MOZILLA FIREFOX" modulo 8 = 2. Computed once with a script on 16 Nov 2026.
        Assert.Equal(2, LetterTileRules.ColourIndex("Mozilla Firefox"));
    }

    [Fact]
    public void Same_name_always_gives_same_index()
    {
        Assert.Equal(LetterTileRules.ColourIndex("Notion"), LetterTileRules.ColourIndex("Notion"));
        Assert.Equal(LetterTileRules.ColourIndex("notion"), LetterTileRules.ColourIndex("NOTION"));
    }

    [Fact]
    public void Index_is_always_between_0_and_7()
    {
        Assert.InRange(LetterTileRules.ColourIndex(null), 0, 7);

        for (var i = 0; i < 1000; i++)
        {
            Assert.InRange(LetterTileRules.ColourIndex($"App {i}"), 0, 7);
        }
    }

    [Fact]
    public void One_thousand_names_use_all_eight_colours()
    {
        var used = new HashSet<int>();
        for (var i = 0; i < 1000; i++)
        {
            used.Add(LetterTileRules.ColourIndex($"App {i}"));
        }

        Assert.Equal(8, used.Count);
        Assert.Equal(8, LetterTileRules.PaletteHex.Count);
    }
}
