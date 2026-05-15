// SPDX-License-Identifier: GPL-3.0-or-later
namespace DispHub.Services.Display;

public interface IVibranceService : IDisposable
{
    bool IsSupported { get; }
    int MinValue { get; }
    int MaxValue { get; }
    int DefaultValue { get; }
    bool ApplyVibrance(int value);
    void ResetVibrance();
}
