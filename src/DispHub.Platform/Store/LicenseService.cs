// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Logging;
using Windows.Services.Store;

namespace DispHub.Platform.Store;

public enum PurchaseResult
{
    Purchased,
    AlreadyOwned,
    Cancelled,
    Failed,
}

public interface ILicenseService
{
    bool IsPro { get; }

    /// <summary>True when Pro can be bought here (the Microsoft Store build). Other builds are already Pro.</summary>
    bool CanPurchase { get; }

    /// <summary>Raised on a background thread when <see cref="IsPro"/> may have changed.</summary>
    event EventHandler? Changed;

    Task InitializeAsync();

    /// <summary>Shows the Store purchase dialog, owned by <paramref name="windowHandle"/> (must be a visible window).</summary>
    Task<PurchaseResult> PurchaseProAsync(nint windowHandle);

    /// <summary>Shows the Store rating dialog. Returns true if the user submitted a rating.</summary>
    Task<bool> RequestReviewAsync(nint windowHandle);
}

/// <summary>Builds from source, sideloaded packages and development builds: everything unlocked.</summary>
public sealed class UnlockedLicenseService : ILicenseService
{
    public bool IsPro => true;
    public bool CanPurchase => false;

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task<PurchaseResult> PurchaseProAsync(nint windowHandle) => Task.FromResult(PurchaseResult.AlreadyOwned);

    public Task<bool> RequestReviewAsync(nint windowHandle) => Task.FromResult(false);
}

/// <summary>Developer switch (--fake-store=free|pro) to try the free tier and the upgrade flow without the Store.</summary>
public sealed class FakeLicenseService(bool startAsPro) : ILicenseService
{
    public bool IsPro { get; private set; } = startAsPro;
    public bool CanPurchase => true;

    public event EventHandler? Changed;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task<PurchaseResult> PurchaseProAsync(nint windowHandle)
    {
        if (IsPro)
            return Task.FromResult(PurchaseResult.AlreadyOwned);
        IsPro = true;
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(PurchaseResult.Purchased);
    }

    public Task<bool> RequestReviewAsync(nint windowHandle) => Task.FromResult(false);
}

/// <summary>
/// The Microsoft Store build. Pro is a durable add-on whose product ID ("in-app offer token") in Partner Center is
/// <see cref="ProOfferToken"/>, so no Store IDs need to be compiled in.
/// </summary>
public sealed class StoreLicenseService(ILogger<StoreLicenseService> logger) : ILicenseService
{
    public const string ProOfferToken = "pro";

    private StoreContext? _context;
    private volatile bool _isPro;

    public bool IsPro => _isPro;
    public bool CanPurchase => true;

    public event EventHandler? Changed;

    public async Task InitializeAsync()
    {
        try
        {
            _context = StoreContext.GetDefault();
            _context.OfflineLicensesChanged += async (_, _) => await RefreshAsync();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Store licence check failed");
        }
    }

    public async Task<PurchaseResult> PurchaseProAsync(nint windowHandle)
    {
        if (_context is null)
            return PurchaseResult.Failed;
        try
        {
            WinRT.Interop.InitializeWithWindow.Initialize(_context, windowHandle);
            StoreProductQueryResult products = await _context.GetAssociatedStoreProductsAsync(["Durable"]);
            StoreProduct? pro = products.Products.Values.FirstOrDefault(p => p.InAppOfferToken == ProOfferToken);
            if (pro is null)
            {
                logger.LogError("Pro add-on not found in the Store ({Error})", products.ExtendedError?.Message);
                return PurchaseResult.Failed;
            }

            StorePurchaseResult result = await pro.RequestPurchaseAsync();
            await RefreshAsync();
            return result.Status switch
            {
                StorePurchaseStatus.Succeeded => PurchaseResult.Purchased,
                StorePurchaseStatus.AlreadyPurchased => PurchaseResult.AlreadyOwned,
                StorePurchaseStatus.NotPurchased => PurchaseResult.Cancelled,
                _ => PurchaseResult.Failed,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pro purchase failed");
            return PurchaseResult.Failed;
        }
    }

    public async Task<bool> RequestReviewAsync(nint windowHandle)
    {
        if (_context is null)
            return false;
        try
        {
            WinRT.Interop.InitializeWithWindow.Initialize(_context, windowHandle);
            StoreRateAndReviewResult result = await _context.RequestRateAndReviewAppAsync();
            return result.Status == StoreRateAndReviewStatus.Succeeded;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rate and review request failed");
            return false;
        }
    }

    private async Task RefreshAsync()
    {
        if (_context is null)
            return;
        StoreAppLicense license = await _context.GetAppLicenseAsync();
        bool isPro = license.AddOnLicenses.Values.Any(l => l.IsActive && l.InAppOfferToken == ProOfferToken);
        if (isPro != _isPro)
        {
            _isPro = isPro;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
