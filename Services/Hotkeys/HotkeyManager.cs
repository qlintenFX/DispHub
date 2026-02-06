using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using KeyedColors.Constants;
using KeyedColors.Models;

namespace KeyedColors.Services.Hotkeys
{
    public class HotkeyManager
    {
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private int nextHotkeyId = 1;
        private readonly Dictionary<int, Profile> registeredHotkeys = new();
        private readonly IntPtr formHandle;

        public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

        public IntPtr FormHandle => formHandle;

        public HotkeyManager(IntPtr formHandle)
        {
            this.formHandle = formHandle;
        }

        public int RegisterHotkey(Profile profile)
        {
            if (profile.HotKey == Keys.None)
                return -1;

            uint modifiers = 0;
            if ((profile.HotKeyModifier & Keys.Alt) == Keys.Alt)
                modifiers |= AppConstants.MOD_ALT;
            if ((profile.HotKeyModifier & Keys.Control) == Keys.Control)
                modifiers |= AppConstants.MOD_CONTROL;
            if ((profile.HotKeyModifier & Keys.Shift) == Keys.Shift)
                modifiers |= AppConstants.MOD_SHIFT;

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
        public int RegisterRawHotkey(Keys key, uint modifiers)
        {
            int id = nextHotkeyId++;
            if (RegisterHotKey(formHandle, id, modifiers, (uint)key))
            {
                return id;
            }
            return -1;
        }

        /// <summary>
        /// Unregisters a raw hotkey by ID (not associated with a profile).
        /// </summary>
        public bool UnregisterRawHotkey(int id)
        {
            if (id <= 0) return false;
            return UnregisterHotKey(formHandle, id);
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
            {
                UnregisterHotKey(formHandle, id);
            }
            registeredHotkeys.Clear();
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
