using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using HoobiBitwardenCommandPaletteExtension.Services;

namespace HoobiBitwardenCommandPaletteExtension.Commands;

internal sealed partial class LogoutCommand : InvokableCommand
{
  private readonly BitwardenCliService _service;

  public LogoutCommand(BitwardenCliService service)
  {
    _service = service;
    Name = "Logout";
  }

  public override ICommandResult Invoke()
  {
    Task.Run(() => _service.LogoutAsync()).GetAwaiter().GetResult();
    return CommandResult.Dismiss();
  }
}
