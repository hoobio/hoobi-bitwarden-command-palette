using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Text.Json.Nodes;
using HoobiBitwardenCommandPaletteExtension.Services;

namespace HoobiBitwardenCommandPaletteExtension.Pages;

internal record VerificationRequest(string Password, BitwardenCliService Service, Action InnerAction, string ActionLabel);

internal sealed partial class RepromptPage : ContentPage
{
  internal static int GracePeriodSeconds { get; set; } = 60;

  private static readonly string GraceFile = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "HoobiBitwardenCommandPalette", "grace.json");

  private static long _lastVerifiedTs;

  internal static event Action? GraceStarted;
  internal static event Action<VerificationRequest>? VerificationRequested;

  internal static bool IsWithinGracePeriod()
  {
    if (GracePeriodSeconds <= 0) return false;

    var ts = Interlocked.Read(ref _lastVerifiedTs);
    if (ts != 0 && Stopwatch.GetElapsedTime(ts).TotalSeconds < GracePeriodSeconds)
      return true;

    try
    {
      if (!File.Exists(GraceFile)) return false;
      var json = File.ReadAllText(GraceFile);
      if (JsonNode.Parse(json)?["verified"]?.GetValue<long>() is long utcTicks)
      {
        var elapsed = DateTime.UtcNow - new DateTime(utcTicks, DateTimeKind.Utc);
        if (elapsed.TotalSeconds < GracePeriodSeconds)
        {
          Interlocked.CompareExchange(ref _lastVerifiedTs,
            Stopwatch.GetTimestamp() - (long)(elapsed.TotalSeconds * Stopwatch.Frequency),
            0);
          return true;
        }
      }
    }
    catch { }

    return false;
  }

  internal static void RecordVerification()
  {
    Interlocked.Exchange(ref _lastVerifiedTs, Stopwatch.GetTimestamp());
    PersistGrace();
    GraceStarted?.Invoke();
  }

  internal static void ClearGracePeriod()
  {
    Interlocked.Exchange(ref _lastVerifiedTs, 0);
    try { File.Delete(GraceFile); } catch { }
  }

  private static void PersistGrace()
  {
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(GraceFile)!);
      File.WriteAllText(GraceFile, $"{{\"verified\":{DateTime.UtcNow.Ticks}}}");
    }
    catch { }
  }

  internal static void RaiseVerificationRequested(VerificationRequest request) =>
    VerificationRequested?.Invoke(request);

  private readonly RepromptForm _form;

  public RepromptPage(BitwardenCliService service, Action innerAction, string actionLabel)
  {
    Name = "Verify Password";
    Title = "Master Password Required";
    Icon = new IconInfo("\uE72E");
    _form = new RepromptForm(service, innerAction, actionLabel);
  }

  public override IContent[] GetContent() => [_form];
}

internal sealed partial class RepromptForm(BitwardenCliService service, Action innerAction, string actionLabel) : FormContent
{
  private bool _showError;

  internal void ShowError()
  {
    _showError = true;
    TemplateJson = BuildTemplate(showError: true);
  }

  internal void ResetError()
  {
    if (_showError)
    {
      _showError = false;
      TemplateJson = BuildTemplate(showError: false);
    }
  }

  private static string BuildTemplate(bool showError) =>
    """
    {
        "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
        "type": "AdaptiveCard",
        "version": "1.6",
        "body": [
            {
                "type": "TextBlock",
                "size": "medium",
                "weight": "bolder",
                "text": "Re-enter your master password",
                "horizontalAlignment": "center",
                "wrap": true,
                "style": "heading"
            },
            {
                "type": "TextBlock",
                "text": "This item requires master password verification before you can access it.",
                "wrap": true,
                "isSubtle": true,
                "size": "small"
            },
            {
                "type": "Input.Text",
                "label": "Master Password",
                "style": "Password",
                "id": "MasterPassword",
                "isRequired": true,
                "errorMessage": "Master password is required",
                "placeholder": "Enter your master password"
            },
            {
                "type": "ActionSet",
                "actions": [
                    {
                        "type": "Action.Submit",
                        "title": "Verify & Continue"
                    }
                ]
            }
    """ + (showError ? """
            ,{
                "type": "TextBlock",
                "text": "Incorrect master password. Please try again.",
                "color": "Attention",
                "wrap": true,
                "size": "small"
            }
    """ : "") + """
        ]
    }
    """;

  public override ICommandResult SubmitForm(string inputs, string data)
  {
    var formInput = JsonNode.Parse(inputs)?.AsObject();
    var password = formInput?["MasterPassword"]?.GetValue<string>();

    if (string.IsNullOrEmpty(password))
      return CommandResult.KeepOpen();

    RepromptPage.RaiseVerificationRequested(
      new VerificationRequest(password, service, innerAction, actionLabel));

    return CommandResult.GoBack();
  }
}
