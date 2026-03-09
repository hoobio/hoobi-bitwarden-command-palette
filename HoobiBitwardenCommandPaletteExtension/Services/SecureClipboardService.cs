using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace HoobiBitwardenCommandPaletteExtension.Services;

internal static partial class SecureClipboardService
{
  private static Timer? _clearTimer;
  private static string? _lastCopiedText;
  private static readonly Lock _lock = new();
  private static int _clearDelaySeconds = 30;

  internal static bool AutoClearEnabled { get; set; } = true;

  internal static int ClearDelaySeconds
  {
    get => _clearDelaySeconds;
    set => _clearDelaySeconds = value > 0 ? value : 30;
  }

  internal static void CopySensitive(string text)
  {
    lock (_lock)
    {
      _lastCopiedText = text;
      SetClipboardExcludedFromHistory(text);
      if (AutoClearEnabled)
        ScheduleClear();
    }
  }

  internal static void CopyNonSensitive(string text)
  {
    lock (_lock)
    {
      _lastCopiedText = null;
      _clearTimer?.Dispose();
      _clearTimer = null;
    }
    Microsoft.CommandPalette.Extensions.Toolkit.ClipboardHelper.SetText(text);
  }

  private static void ScheduleClear()
  {
    _clearTimer?.Dispose();
    _clearTimer = new Timer(OnTimerElapsed, null, _clearDelaySeconds * 1000, Timeout.Infinite);
  }

  private static void OnTimerElapsed(object? state)
  {
    lock (_lock)
    {
      if (_lastCopiedText == null)
        return;

      var current = GetClipboardText();
      if (current != null && current == _lastCopiedText)
        ClearClipboard();

      _lastCopiedText = null;
      _clearTimer?.Dispose();
      _clearTimer = null;
    }
  }

  private static void SetClipboardExcludedFromHistory(string text)
  {
    if (!OpenClipboard(nint.Zero))
      return;

    try
    {
      EmptyClipboard();

      var bytes = System.Text.Encoding.Unicode.GetBytes(text + "\0");
      var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (nuint)bytes.Length);
      if (hGlobal == nint.Zero)
        return;

      var ptr = GlobalLock(hGlobal);
      if (ptr == nint.Zero)
      {
        GlobalFree(hGlobal);
        return;
      }

      Marshal.Copy(bytes, 0, ptr, bytes.Length);
      GlobalUnlock(hGlobal);

      if (SetClipboardData(CF_UNICODETEXT, hGlobal) == nint.Zero)
        GlobalFree(hGlobal);

      var excludeFormat = RegisterClipboardFormatW("ExcludeClipboardContentFromMonitorProcessing");
      if (excludeFormat != 0)
      {
        var hFlag = GlobalAlloc(GMEM_MOVEABLE, (nuint)sizeof(int));
        if (hFlag != nint.Zero)
        {
          var flagPtr = GlobalLock(hFlag);
          if (flagPtr != nint.Zero)
          {
            Marshal.WriteInt32(flagPtr, 1);
            GlobalUnlock(hFlag);
            if (SetClipboardData(excludeFormat, hFlag) == nint.Zero)
              GlobalFree(hFlag);
          }
          else
          {
            GlobalFree(hFlag);
          }
        }
      }
    }
    finally
    {
      CloseClipboard();
    }
  }

  private static string? GetClipboardText()
  {
    if (!OpenClipboard(nint.Zero))
      return null;

    try
    {
      var hData = GetClipboardData(CF_UNICODETEXT);
      if (hData == nint.Zero)
        return null;

      var ptr = GlobalLock(hData);
      if (ptr == nint.Zero)
        return null;

      try
      {
        return Marshal.PtrToStringUni(ptr);
      }
      finally
      {
        GlobalUnlock(hData);
      }
    }
    finally
    {
      CloseClipboard();
    }
  }

  private static void ClearClipboard()
  {
    if (!OpenClipboard(nint.Zero))
      return;

    try
    {
      EmptyClipboard();
    }
    finally
    {
      CloseClipboard();
    }
  }

  private const uint CF_UNICODETEXT = 13;
  private const uint GMEM_MOVEABLE = 0x0002;

  [LibraryImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static partial bool OpenClipboard(nint hWndNewOwner);

  [LibraryImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static partial bool CloseClipboard();

  [LibraryImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static partial bool EmptyClipboard();

  [LibraryImport("user32.dll")]
  private static partial nint SetClipboardData(uint uFormat, nint hMem);

  [LibraryImport("user32.dll")]
  private static partial nint GetClipboardData(uint uFormat);

  [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
  private static partial uint RegisterClipboardFormatW(string lpszFormat);

  [LibraryImport("kernel32.dll")]
  private static partial nint GlobalAlloc(uint uFlags, nuint dwBytes);

  [LibraryImport("kernel32.dll")]
  private static partial nint GlobalFree(nint hMem);

  [LibraryImport("kernel32.dll")]
  private static partial nint GlobalLock(nint hMem);

  [LibraryImport("kernel32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static partial bool GlobalUnlock(nint hMem);
}
