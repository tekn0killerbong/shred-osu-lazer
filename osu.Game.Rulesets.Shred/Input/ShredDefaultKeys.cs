using System;
using System.Runtime.InteropServices;
using osu.Framework.Input.Bindings;

namespace osu.Game.Rulesets.Shred.Input
{
    public static class ShredDefaultKeys
    {
        // Lazer binds physical keys, not text characters. Ask the active Windows
        // layout which physical key produces è, including Canadian layouts.
        // If the character needs a dead-key sequence, use normal in-game rebinding.
        public static InputKey FingerPick
        {
            get
            {
                if (!OperatingSystem.IsWindows()) return InputKey.Quote;
                IntPtr layout = GetKeyboardLayout(0);
                short key = VkKeyScanExW('è', layout);
                if (key == -1) return InputKey.Quote;
                uint scan = MapVirtualKeyExW((uint)(key & 255), 4, layout) & 255;
                InputKey physical = FromScanCode(scan, InputKey.Quote);
                return physical == InputKey.K || physical == InputKey.L || physical == InputKey.Semicolon ? InputKey.Quote : physical;
            }
        }
        public static InputKey ModeIncrease
        {
            get
            {
                if (!OperatingSystem.IsWindows()) return InputKey.BracketLeft;
                IntPtr layout = GetKeyboardLayout(0);
                short key = VkKeyScanExW('^', layout);
                if (key == -1) return InputKey.BracketLeft;
                uint scan = MapVirtualKeyExW((uint)(key & 255), 4, layout) & 255;
                // Bind the physical circumflex key, including a dead key.
                // It can be rebound normally for layouts requiring a chord.
                return FromScanCode(scan, InputKey.BracketLeft);
            }
        }
        private static InputKey FromScanCode(uint scan, InputKey fallback = InputKey.K) => scan switch
        {
            0x02 => InputKey.Number1, 0x03 => InputKey.Number2, 0x04 => InputKey.Number3,
            0x05 => InputKey.Number4, 0x06 => InputKey.Number5, 0x07 => InputKey.Number6,
            0x08 => InputKey.Number7, 0x09 => InputKey.Number8, 0x0a => InputKey.Number9,
            0x0b => InputKey.Number0, 0x0c => InputKey.Minus, 0x0d => InputKey.Plus,
            0x10 => InputKey.Q, 0x11 => InputKey.W, 0x12 => InputKey.E, 0x13 => InputKey.R,
            0x14 => InputKey.T, 0x15 => InputKey.Y, 0x16 => InputKey.U, 0x17 => InputKey.I,
            0x18 => InputKey.O, 0x19 => InputKey.P, 0x1a => InputKey.BracketLeft, 0x1b => InputKey.BracketRight,
            0x1e => InputKey.A, 0x1f => InputKey.S, 0x20 => InputKey.D, 0x21 => InputKey.F,
            0x22 => InputKey.G, 0x23 => InputKey.H, 0x24 => InputKey.J, 0x25 => InputKey.K,
            0x26 => InputKey.L, 0x27 => InputKey.Semicolon, 0x28 => InputKey.Quote,
            0x29 => InputKey.Tilde, 0x2b => InputKey.BackSlash,
            0x2c => InputKey.Z, 0x2d => InputKey.X, 0x2e => InputKey.C, 0x2f => InputKey.V,
            0x30 => InputKey.B, 0x31 => InputKey.N, 0x32 => InputKey.M,
            0x33 => InputKey.Comma, 0x34 => InputKey.Period, 0x35 => InputKey.Slash,
            0x56 => InputKey.NonUSBackSlash,
            _ => fallback
        };
        [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint threadId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern short VkKeyScanExW(char ch, IntPtr layout);
        [DllImport("user32.dll")] private static extern uint MapVirtualKeyExW(uint code, uint mapType, IntPtr layout);
    }
}
