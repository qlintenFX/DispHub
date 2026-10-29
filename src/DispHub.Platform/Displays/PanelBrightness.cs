// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Variant;
using Windows.Win32.System.Wmi;

namespace DispHub.Platform.Displays;

/// <summary>
/// A laptop or tablet screen's own brightness, the one its brightness keys and Windows' slider change, through WMI's
/// WmiMonitorBrightness classes, as Twinkle Tray and Monitorian do it. Each call takes a few milliseconds, so like
/// DDC/CI it runs on the brightness thread.
/// </summary>
internal sealed unsafe class PanelBrightness : IHardwareBrightness
{
    private const uint AuthnWinNT = 10;
    private const uint AuthzNone = 0;
    private const int WaitForever = -1; // WBEM_INFINITE

    public HardwareLevel? ReadLevel(MonitorInfo monitor) =>
        WithInstance(monitor, "WmiMonitorBrightness", (_, instance) =>
        {
            VARIANT value = default;
            try
            {
                instance->Get("CurrentBrightness", 0, ref value);
                return value.Anonymous.Anonymous.vt == VARENUM.VT_UI1
                    ? new HardwareLevel(value.Anonymous.Anonymous.Anonymous.bVal, 100)
                    : (HardwareLevel?)null;
            }
            finally
            {
                PInvoke.VariantClear(ref value);
            }
        }, null);

    public bool WriteLevel(MonitorInfo monitor, int percent) =>
        WithInstance(monitor, "WmiMonitorBrightnessMethods", (services, instance) =>
        {
            string? path = Text(instance, "__PATH");
            if (path is null)
                return false;
            IWbemClassObject* methods = null, inputs = null, call = null;
            try
            {
                using (var name = new Bstr("WmiMonitorBrightnessMethods"))
                    services->GetObject(name, 0, null, &methods, null);
                methods->GetMethod("WmiSetBrightness", 0, &inputs, null);
                inputs->SpawnInstance(0, &call);
                Put(call, "Timeout", Number(VARENUM.VT_I4, 0));
                Put(call, "Brightness", Number(VARENUM.VT_UI1, Math.Clamp(percent, 0, 100)));
                using var target = new Bstr(path);
                using var method = new Bstr("WmiSetBrightness");
                services->ExecMethod(target, method, 0, null, call, null, null);
                return true;
            }
            finally
            {
                Release(call);
                Release(inputs);
                Release(methods);
            }
        }, false);

    /// <summary>
    /// The start of a screen's WMI instance name, from its monitor device path:
    /// \\?\DISPLAY#EDO4245#4&amp;6cbc18c&amp;1&amp;UID8388688#{...} is DISPLAY\EDO4245\4&amp;6cbc18c&amp;1&amp;UID8388688,
    /// and WMI adds "_0".
    /// </summary>
    internal static string? InstancePrefix(string monitorKey)
    {
        string path = monitorKey.StartsWith(@"\\?\", StringComparison.Ordinal) ? monitorKey[4..] : monitorKey;
        int classGuid = path.IndexOf("#{", StringComparison.Ordinal);
        return classGuid <= 0 ? null : path[..classGuid].Replace('#', '\\');
    }

    private delegate T InstanceAction<T>(IWbemServices* services, IWbemClassObject* instance);

    /// <summary>Runs <paramref name="action"/> on the active instance of a WMI class that belongs to this screen.</summary>
    private static T WithInstance<T>(MonitorInfo monitor, string className, InstanceAction<T> action, T fallback)
    {
        if (!monitor.IsInternal || InstancePrefix(monitor.Key) is not { } prefix)
            return fallback;
        _ = PInvoke.CoInitializeEx(null, COINIT.COINIT_MULTITHREADED);
        IWbemLocator* locator = null;
        IWbemServices* services = null;
        IEnumWbemClassObject* found = null;
        try
        {
            if (WbemLocator.CreateInstance(out locator).Failed)
                return fallback;
            using (var space = new Bstr(@"ROOT\WMI"))
                locator->ConnectServer(space, default, default, default, 0, default, null, &services);
            PInvoke.CoSetProxyBlanket(
                (IUnknown*)services, AuthnWinNT, AuthzNone, default, RPC_C_AUTHN_LEVEL.RPC_C_AUTHN_LEVEL_CALL,
                RPC_C_IMP_LEVEL.RPC_C_IMP_LEVEL_IMPERSONATE, null, EOLE_AUTHENTICATION_CAPABILITIES.EOAC_NONE).ThrowOnFailure();
            using (var language = new Bstr("WQL"))
            using (var query = new Bstr($"SELECT * FROM {className} WHERE Active = TRUE"))
            {
                services->ExecQuery(
                    language, query,
                    WBEM_GENERIC_FLAG_TYPE.WBEM_FLAG_FORWARD_ONLY | WBEM_GENERIC_FLAG_TYPE.WBEM_FLAG_RETURN_IMMEDIATELY,
                    null, &found);
            }
            while (true)
            {
                IWbemClassObject* instance = null;
                uint count = 0;
                if (found->Next(WaitForever, 1, &instance, &count).Failed || count == 0)
                    return fallback;
                try
                {
                    if (Text(instance, "InstanceName") is { } name &&
                        name.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase))
                    {
                        return action(services, instance);
                    }
                }
                finally
                {
                    instance->Release();
                }
            }
        }
        catch (COMException)
        {
            // No WMI brightness here: a desktop, an external screen, or a driver without it.
            return fallback;
        }
        finally
        {
            Release(found);
            Release(services);
            Release(locator);
        }
    }

    private static string? Text(IWbemClassObject* instance, string property)
    {
        VARIANT value = default;
        try
        {
            instance->Get(property, 0, ref value);
            return value.Anonymous.Anonymous.vt == VARENUM.VT_BSTR ? value.Anonymous.Anonymous.Anonymous.bstrVal.ToString() : null;
        }
        finally
        {
            PInvoke.VariantClear(ref value);
        }
    }

    private static VARIANT Number(VARENUM type, int number)
    {
        VARIANT value = default;
        value.Anonymous.Anonymous.vt = type;
        if (type == VARENUM.VT_UI1)
            value.Anonymous.Anonymous.Anonymous.bVal = (byte)number;
        else
            value.Anonymous.Anonymous.Anonymous.lVal = number;
        return value;
    }

    private static void Put(IWbemClassObject* instance, string property, VARIANT value) =>
        instance->Put(property, 0, in value, 0);

    private static void Release<T>(T* pointer) where T : unmanaged
    {
        if (pointer != null)
            ((IUnknown*)pointer)->Release();
    }

    /// <summary>A BSTR for one call, freed afterwards.</summary>
    private readonly struct Bstr(string text) : IDisposable
    {
        private readonly nint _value = Marshal.StringToBSTR(text);

        public static implicit operator BSTR(Bstr value) => new(value._value);

        public void Dispose() => Marshal.FreeBSTR(_value);
    }
}
