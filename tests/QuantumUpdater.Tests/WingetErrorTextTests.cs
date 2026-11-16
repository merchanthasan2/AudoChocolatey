using QuantumUpdater.Services.Winget;
using Xunit;

namespace QuantumUpdater.Tests;

public class WingetErrorTextTests
{
    // Every code that has a fixed text (0 is success; the rest come from the table).
    public static TheoryData<int, bool, bool> FlagRows => new()
    {
        { 0, true, false },
        { unchecked((int)0x8A15002B), false, false },
        { unchecked((int)0x8A150011), false, false },
        { unchecked((int)0x8A150010), false, false },
        { unchecked((int)0x8A150101), false, false },
        { unchecked((int)0x8A150111), false, false },
        { unchecked((int)0x8A150103), false, false },
        { unchecked((int)0x8A150102), false, false },
        { unchecked((int)0x8A150105), false, false },
        { unchecked((int)0x8A150106), false, false },
        { unchecked((int)0x8A150107), false, false },
        { unchecked((int)0x8A150008), false, false },
        { unchecked((int)0x8A150109), true, true },
        { unchecked((int)0x8A15010A), false, false },
        { unchecked((int)0x8A15010B), true, true },
        { unchecked((int)0x8A15010C), false, false },
        { unchecked((int)0x8A15010D), true, false },
        { unchecked((int)0x8A15010E), false, false },
        { unchecked((int)0x8A15010F), false, false },
        { unchecked((int)0x8A150110), false, false },
        { unchecked((int)0x8A150104), false, false },
        { unchecked((int)0x8A150108), false, false },
        { unchecked((int)0x8A150068), false, false },
        { unchecked((int)0x8A150112), false, false },
        { unchecked((int)0x8A150113), false, false },
        { unchecked((int)0x8A150114), false, false },
        { unchecked((int)0x8A150115), false, false },
        { 1602, false, false },
        { 1603, false, false },
        { 1618, false, false },
        { 1641, true, true },
        { 3010, true, true },
    };

    // The same codes, without flags. Used for the word check.
    private static readonly int[] MappedCodes =
    {
        unchecked((int)0x8A15002B),
        unchecked((int)0x8A150011),
        unchecked((int)0x8A150010),
        unchecked((int)0x8A150101),
        unchecked((int)0x8A150111),
        unchecked((int)0x8A150103),
        unchecked((int)0x8A150102),
        unchecked((int)0x8A150105),
        unchecked((int)0x8A150106),
        unchecked((int)0x8A150107),
        unchecked((int)0x8A150008),
        unchecked((int)0x8A150109),
        unchecked((int)0x8A15010A),
        unchecked((int)0x8A15010B),
        unchecked((int)0x8A15010C),
        unchecked((int)0x8A15010D),
        unchecked((int)0x8A15010E),
        unchecked((int)0x8A15010F),
        unchecked((int)0x8A150110),
        unchecked((int)0x8A150104),
        unchecked((int)0x8A150108),
        unchecked((int)0x8A150068),
        unchecked((int)0x8A150112),
        unchecked((int)0x8A150113),
        unchecked((int)0x8A150114),
        unchecked((int)0x8A150115),
        1602,
        1603,
        1618,
        1641,
        3010,
    };

    [Theory]
    [MemberData(nameof(FlagRows))]
    public void MappedCodeHasReasonAndFlags(int code, bool isSuccess, bool restartNeeded)
    {
        ErrorText result = WingetErrorText.For(code);

        Assert.False(string.IsNullOrEmpty(result.Reason));
        Assert.Equal(isSuccess, result.IsSuccess);
        Assert.Equal(restartNeeded, result.RestartNeeded);
    }

    [Fact]
    public void HashMismatchGivesHashMismatchText()
    {
        ErrorText result = WingetErrorText.For(unchecked((int)0x8A150011));

        Assert.Equal("The download did not match the file the app maker published.", result.Reason);
        Assert.Equal("Try again later. The app maker may be updating the file.", result.WhatToDo);
    }

    [Fact]
    public void UnknownHexCodeGivesStoppedTextWithHex()
    {
        ErrorText result = WingetErrorText.For(unchecked((int)0x8A15FFFF));

        Assert.Equal("The update stopped (code 0x8A15FFFF).", result.Reason);
        Assert.False(result.IsSuccess);
        Assert.False(result.RestartNeeded);
    }

    [Fact]
    public void UnknownDecimalCodeGivesStoppedTextWithDecimal()
    {
        ErrorText result = WingetErrorText.For(42);

        Assert.Equal("The update stopped (code 42).", result.Reason);
        Assert.False(result.IsSuccess);
        Assert.False(result.RestartNeeded);
    }

    [Fact]
    public void MappedCodesAvoidForbiddenWords()
    {
        foreach (int code in MappedCodes)
        {
            ErrorText result = WingetErrorText.For(code);

            foreach (string text in new[] { result.Reason, result.WhatToDo })
            {
                Assert.DoesNotContain("winget", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("HRESULT", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("MSI", text, StringComparison.Ordinal);
            }
        }
    }
}
