using System;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using HoobiBitwardenCommandPaletteExtension.Services;

namespace HoobiBitwardenCommandPaletteExtension.Pages;

internal sealed partial class GeneratePasswordPage : ContentPage
{
  private readonly GeneratePasswordForm _form;

  public GeneratePasswordPage(BitwardenCliService service, BitwardenSettingsManager settings)
  {
    Name = "Generate Password";
    Title = "Generate Password";
    Icon = new IconInfo("\uE8D7");
    _form = new GeneratePasswordForm(service, settings);
  }

  public override IContent[] GetContent() => [_form];
}

internal sealed partial class GeneratePasswordForm : FormContent
{
  private readonly BitwardenCliService _service;
  private readonly BitwardenSettingsManager _settings;

  public GeneratePasswordForm(BitwardenCliService service, BitwardenSettingsManager settings)
  {
    _service = service;
    _settings = settings;
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
                "text": "Generate Password",
                "horizontalAlignment": "center",
                "wrap": true,
                "style": "heading"
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
                        "title": "Generate & Copy"
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
#pragma warning restore VSTHRD002

      if (string.IsNullOrEmpty(password))
        return CommandResult.ShowToast("Failed to generate password");

      SecureClipboardService.CopySensitive(password);
      return CommandResult.ShowToast("Password copied to clipboard");
    }
    catch (Exception ex)
    {
      DebugLogService.Log("Generate", $"Generate failed: {ex.Message}");
      return CommandResult.ShowToast("Failed to generate password");
    }
  }
}
