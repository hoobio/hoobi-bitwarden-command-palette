using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Text.Json.Nodes;
using HoobiBitwardenCommandPaletteExtension.Services;

namespace HoobiBitwardenCommandPaletteExtension.Pages;

internal sealed partial class RepromptPage : ContentPage
{
  internal static int GracePeriodSeconds { get; set; } = 60;
  private static DateTime _lastVerified = DateTime.MinValue;

  internal static event Action? GraceStarted;

  internal static bool IsWithinGracePeriod() =>
    GracePeriodSeconds > 0 && (DateTime.UtcNow - _lastVerified).TotalSeconds < GracePeriodSeconds;

  internal static void RecordVerification()
  {
    _lastVerified = DateTime.UtcNow;
    GraceStarted?.Invoke();
  }

  internal static void ClearGracePeriod() => _lastVerified = DateTime.MinValue;

  private readonly RepromptForm _form;

  public RepromptPage(BitwardenCliService service, Action innerAction, string actionLabel, ICommandResult? successResult = null)
  {
    Name = "Verify Password";
    Title = "Master Password Required";
    Icon = new IconInfo("\uE72E");
    _form = new RepromptForm(service, innerAction, actionLabel, this, successResult);
  }

  public override IContent[] GetContent() => [_form];
}

internal sealed partial class RepromptForm : FormContent
{
  private readonly BitwardenCliService _service;
  private readonly Action _innerAction;
  private readonly string _actionLabel;
  private readonly RepromptPage _page;
  private readonly ICommandResult _successResult;

  private enum FormState { Initial, Verifying, Error }
  private FormState _state;

  public RepromptForm(BitwardenCliService service, Action innerAction, string actionLabel, RepromptPage page, ICommandResult? successResult = null)
  {
    _service = service;
    _innerAction = innerAction;
    _actionLabel = actionLabel;
    _page = page;
    _successResult = successResult ?? CommandResult.ShowToast($"Copied {actionLabel} to clipboard");
    _state = FormState.Initial;
    TemplateJson = BuildTemplate();
  }

  private string BuildTemplate() => _state switch
  {
    FormState.Verifying => BuildVerifyingTemplate(),
    FormState.Error => BuildErrorTemplate(),
    _ => BuildInitialTemplate(),
  };

  private static string BuildVerifyingTemplate() => """
    {
        "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
        "type": "AdaptiveCard",
        "version": "1.6",
        "body": [
            {
                "type": "TextBlock",
                "size": "medium",
                "weight": "bolder",
                "text": "Verifying master password...",
                "horizontalAlignment": "center",
                "wrap": true,
                "style": "heading"
            },
            {
                "type": "TextBlock",
                "text": "Running: bw unlock",
                "wrap": true,
                "isSubtle": true,
                "size": "small"
            }
        ]
    }
    """;

  private static string BuildInitialTemplate() => """
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
        ]
    }
    """;

  private static string BuildErrorTemplate() => """
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
            },
            {
                "type": "TextBlock",
                "text": "Incorrect master password. Please try again.",
                "color": "Attention",
                "wrap": true,
                "size": "small"
            }
        ]
    }
    """;

  public override ICommandResult SubmitForm(string inputs, string data)
  {
    var formInput = JsonNode.Parse(inputs)?.AsObject();
    var password = formInput?["MasterPassword"]?.GetValue<string>();

    if (string.IsNullOrEmpty(password))
      return CommandResult.KeepOpen();

    _state = FormState.Verifying;
    TemplateJson = BuildTemplate();
    _page.IsLoading = true;

    // SubmitForm is synchronous by SDK design (IFormContent.SubmitForm returns ICommandResult).
    // bw unlock is a fast local crypto operation with no SynchronizationContext to deadlock on.
#pragma warning disable VSTHRD002
    var verified = _service.VerifyMasterPasswordAsync(password).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    _page.IsLoading = false;

    if (!verified)
    {
      _state = FormState.Error;
      TemplateJson = BuildTemplate();
      return CommandResult.KeepOpen();
    }

    RepromptPage.RecordVerification();
    _innerAction();
    return _successResult;
  }
}
