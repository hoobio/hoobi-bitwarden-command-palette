using HoobiBitwardenCommandPaletteExtension.Models;
using HoobiBitwardenCommandPaletteExtension.Services;

namespace HoobiBitwardenCommandPaletteExtension.Tests;

public class ContextAwarenessTests
{
  [Theory]
  [InlineData("chrome", true)]
  [InlineData("msedge", true)]
  [InlineData("firefox", true)]
  [InlineData("brave", true)]
  [InlineData("notepad", false)]
  [InlineData("explorer", false)]
  [InlineData("", false)]
  [InlineData(null, false)]
  public void IsBrowserProcess_DetectsCorrectly(string? processName, bool expected)
  {
    Assert.Equal(expected, ContextAwarenessService.IsBrowserProcess(processName));
  }

  [Fact]
  public void ContextScore_ReturnsZero_WhenNoMatch()
  {
    var context = new ForegroundContext
    {
      Windows = [new WindowContext { ProcessName = "notepad", WindowTitle = "Untitled" }]
    };
    var item = new BitwardenItem
    {
      Id = "1",
      Name = "GitHub",
      Type = BitwardenItemType.Login,
      Uris = [new ItemUri("https://github.com", UriMatchType.Default)]
    };

    Assert.Equal(0, ContextAwarenessService.ContextScore(context, item));
  }

  [Fact]
  public void ContextScore_ReturnsPositive_WhenProcessNameMatches()
  {
    var context = new ForegroundContext
    {
      Windows = [new WindowContext { ProcessName = "discord", WindowTitle = "Discord" }]
    };
    var item = new BitwardenItem
    {
      Id = "1",
      Name = "Discord",
      Type = BitwardenItemType.Login,
      Uris = [new ItemUri("https://discord.com", UriMatchType.Default)]
    };

    Assert.True(ContextAwarenessService.ContextScore(context, item) > 0);
  }

  [Fact]
  public void ContextScore_HigherForForegroundWindow()
  {
    var context = new ForegroundContext
    {
      Windows =
        [
            new WindowContext { ProcessName = "discord", WindowTitle = "Discord" },
                new WindowContext { ProcessName = "slack", WindowTitle = "Slack" }
        ]
    };

    var discord = new BitwardenItem { Id = "1", Name = "Discord", Type = BitwardenItemType.Login };
    var slack = new BitwardenItem { Id = "2", Name = "Slack", Type = BitwardenItemType.Login };

    var discordScore = ContextAwarenessService.ContextScore(context, discord);
    var slackScore = ContextAwarenessService.ContextScore(context, slack);

    Assert.True(discordScore > slackScore);
  }
}
