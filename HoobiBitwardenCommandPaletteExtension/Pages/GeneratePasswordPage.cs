using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using HoobiBitwardenCommandPaletteExtension.Services;

namespace HoobiBitwardenCommandPaletteExtension.Pages;

internal sealed partial class GeneratePasswordPage : ContentPage
{
  private readonly BitwardenCliService _service;
  private readonly BitwardenSettingsManager _settings;
  private GeneratePasswordForm? _form;

  public GeneratePasswordPage(BitwardenCliService service, BitwardenSettingsManager settings)
  {
    Name = "Generate Password";
    Title = "Generate Password";
    Icon = new IconInfo("\uE8D7");
    _service = service;
    _settings = settings;
  }

  public override IContent[] GetContent()
  {
    _form ??= new GeneratePasswordForm(_service, _settings);
    return [_form];
  }
}

internal sealed partial class GeneratePasswordForm : FormContent
{
  private readonly BitwardenCliService _service;
  private string? _currentPassword;

  private readonly BitwardenSettingsManager _settings;

  public GeneratePasswordForm(BitwardenCliService service, BitwardenSettingsManager settings)
  {
    _service = service;
    _settings = settings;
    var length = ParseLength(settings.GeneratorLength.Value);
    var upper = settings.GeneratorUppercase.Value;
    var lower = settings.GeneratorLowercase.Value;
    var numbers = settings.GeneratorNumbers.Value;
    var special = settings.GeneratorSpecial.Value;
    _currentPassword = TryGenerate(length, upper, lower, numbers, special);
    TemplateJson = BuildTemplate(_currentPassword, length, upper, lower, numbers, special);
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

  private static string BuildTemplate(string? password, int length, bool upper, bool lower, bool numbers, bool special)
  {
    var masked = password != null ? new string('\u2022', password.Length) : "\u2022\u2022\u2022\u2022\u2022";
    var revealed = password != null ? JsonValue.Create(password).ToJsonString() : "\"\"";

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
                "text": "Generate Password",
                "horizontalAlignment": "center",
                "wrap": true,
                "style": "heading"
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
                        "title": "Copy",
                        "data": {"_submit": "copy"}
                    }
                ]
            }
        ]
    }
    """;
  }

  public override ICommandResult SubmitForm(string inputs, string data)
  {
    var formInput = JsonNode.Parse(inputs)?.AsObject();
    var submitType = formInput?["_submit"]?.GetValue<string>() ?? "copy";
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
      TemplateJson = BuildTemplate(_currentPassword, length, upper, lower, numbers, special);
      return CommandResult.KeepOpen();
    }

    // copy - use the previewed password, fallback to generating if somehow null
    if (string.IsNullOrEmpty(_currentPassword))
      _currentPassword = TryGenerate(length, upper, lower, numbers, special);

    if (string.IsNullOrEmpty(_currentPassword))
      return CommandResult.ShowToast("Failed to generate password");

    SecureClipboardService.CopySensitive(_currentPassword);
    return CommandResult.ShowToast("Password copied to clipboard");
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

