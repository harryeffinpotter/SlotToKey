using StardewModdingAPI;
using System.Collections.Generic;

public sealed class ModConfig
{
    public Dictionary<SButton, int> SlotKeyBindings { get; set; }  // Bindings for slots
    public Dictionary<SButton, int> ItemKeyBindings { get; set; }  // Bindings for items

    // Default keyboard keys to use for combination binds.
    public List<SButton> ModifierKeys { get; set; }
    // Default controller buttons to use for combination binds.
    public List<SButton> ModifierButtons { get; set; }
    // Default controller buttons and keyboard keys to disable for single binds.
    public List<SButton> DisabledSingleButtons { get; set; }

    public bool BindToItem { get; set; }  // Whether to bind to item instead of slot

    public ModConfig()
    {
        this.SlotKeyBindings = new Dictionary<SButton, int>();
        this.ItemKeyBindings = new Dictionary<SButton, int>();

        this.ModifierKeys = new List<SButton>
            {
                SButton.LeftControl,
                SButton.RightControl,
                SButton.LeftAlt,
                SButton.RightAlt,
                SButton.LeftShift,
                SButton.RightShift
            };

        this.ModifierButtons = new List<SButton>(new[]
        {
                SButton.LeftShoulder,
                SButton.RightShoulder,
                SButton.LeftTrigger,
                SButton.RightTrigger,
                SButton.DPadUp,  // D-pad as modifier
                SButton.DPadDown,
                SButton.DPadLeft,
                SButton.DPadRight,
                SButton.LeftStick,
                SButton.RightStick
            });

        this.DisabledSingleButtons = new List<SButton>
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

        this.BindToItem = false;  // Default is to bind to slots
    }
}
