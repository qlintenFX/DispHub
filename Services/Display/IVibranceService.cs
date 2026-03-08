namespace DisplayHub.Services.Display;

public interface IVibranceService : IDisposable
{
    bool IsSupported { get; }
    int MinValue { get; }
    int MaxValue { get; }
    int DefaultValue { get; }
    bool ApplyVibrance(int value);
    void ResetVibrance();
}
