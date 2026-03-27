using System;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using HoobiBitwardenCommandPaletteExtension.Services;

namespace HoobiBitwardenCommandPaletteExtension.Pages;

internal sealed partial class RotatePasswordPage : ContentPage
{
  private readonly RotatePasswordForm _form;

  public RotatePasswordPage(BitwardenCliService service, BitwardenSettingsManager settings, string itemId, string itemName)
  {
    Name = "Rotate Password";
    Title = $"Rotate Password — {itemName}";
    Icon = new IconInfo("\uE72C");
    _form = new RotatePasswordForm(service, settings, itemId, itemName);
  }

  public override IContent[] GetContent() => [_form];
}

internal sealed partial class RotatePasswordForm : FormContent
{
  private readonly BitwardenCliService _service;
  private readonly BitwardenSettingsManager _settings;
  private readonly string _itemId;
  private readonly string _itemName;

  public RotatePasswordForm(BitwardenCliService service, BitwardenSettingsManager settings, string itemId, string itemName)
  {
    _service = service;
    _settings = settings;
    _itemId = itemId;
    _itemName = itemName;
    TemplateJson = BuildTemplate();
  }

  private string BuildTemplate()
  {
    var length = _settings.GeneratorLength.Value ?? "20";
    var upper = _settings.GeneratorUppercase.Value;
    var lower = _settings.GeneratorLowercase.Value;
    var numbers = _settings.GeneratorNumbers.Value;
    var special = _settings.GeneratorSpecial.Value;

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
                "text": "Generate a new password for **{{_itemName}}**, save it to your vault, and copy it to clipboard.",
                "wrap": true,
                "isSubtle": true
            },
            {
                "type": "Input.ChoiceSet",
                "id": "Length",
                "label": "Password Length",
                "value": "{{length}}",
                "choices": [
                    { "title": "8", "value": "8" },
                    { "title": "12", "value": "12" },
                    { "title": "16", "value": "16" },
                    { "title": "20", "value": "20" },
                    { "title": "24", "value": "24" },
                    { "title": "32", "value": "32" },
                    { "title": "48", "value": "48" },
                    { "title": "64", "value": "64" }
                ]
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
                        "title": "Rotate & Copy"
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
    var length = int.TryParse(formInput?["Length"]?.GetValue<string>(), out var l) ? l : 20;
    var uppercase = formInput?["Uppercase"]?.GetValue<string>() == "true";
    var lowercase = formInput?["Lowercase"]?.GetValue<string>() == "true";
    var numbers = formInput?["Numbers"]?.GetValue<string>() == "true";
    var special = formInput?["Special"]?.GetValue<string>() == "true";

    if (!uppercase && !lowercase && !numbers && !special)
      return CommandResult.KeepOpen();

    try
    {
#pragma warning disable VSTHRD002 // SubmitForm is a synchronous SDK callback
      var password = _service.GeneratePasswordAsync(length, uppercase, lowercase, numbers, special)
        .GetAwaiter().GetResult();

      if (string.IsNullOrEmpty(password))
        return CommandResult.ShowToast("Failed to generate password");

      var (success, error) = _service.EditItemPasswordAsync(_itemId, password)
        .GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

      if (!success)
      {
        DebugLogService.Log("Rotate", $"Rotate failed for {_itemId}: {error}");
        return CommandResult.ShowToast($"Failed to update password: {error}");
      }

      SecureClipboardService.CopySensitive(password);
      return CommandResult.ShowToast($"Password rotated and copied to clipboard");
    }
    catch (InvalidOperationException)
    {
      return CommandResult.ShowToast("Session expired — please unlock your vault");
    }
    catch (Exception ex)
    {
      DebugLogService.Log("Rotate", $"Rotate failed: {ex.Message}");
      return CommandResult.ShowToast("Failed to rotate password");
    }
  }
}
