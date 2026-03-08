namespace DisplayHub.Services.Display;

public sealed class NullVibranceService : IVibranceService
{
    public static NullVibranceService Instance { get; } = new NullVibranceService();

    private NullVibranceService() { }

    public bool IsSupported => false;
    public int MinValue => 0;
    public int MaxValue => 100;
    public int DefaultValue => 50;

    public bool ApplyVibrance(int value) => true;
    public void ResetVibrance() { }
    public void Dispose() { }
}
