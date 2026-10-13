using System.Runtime.InteropServices;
using Windows.Devices.Geolocation;

namespace DispHub.Platform;

/// <summary>Asks Windows where the PC is, for sunrise and sunset. Only on request; the result isn't sent anywhere.</summary>
public static class LocationService
{
    public enum Failure
    {
        None,
        Denied,
        Unavailable,
    }

    public static async Task<(double Latitude, double Longitude, Failure Failure)> TryGetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            GeolocationAccessStatus access = await Geolocator.RequestAccessAsync().AsTask(cancellationToken);
            if (access != GeolocationAccessStatus.Allowed)
                return (0, 0, Failure.Denied);
            var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
            Geoposition position = await locator.GetGeopositionAsync(TimeSpan.FromHours(1), TimeSpan.FromSeconds(15)).AsTask(cancellationToken);
            BasicGeoposition point = position.Coordinate.Point.Position;
            return (point.Latitude, point.Longitude, Failure.None);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or COMException or OperationCanceledException or InvalidOperationException)
        {
            return (0, 0, ex is UnauthorizedAccessException ? Failure.Denied : Failure.Unavailable);
        }
    }
}
