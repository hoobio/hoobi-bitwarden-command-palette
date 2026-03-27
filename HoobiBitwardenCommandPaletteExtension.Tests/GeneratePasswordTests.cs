using HoobiBitwardenCommandPaletteExtension.Pages;
using HoobiBitwardenCommandPaletteExtension.Services;

namespace HoobiBitwardenCommandPaletteExtension.Tests;

[Collection("SessionStore")]
public class GeneratePasswordTests
{
  private static (BitwardenCliService Service, FakeProcessFactory Factory) CreateService()
  {
    var factory = new FakeProcessFactory();
    var svc = new BitwardenCliService(processFactory: factory.Create);
    svc.SetSession("test-session");
    return (svc, factory);
  }

  private static BitwardenSettingsManager CreateSettings() => new();

  [Fact]
  public async Task GeneratePasswordAsync_ReturnsGeneratedPassword()
  {
    var (svc, factory) = CreateService();
    factory.Enqueue(new FakeCliProcess(stdout: "X7kQ9mP!vR2bZ#nL\n", exitCode: 0));

    var result = await svc.GeneratePasswordAsync(16, true, true, true, true);

    Assert.Equal("X7kQ9mP!vR2bZ#nL", result);
  }

  [Fact]
  public async Task GeneratePasswordAsync_DefaultArgs_IncludesAllCharSets()
  {
    var (svc, factory) = CreateService();
    factory.Enqueue(new FakeCliProcess(stdout: "password123\n", exitCode: 0));

    await svc.GeneratePasswordAsync();

    Assert.Contains("--length 20", factory.LastArgs, StringComparison.Ordinal);
    Assert.Contains("--uppercase", factory.LastArgs, StringComparison.Ordinal);
    Assert.Contains("--lowercase", factory.LastArgs, StringComparison.Ordinal);
    Assert.Contains("--number", factory.LastArgs, StringComparison.Ordinal);
    Assert.Contains("--special", factory.LastArgs, StringComparison.Ordinal);
  }

  [Fact]
  public async Task GeneratePasswordAsync_CustomArgs_OmitsDisabledCharSets()
  {
    var (svc, factory) = CreateService();
    factory.Enqueue(new FakeCliProcess(stdout: "abc\n", exitCode: 0));

    await svc.GeneratePasswordAsync(12, uppercase: false, lowercase: true, numbers: false, special: false);

    Assert.Contains("--length 12", factory.LastArgs, StringComparison.Ordinal);
    Assert.DoesNotContain("--uppercase", factory.LastArgs, StringComparison.Ordinal);
    Assert.Contains("--lowercase", factory.LastArgs, StringComparison.Ordinal);
    Assert.DoesNotContain("--number", factory.LastArgs, StringComparison.Ordinal);
    Assert.DoesNotContain("--special", factory.LastArgs, StringComparison.Ordinal);
  }

  [Fact]
  public async Task GeneratePasswordAsync_TrimsWhitespace()
  {
    var (svc, factory) = CreateService();
    factory.Enqueue(new FakeCliProcess(stdout: "  abc123!  \n", exitCode: 0));

    var result = await svc.GeneratePasswordAsync(8);

    Assert.Equal("abc123!", result);
  }

  [Fact]
  public async Task EditItemPasswordAsync_Success_ReturnsTrue()
  {
    var (svc, factory) = CreateService();
    var itemJson = """{"id":"item-1","type":1,"login":{"username":"user","password":"old"}}""";
    factory.Enqueue(new FakeCliProcess(stdout: itemJson + "\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: """{"id":"item-1"}""" + "\n", exitCode: 0));
    // Background sync: sync + list folders + list items
    factory.Enqueue(new FakeCliProcess(stdout: "Syncing complete.\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: "[]\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: "[]\n", exitCode: 0));

    var (success, error) = await svc.EditItemPasswordAsync("item-1", "newpass");

    Assert.True(success);
    Assert.Null(error);
  }

  [Fact]
  public async Task EditItemPasswordAsync_NonLoginItem_ReturnsError()
  {
    var (svc, factory) = CreateService();
    var itemJson = """{"id":"item-1","type":2,"notes":"secret note"}""";
    factory.Enqueue(new FakeCliProcess(stdout: itemJson + "\n", exitCode: 0));

    var (success, error) = await svc.EditItemPasswordAsync("item-1", "newpass");

    Assert.False(success);
    Assert.Equal("Item is not a login type", error);
  }

  [Fact]
  public async Task EditItemPasswordAsync_EncodesItemAsBase64()
  {
    var (svc, factory) = CreateService();
    var itemJson = """{"id":"item-1","type":1,"login":{"username":"user","password":"old"}}""";
    factory.Enqueue(new FakeCliProcess(stdout: itemJson + "\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: """{"id":"item-1"}""" + "\n", exitCode: 0));
    // Background sync: sync + list folders + list items
    factory.Enqueue(new FakeCliProcess(stdout: "Syncing complete.\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: "[]\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: "[]\n", exitCode: 0));

    await svc.EditItemPasswordAsync("item-1", "newpass");

    var editArgs = factory.AllArgs[1];
    Assert.StartsWith("edit item item-1 ", editArgs, StringComparison.Ordinal);
    var base64Part = editArgs.Split(' ').Last();
    var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Part));
    Assert.Contains("\"newpass\"", decoded, StringComparison.Ordinal);
  }

  [Fact]
  public async Task EditItemPasswordAsync_SessionExpired_Throws()
  {
    var (svc, factory) = CreateService();
    factory.Enqueue(new FakeCliProcess(stdout: "", stderr: "vault is locked\n", exitCode: 1));

    await Assert.ThrowsAsync<InvalidOperationException>(
      () => svc.EditItemPasswordAsync("item-1", "newpass"));
  }

  [Fact]
  public void GenerateForm_ShowsPreviewOnOpen()
  {
    var (svc, factory) = CreateService();
    factory.Enqueue(new FakeCliProcess(stdout: "PreviewPw!23\n", exitCode: 0));

    var form = new GeneratePasswordForm(svc, CreateSettings());

    Assert.Contains("PreviewPw!23", form.TemplateJson, StringComparison.Ordinal);
  }

  [Fact]
  public void GenerateForm_PreviewIsMaskedByDefault()
  {
    var (svc, factory) = CreateService();
    factory.Enqueue(new FakeCliProcess(stdout: "PreviewPw!23\n", exitCode: 0));

    var form = new GeneratePasswordForm(svc, CreateSettings());

    // Masked block is visible, revealed block is hidden
    Assert.Contains("isVisible\": true", form.TemplateJson, StringComparison.Ordinal);
    Assert.Contains("isVisible\": false", form.TemplateJson, StringComparison.Ordinal);
    Assert.Contains("\u2022\u2022\u2022\u2022", form.TemplateJson, StringComparison.Ordinal);
  }

  [Fact]
  public void GenerateForm_Refresh_UpdatesPreview()
  {
    var (svc, factory) = CreateService();
    factory.Enqueue(new FakeCliProcess(stdout: "Initial0!pass\n", exitCode: 0));  // constructor
    factory.Enqueue(new FakeCliProcess(stdout: "Updated1!pass\n", exitCode: 0));  // refresh

    var form = new GeneratePasswordForm(svc, CreateSettings());
    var inputs = """{"_submit":"refresh","Length":20,"Uppercase":"true","Lowercase":"true","Numbers":"true","Special":"true"}""";

    form.SubmitForm(inputs, "");

    Assert.Contains("Updated1!pass", form.TemplateJson, StringComparison.Ordinal);
    Assert.DoesNotContain("Initial0!pass", form.TemplateJson, StringComparison.Ordinal);
  }

  [Fact]
  public void RotateForm_ShowsPreviewOnOpen()
  {
    var (svc, factory) = CreateService();
    factory.Enqueue(new FakeCliProcess(stdout: "MySecureP@ss\n", exitCode: 0));

    var form = new RotatePasswordForm(svc, CreateSettings(), "i1", "Test Item");

    Assert.Contains("MySecureP@ss", form.TemplateJson, StringComparison.Ordinal);
  }

  [Fact]
  public void RotateForm_SubmitRotate_ShowsSuccessState()
  {
    var (svc, factory) = CreateService();
    factory.Enqueue(new FakeCliProcess(stdout: "MySecureP@ss\n", exitCode: 0));  // constructor
    factory.Enqueue(new FakeCliProcess(stdout: """{"id":"i1","type":1,"login":{"username":"u","password":"old"}}""" + "\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: """{"id":"i1"}""" + "\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: "Syncing complete.\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: "[]\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: "[]\n", exitCode: 0));

    var form = new RotatePasswordForm(svc, CreateSettings(), "i1", "Test Item");
    var inputs = """{"_submit":"rotate","Length":20,"Uppercase":"true","Lowercase":"true","Numbers":"true","Special":"true"}""";

    form.SubmitForm(inputs, "");

    Assert.Contains("Password Rotated", form.TemplateJson, StringComparison.Ordinal);
    Assert.Contains("MySecureP@ss", form.TemplateJson, StringComparison.Ordinal);
  }

  [Fact]
  public void RotateForm_SubmitRotate_GuardsAgainstDoubleSubmit()
  {
    var (svc, factory) = CreateService();
    factory.Enqueue(new FakeCliProcess(stdout: "newpass1\n", exitCode: 0));  // constructor
    factory.Enqueue(new FakeCliProcess(stdout: """{"id":"i1","type":1,"login":{"username":"u","password":"old"}}""" + "\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: """{"id":"i1"}""" + "\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: "Syncing complete.\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: "[]\n", exitCode: 0));
    factory.Enqueue(new FakeCliProcess(stdout: "[]\n", exitCode: 0));

    var form = new RotatePasswordForm(svc, CreateSettings(), "i1", "Test Item");
    var inputs = """{"_submit":"rotate","Length":20,"Uppercase":"true","Lowercase":"true","Numbers":"true","Special":"true"}""";

    form.SubmitForm(inputs, "");
    form.SubmitForm(inputs, "");  // second call should be a no-op

    Assert.Single(factory.AllArgs, a => a.StartsWith("edit item", StringComparison.Ordinal));
  }
}
