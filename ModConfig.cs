using StardewModdingAPI;

public sealed class ModConfig
{
    public Dictionary<SButton, int> StoredButons { get; set; }
    // Default keyboard keys to use for combination binds.
    public List<SButton> ModifierKeys { get; set; }
    // Default controller buttons to use for combination binds.
    public List<SButton> ModifierButtons { get; set; }
    // Default controller buttons and keyboard keys to disable for single binds.
    public List<SButton> DisabledSingleButtons { get; set; }

    public ModConfig()
    {
        this.StoredButons = new Dictionary<SButton, int>();
        this.ModifierKeys = new List<SButton> { SButton.LeftControl, SButton.RightControl, SButton.LeftAlt, SButton.RightAlt, SButton.LeftShift, SButton.RightShift };
        this.ModifierButtons = new List<SButton>(new[] { SButton.LeftShoulder, SButton.RightShoulder, SButton.LeftTrigger, SButton.RightTrigger, SButton.DPadUp, SButton.DPadDown, SButton.DPadLeft, SButton.DPadRight, SButton.LeftStick, SButton.RightStick });
        this.DisabledSingleButtons = new List<SButton> { SButton.MouseLeft, SButton.MouseRight, SButton.MouseMiddle, SButton.MouseX1, SButton.MouseX2, SButton.W, SButton.A, SButton.S, SButton.D, SButton.ControllerA, SButton.ControllerB, SButton.ControllerX, SButton.ControllerY, SButton.ControllerStart, SButton.ControllerBack };
    }
}
