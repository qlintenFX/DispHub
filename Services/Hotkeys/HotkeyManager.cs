using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DisplayHub.Constants;
using DisplayHub.Models;

namespace DisplayHub.Services.Hotkeys
{
    public class HotkeyManager
    {
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly HashSet<int> _rawHotkeyIds = new();
        private int nextHotkeyId = 1;
        private readonly Dictionary<int, Profile> registeredHotkeys = new();
        private IntPtr formHandle;

        public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

        public IntPtr FormHandle => formHandle;

        public HotkeyManager(IntPtr formHandle)
        {
            this.formHandle = formHandle;
        }

        /// <summary>
        /// Updates the window handle (needed for WPF where handle is obtained after construction).
        /// </summary>
        public void SetHandle(IntPtr handle)
        {
            formHandle = handle;
        }

        public int RegisterHotkey(Profile profile)
        {
            if (profile.HotKey == 0)
                return -1;

            uint modifiers = 0;
            int mod = profile.HotKeyModifier;
            // WinForms Keys.Alt = 0x40000, Keys.Control = 0x20000, Keys.Shift = 0x10000
            if ((mod & 0x40000) != 0) modifiers |= AppConstants.MOD_ALT;
            if ((mod & 0x20000) != 0) modifiers |= AppConstants.MOD_CONTROL;
            if ((mod & 0x10000) != 0) modifiers |= AppConstants.MOD_SHIFT;

            uint key = (uint)profile.HotKey;

            int id = nextHotkeyId++;
            if (RegisterHotKey(formHandle, id, modifiers, key))
            {
                registeredHotkeys[id] = profile;
                profile.HotkeyId = id;
                return id;
            }

            return -1;
        }

        /// <summary>
        /// Registers a raw hotkey without associating it to a profile.
        /// Used by DynamicControls for directional adjustment hotkeys.
        /// </summary>
        public int RegisterRawHotkey(int vkCode, uint modifiers)
        {
            int id = nextHotkeyId++;
            if (RegisterHotKey(formHandle, id, modifiers, (uint)vkCode))
            {
                _rawHotkeyIds.Add(id);
                return id;
            }
            return -1;
        }

        public bool UnregisterRawHotkey(int id)
        {
            if (id <= 0) return false;
            bool result = UnregisterHotKey(formHandle, id);
            if (result) _rawHotkeyIds.Remove(id);
            return result;
        }

        public bool UnregisterHotkey(int id)
        {
            if (id <= 0)
                return false;

            bool result = UnregisterHotKey(formHandle, id);
            if (result)
            {
                registeredHotkeys.Remove(id);
            }
            return result;
        }

        public void UnregisterAllHotkeys()
        {
            foreach (int id in registeredHotkeys.Keys)
                UnregisterHotKey(formHandle, id);
            registeredHotkeys.Clear();

            // Also unregister raw hotkeys (e.g. Dynamic Controls directional adjustments)
            foreach (int id in _rawHotkeyIds)
                UnregisterHotKey(formHandle, id);
            _rawHotkeyIds.Clear();
        }

        public void ProcessHotkey(IntPtr wParam)
        {
            int id = wParam.ToInt32();
            if (registeredHotkeys.TryGetValue(id, out Profile? profile) && profile != null)
            {
                HotkeyPressed?.Invoke(this, new HotkeyEventArgs(profile));
            }
        }
    }

    public class HotkeyEventArgs : EventArgs
    {
        public Profile Profile { get; }

        public HotkeyEventArgs(Profile profile)
        {
            Profile = profile;
        }
    }
}
