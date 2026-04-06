using StardewModdingAPI;
using System.Collections.Generic;

namespace QuickSelect
{
    public sealed class ModConfig
    {
        /// <summary>Keyboard key to enter binding mode (instant).</summary>
        public SButton BindModeKey { get; set; } = SButton.F1;

        /// <summary>Controller button to enter binding mode (hold).</summary>
        public SButton BindModeButton { get; set; } = SButton.ControllerBack;

        /// <summary>How long to hold the controller bind button (in milliseconds).</summary>
        public int BindHoldMs { get; set; } = 300;

        /// <summary>Key to clear all bindings when held.</summary>
        public SButton ClearAllBindingsKey { get; set; } = SButton.OemTilde;

        /// <summary>Auto-use consumables (food/bombs/stairs) and swap back to previous item.</summary>
        public bool AutoUseAndSwapBack { get; set; } = true;

        /// <summary>Controller button label style: "Sony" or "Xbox".</summary>
        public string ControllerStyle { get; set; } = "Sony";

        /// <summary>Extra item IDs the user considers consumable (auto-used and swapped back).</summary>
        public List<string> ExtraConsumableIds { get; set; } = new();

        /// <summary>Saved keybindings. Keys are button combos, values are item names.</summary>
        public Dictionary<string, string> Bindings { get; set; } = new();

        /// <summary>Keyboard keys treated as modifiers for combo binds.</summary>
        public List<SButton> ModifierKeys { get; set; } = new()
        {
            SButton.LeftControl,
            SButton.RightControl,
            SButton.LeftAlt,
            SButton.RightAlt,
            SButton.LeftShift,
            SButton.RightShift
        };

        /// <summary>Controller buttons treated as modifiers for combo binds.</summary>
        public List<SButton> ModifierButtons { get; set; } = new()
        {
            SButton.LeftShoulder,
            SButton.RightShoulder,
            SButton.LeftTrigger,
            SButton.RightTrigger,
            SButton.LeftStick,
            SButton.RightStick
        };

        /// <summary>Buttons that cannot be used as single-key binds.</summary>
        public List<SButton> DisabledSingleButtons { get; set; } = new()
        {
            SButton.E,
            SButton.F,
            SButton.MouseLeft,
            SButton.MouseRight,
            SButton.MouseMiddle,
            SButton.MouseX1,
            SButton.MouseX2,
            SButton.W,
            SButton.A,
            SButton.S,
            SButton.D,
            SButton.ControllerA,
            SButton.ControllerB,
            SButton.ControllerX,
            SButton.ControllerY,
            SButton.ControllerStart,
            SButton.ControllerBack
        };

        /// <summary>Directional buttons blocked from being part of any combo.</summary>
        public List<SButton> DirectionalButtons { get; set; } = new()
        {
            SButton.DPadUp,
            SButton.DPadDown,
            SButton.DPadLeft,
            SButton.DPadRight,
            SButton.LeftThumbstickUp,
            SButton.LeftThumbstickDown,
            SButton.LeftThumbstickLeft,
            SButton.LeftThumbstickRight,
            SButton.W,
            SButton.A,
            SButton.S,
            SButton.D,
            SButton.Up,
            SButton.Down,
            SButton.Left,
            SButton.Right
        };
    }
}
