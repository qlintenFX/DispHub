// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Credentials;

namespace DispHub.Platform.Store;

/// <summary>The date DispHub first ran for this Windows user, kept where reinstalling DispHub doesn't remove it.</summary>
public interface IFirstRunMarker
{
    DateTimeOffset? Read();

    void Write(DateTimeOffset firstRunUtc);
}

/// <summary>For builds without a Pro trial: nothing is kept.</summary>
public sealed class NoFirstRunMarker : IFirstRunMarker
{
    public DateTimeOffset? Read() => null;

    public void Write(DateTimeOffset firstRunUtc)
    {
    }
}

/// <summary>
/// Keeps the first-run date in Windows Credential Manager (Windows Credentials, "DispHub/FirstRun"). Windows removes a
/// Store app's own files and registry when it's uninstalled, so this is what keeps a reinstall from starting a new
/// Pro trial. It holds that date and nothing else; deleting it there is harmless.
/// </summary>
public sealed class CredentialFirstRunMarker(string target = "DispHub/FirstRun") : IFirstRunMarker
{
    private const string UserName = "DispHub";
    private readonly string _target = target;

    public unsafe DateTimeOffset? Read()
    {
        CREDENTIALW* credential;
        fixed (char* target = _target)
        {
            if (!PInvoke.CredRead(new PCWSTR(target), CRED_TYPE.CRED_TYPE_GENERIC, 0, &credential))
                return null;
        }
        try
        {
            string text = Encoding.UTF8.GetString(credential->CredentialBlob, (int)credential->CredentialBlobSize);
            return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset date)
                ? date
                : null;
        }
        finally
        {
            PInvoke.CredFree(credential);
        }
    }

    public unsafe void Write(DateTimeOffset firstRunUtc)
    {
        byte[] blob = Encoding.UTF8.GetBytes(firstRunUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        fixed (char* target = _target)
        fixed (char* user = UserName)
        fixed (byte* data = blob)
        {
            var credential = new CREDENTIALW
            {
                Type = CRED_TYPE.CRED_TYPE_GENERIC,
                TargetName = new PWSTR(target),
                UserName = new PWSTR(user),
                CredentialBlob = data,
                CredentialBlobSize = (uint)blob.Length,
                Persist = CRED_PERSIST.CRED_PERSIST_LOCAL_MACHINE,
            };
            PInvoke.CredWrite(&credential, 0);
        }
    }
}
