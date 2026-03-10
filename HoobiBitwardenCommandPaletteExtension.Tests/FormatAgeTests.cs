namespace HoobiBitwardenCommandPaletteExtension.Tests;

public class FormatAgeTests
{
  [Theory]
  [InlineData(0, "just now")]
  [InlineData(3, "just now")]
  [InlineData(30, "30 seconds ago")]
  [InlineData(59, "59 seconds ago")]
  [InlineData(60, "1 minute ago")]
  [InlineData(119, "1 minute ago")]
  [InlineData(120, "2 minutes ago")]
  [InlineData(3599, "59 minutes ago")]
  [InlineData(3600, "1 hour ago")]
  [InlineData(7199, "1 hour ago")]
  [InlineData(7200, "2 hours ago")]
  [InlineData(86400, "24 hours ago")]
  public void FormatAge_ReturnsExpected(int seconds, string expected)
  {
    var result = HoobiBitwardenCommandPaletteExtensionPage.FormatAge(TimeSpan.FromSeconds(seconds));
    Assert.Equal(expected, result);
  }
}
