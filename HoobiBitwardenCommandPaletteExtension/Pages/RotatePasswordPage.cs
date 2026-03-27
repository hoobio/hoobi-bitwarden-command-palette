using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using HoobiBitwardenCommandPaletteExtension.Services;

namespace HoobiBitwardenCommandPaletteExtension.Pages;

internal sealed partial class RotatePasswordPage : ContentPage
{
  private readonly BitwardenCliService _service;
  private readonly BitwardenSettingsManager _settings;
  private readonly string _itemId;
  private readonly string _itemName;
  private RotatePasswordForm? _form;

  public RotatePasswordPage(BitwardenCliService service, BitwardenSettingsManager settings, string itemId, string itemName)
  {
    Name = "Rotate Password";
    Title = $"Rotate Password \u2014 {itemName}";
    Icon = new IconInfo("\uE72C");
    _service = service;
    _settings = settings;
    _itemId = itemId;
    _itemName = itemName;
  }

  public override IContent[] GetContent()
  {
    _form ??= new RotatePasswordForm(_service, _settings, _itemId, _itemName);
    return [_form];
  }
}

internal sealed partial class RotatePasswordForm : FormContent
{
  private readonly BitwardenCliService _service;
  private readonly string _itemId;
  private readonly string _itemName;
  private string? _currentPassword;
  private volatile bool _rotated;
  private int _processing;

  public RotatePasswordForm(BitwardenCliService service, BitwardenSettingsManager settings, string itemId, string itemName)
  {
    _service = service;
    _itemId = itemId;
    _itemName = itemName;
    var length = ParseLength(settings.GeneratorLength.Value);
    var upper = settings.GeneratorUppercase.Value;
    var lower = settings.GeneratorLowercase.Value;
    var numbers = settings.GeneratorNumbers.Value;
    var special = settings.GeneratorSpecial.Value;
    _currentPassword = TryGenerate(length, upper, lower, numbers, special);
    TemplateJson = BuildFormTemplate(_currentPassword, length, upper, lower, numbers, special);
  }

  private string? TryGenerate(int length, bool upper, bool lower, bool numbers, bool special)
  {
    try
    {
#pragma warning disable VSTHRD002 // SubmitForm and constructor are synchronous SDK callbacks
      return _service.GeneratePasswordAsync(length, upper, lower, numbers, special).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }
    catch { return null; }
  }

  private string BuildFormTemplate(string? password, int length, bool upper, bool lower, bool numbers, bool special)
  {
    var masked = password != null ? new string('\u2022', password.Length) : "\u2022\u2022\u2022\u2022\u2022";
    var revealed = password != null ? JsonValue.Create(password).ToJsonString() : "\"\"";
    var itemNameJson = JsonValue.Create($"New password for **{_itemName}**").ToJsonString();

    return $$"""
    {
        "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
        "type": "AdaptiveCard",
        "version": "1.6",
        "body": [
            {
                "type": "TextBlock",
                "size": "medium",
                "weight": "bolder",
                "text": "Rotate Password",
                "horizontalAlignment": "center",
                "wrap": true,
                "style": "heading"
            },
            {
                "type": "TextBlock",
                "text": {{itemNameJson}},
                "wrap": true,
                "isSubtle": true,
                "size": "small"
            },
            {
                "type": "Container",
                "style": "emphasis",
                "items": [
                    {
                        "type": "TextBlock",
                        "id": "passwordMasked",
                        "text": "{{masked}}",
                        "fontType": "Monospace",
                        "wrap": true,
                        "isVisible": true
                    },
                    {
                        "type": "TextBlock",
                        "id": "passwordVisible",
                        "text": {{revealed}},
                        "fontType": "Monospace",
                        "wrap": true,
                        "isVisible": false
                    }
                ]
            },
            {
                "type": "ActionSet",
                "actions": [
                    {
                        "type": "Action.ToggleVisibility",
                        "title": "Reveal / Hide",
                        "targetElements": ["passwordMasked", "passwordVisible"]
                    },
                    {
                        "type": "Action.Submit",
                        "title": "↻ Regenerate",
                        "data": {"_submit": "refresh"}
                    }
                ]
            },
            {
                "type": "Input.Number",
                "id": "Length",
                "label": "Password Length",
                "value": {{length}},
                "min": 8,
                "max": 64,
                "placeholder": "8–64",
                "isRequired": true,
                "errorMessage": "Length must be between 8 and 64"
            },
            {
                "type": "Input.Toggle",
                "id": "Uppercase",
                "title": "Uppercase (A-Z)",
                "valueOn": "true",
                "valueOff": "false",
                "value": "{{(upper ? "true" : "false")}}"
            },
            {
                "type": "Input.Toggle",
                "id": "Lowercase",
                "title": "Lowercase (a-z)",
                "valueOn": "true",
                "valueOff": "false",
                "value": "{{(lower ? "true" : "false")}}"
            },
            {
                "type": "Input.Toggle",
                "id": "Numbers",
                "title": "Numbers (0-9)",
                "valueOn": "true",
                "valueOff": "false",
                "value": "{{(numbers ? "true" : "false")}}"
            },
            {
                "type": "Input.Toggle",
                "id": "Special",
                "title": "Special characters (!@#$%)",
                "valueOn": "true",
                "valueOff": "false",
                "value": "{{(special ? "true" : "false")}}"
            },
            {
                "type": "ActionSet",
                "actions": [
                    {
                        "type": "Action.Submit",
                        "title": "Rotate & Copy",
                        "data": {"_submit": "rotate"}
                    }
                ]
            }
        ]
    }
    """;
  }

  private string BuildSuccessTemplate(string password)
  {
    var masked = new string('\u2022', password.Length);
    var revealed = JsonValue.Create(password).ToJsonString();
    var successTextJson = JsonValue.Create($"The vault has been updated and the new password for **{_itemName}** has been copied to your clipboard.").ToJsonString();

    return $$"""
    {
        "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
        "type": "AdaptiveCard",
        "version": "1.6",
        "body": [
            {
                "type": "TextBlock",
                "size": "medium",
                "weight": "bolder",
                "text": "Password Rotated",
                "horizontalAlignment": "center",
                "wrap": true,
                "style": "heading"
            },
            {
                "type": "TextBlock",
                "text": {{successTextJson}},
                "wrap": true,
                "color": "Good",
                "size": "small"
            },
            {
                "type": "Container",
                "style": "emphasis",
                "items": [
                    {
                        "type": "TextBlock",
                        "id": "passwordMasked",
                        "text": "{{masked}}",
                        "fontType": "Monospace",
                        "wrap": true,
                        "isVisible": true
                    },
                    {
                        "type": "TextBlock",
                        "id": "passwordVisible",
                        "text": {{revealed}},
                        "fontType": "Monospace",
                        "wrap": true,
                        "isVisible": false
                    }
                ]
            },
            {
                "type": "ActionSet",
                "actions": [
                    {
                        "type": "Action.ToggleVisibility",
                        "title": "Reveal / Hide",
                        "targetElements": ["passwordMasked", "passwordVisible"]
                    }
                ]
            }
        ]
    }
    """;
  }

  public override ICommandResult SubmitForm(string inputs, string data)
  {
    if (_rotated) return CommandResult.KeepOpen();

    var formInput = JsonNode.Parse(inputs)?.AsObject();
    var submitType = formInput?["_submit"]?.GetValue<string>() ?? "rotate";
    var length = ParseLength(formInput?["Length"]);
    var upper = formInput?["Uppercase"]?.GetValue<string>() == "true";
    var lower = formInput?["Lowercase"]?.GetValue<string>() == "true";
    var numbers = formInput?["Numbers"]?.GetValue<string>() == "true";
    var special = formInput?["Special"]?.GetValue<string>() == "true";

    if (!upper && !lower && !numbers && !special)
      return CommandResult.KeepOpen();

    if (submitType == "refresh")
    {
      _currentPassword = TryGenerate(length, upper, lower, numbers, special);
      TemplateJson = BuildFormTemplate(_currentPassword, length, upper, lower, numbers, special);
      return CommandResult.KeepOpen();
    }

    // rotate
    if (Interlocked.CompareExchange(ref _processing, 1, 0) != 0)
      return CommandResult.KeepOpen();

    try
    {
      var password = _currentPassword;
      if (string.IsNullOrEmpty(password))
      {
        password = TryGenerate(length, upper, lower, numbers, special);
        if (string.IsNullOrEmpty(password))
        {
          Volatile.Write(ref _processing, 0);
          return CommandResult.ShowToast("Failed to generate password");
        }
      }

#pragma warning disable VSTHRD002 // SubmitForm is a synchronous SDK callback
      var (success, error) = _service.EditItemPasswordAsync(_itemId, password).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

      if (!success)
      {
        Volatile.Write(ref _processing, 0);
        DebugLogService.Log("Rotate", $"Rotate failed for {_itemId}: {error}");
        return CommandResult.ShowToast($"Failed to update password: {error}");
      }

      SecureClipboardService.CopySensitive(password);
      _rotated = true;
      _currentPassword = password;
      TemplateJson = BuildSuccessTemplate(password);
      return CommandResult.KeepOpen();
    }
    catch (InvalidOperationException)
    {
      Volatile.Write(ref _processing, 0);
      return CommandResult.ShowToast("Session expired — please unlock your vault");
    }
    catch (Exception ex)
    {
      Volatile.Write(ref _processing, 0);
      DebugLogService.Log("Rotate", $"Rotate failed: {ex.Message}");
      return CommandResult.ShowToast("Failed to rotate password");
    }
  }

  private static int ParseLength(JsonNode? node)
  {
    if (node == null) return 20;
    return Math.Clamp(
      node.GetValueKind() == JsonValueKind.Number ? node.GetValue<int>()
        : int.TryParse(node.ToString(), out var l) ? l : 20,
      8, 64);
  }

  private static int ParseLength(string? value) =>
    Math.Clamp(int.TryParse(value, out var l) ? l : 20, 8, 64);
}
