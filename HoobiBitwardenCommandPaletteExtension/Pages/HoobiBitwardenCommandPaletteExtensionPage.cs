using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using HoobiBitwardenCommandPaletteExtension.Helpers;
using HoobiBitwardenCommandPaletteExtension.Models;
using HoobiBitwardenCommandPaletteExtension.Services;

namespace HoobiBitwardenCommandPaletteExtension;

internal sealed partial class HoobiBitwardenCommandPaletteExtensionPage : DynamicListPage, IDisposable
{
    private readonly BitwardenCliService _service;
    private readonly BitwardenSettingsManager? _settings;
    private IListItem[] _currentItems = [];
    private bool _initialized;
    private string _currentSearchText = string.Empty;
    private string? _errorMessage;

    public HoobiBitwardenCommandPaletteExtensionPage(BitwardenCliService service, BitwardenSettingsManager? settings = null)
    {
        _service = service;
        _settings = settings;
        _service.CacheUpdated += OnCacheUpdated;
        _service.StatusChanged += OnStatusChanged;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Bitwarden";
        Name = "Open";
        PlaceholderText = "Search your vault...";
    }

    public override IListItem[] GetItems()
    {
        if (!_initialized)
        {
            _initialized = true;
            _ = Task.Run(InitializeAsync);
        }

        return _currentItems;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _currentSearchText = newSearch;

        if (_service.IsCacheLoaded)
        {
            _currentItems = BuildListItems(_service.SearchCached(newSearch));
            RaiseItemsChanged();
            _service.TriggerBackgroundRefreshIfStale();
        }
        else
        {
            _ = Task.Run(async () =>
            {
                IsLoading = true;
                await _service.RefreshCacheAsync();
            });
        }
    }

    private void OnCacheUpdated()
    {
        var results = _service.SearchCached(_currentSearchText);
        _currentItems = BuildListItems(results);
        RaiseItemsChanged();
        IsLoading = false;
    }

    private void OnStatusChanged()
    {
        _initialized = false;
        _currentItems = [];
        RaiseItemsChanged();
    }

    private async Task InitializeAsync()
    {
        IsLoading = true;

        var status = _service.LastStatus ?? await _service.GetVaultStatusAsync();
        switch (status)
        {
            case VaultStatus.CliNotFound:
                _currentItems = BuildCliNotFoundItems();
                break;
            case VaultStatus.Unauthenticated:
                _currentItems = BuildUnauthenticatedItems();
                break;
            case VaultStatus.Locked:
                _currentItems = BuildLockedItems();
                break;
            case VaultStatus.Unlocked:
                if (!_service.IsCacheLoaded)
                    await _service.RefreshCacheAsync();
                _currentItems = BuildListItems(_service.SearchCached(null));
                break;
        }

        RaiseItemsChanged();
        IsLoading = false;
    }

    private static IListItem[] BuildCliNotFoundItems() =>
    [
        new ListItem(new OpenUrlCommand("https://bitwarden.com/help/cli/#download-and-install"))
        {
            Title = "Bitwarden CLI not found",
            Subtitle = "Install the Bitwarden CLI (bw) and ensure it's in your PATH",
            Icon = new IconInfo("\uE783"),
        },
    ];

    private IListItem[] BuildUnauthenticatedItems() =>
    [
        new ListItem(new Pages.LoginPage(_service, _settings, OnLoginSubmitted))
        {
            Title = "Login to Bitwarden",
            Subtitle = _errorMessage ?? "Sign in with your email and master password",
            Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png"),
        },
        BuildSetServerItem(),
    ];

    private IListItem[] BuildLockedItems() =>
    [
        BuildUnlockItem(),
        BuildSetServerItem(),
        BuildLogoutItem(),
    ];

    private ListItem BuildUnlockItem() => new(new Pages.UnlockVaultPage(_service, _settings, OnUnlockSubmitted))
    {
        Title = "Vault is locked",
        Subtitle = _errorMessage ?? "Click to unlock your Bitwarden vault",
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png"),
    };

    private ListItem BuildSetServerItem() => new(new Pages.SetServerPage(_service))
    {
        Title = "Set Bitwarden Server",
        Subtitle = BitwardenCliService.ServerUrl ?? "https://vault.bitwarden.com",
        Icon = new IconInfo("\uE774"),
    };

    private ListItem BuildLogoutItem() => new(new Commands.LogoutCommand(_service))
    {
        Title = "Logout of Bitwarden",
        Subtitle = "Log out and clear session",
        Icon = new IconInfo("\uEA56"),
    };

    private ListItem BuildLockItem() => new(new Commands.LockCommand(_service))
    {
        Title = "Lock Bitwarden",
        Subtitle = "Lock the vault and clear cached items",
        Icon = new IconInfo("\uE72E"),
    };

    private IListItem[] BuildListItems(List<BitwardenItem> items)
    {
        var list = new List<IListItem>();

        if (items.Count == 0)
            list.Add(new ListItem(new NoOpCommand()) { Title = "No results found" });
        else
            list.AddRange(items.Select(BuildListItem));

        list.Add(BuildSetServerItem());
        list.Add(BuildLockItem());
        list.Add(BuildLogoutItem());
        return list.ToArray();
    }

    private IListItem BuildListItem(BitwardenItem item) => new ListItem(VaultItemHelper.GetDefaultCommand(item))
    {
        Title = item.Name,
        Subtitle = item.Subtitle,
        Icon = VaultItemHelper.GetIcon(item),
        MoreCommands = VaultItemHelper.BuildContextItems(item),
    };

    public void Dispose()
    {
        _service.CacheUpdated -= OnCacheUpdated;
        _service.StatusChanged -= OnStatusChanged;
    }

    private void OnUnlockSubmitted(string password)
    {
        _errorMessage = null;
        _currentItems = [];
        IsLoading = true;
        RaiseItemsChanged();

        _ = Task.Run(async () =>
        {
            var (success, error) = await _service.UnlockAsync(password);
            if (!success)
            {
                _errorMessage = error ?? "Unlock failed";
                _currentItems = BuildLockedItems();
                RaiseItemsChanged();
                IsLoading = false;
                return;
            }

            await _service.RefreshCacheAsync();
            _currentItems = BuildListItems(_service.SearchCached(null));
            RaiseItemsChanged();
            IsLoading = false;
        });
    }

    private void OnLoginSubmitted(string email, string password, string? twoFactorCode)
    {
        _errorMessage = null;
        _currentItems = [];
        IsLoading = true;
        RaiseItemsChanged();

        _ = Task.Run(async () =>
        {
            var (success, error, twoFactorRequired) = await _service.LoginAsync(email, password, twoFactorCode);
            if (!success)
            {
                if (twoFactorRequired && string.IsNullOrEmpty(twoFactorCode))
                    _errorMessage = "Two-factor authentication required - enter your 2FA code";
                else
                    _errorMessage = error ?? "Login failed";

                _currentItems = BuildUnauthenticatedItems();
                RaiseItemsChanged();
                IsLoading = false;
                return;
            }

            await _service.RefreshCacheAsync();
            _currentItems = BuildListItems(_service.SearchCached(null));
            RaiseItemsChanged();
            IsLoading = false;
        });
    }
}
