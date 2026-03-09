using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using HoobiBitwardenCommandPaletteExtension.Services;

namespace HoobiBitwardenCommandPaletteExtension.Commands;

internal sealed partial class LockCommand : InvokableCommand
{
  private readonly BitwardenCliService _service;

  public LockCommand(BitwardenCliService service)
  {
    _service = service;
    Name = "Lock";
  }

  public override ICommandResult Invoke()
  {
    _ = Task.Run(() => _service.LockAsync());
    return CommandResult.ShowToast("Locking Bitwarden vault...");
  }
}
