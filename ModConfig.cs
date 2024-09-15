
public sealed class ModConfig
{
    public List<string> ButtonsToEnterBindMode { get; set; } // Default button combo to Enter Bind mode.
    public List<string> ButtonsToClearAllBinds { get; set; } // Default Button combo used to clear all binds.
    public List<string> ModifierKeys { get; set; } // Default keyboard keys to use for combination binds.
    public List<string> ModifierButtons { get; set; }    // Default controller buttons to use for combination binds.
    public List<string> DisabledSingleButtons { get; set; }    // Default controller buttons and keyboard keys to disable for single binds.

    public ModConfig()
    {
        this.ButtonsToEnterBindMode = new List<string>()
            {
                 "ControllerBack+ControllerStart",
                 "F1+F2"
            };
        this.ButtonsToClearAllBinds = new List<string>()
            {
                 "LeftShoulder+ControllerBack",
                 "F1+F4"
            };
        this.ModifierKeys = new List<string>
                {
                    "LeftControl",
                    "RightControl",
                    "LeftAlt",
                    "RightAlt",
                    "LeftShift",
                    "RightShift"
                };

        this.ModifierButtons = new List<string>
            {
                    "LeftShoulder",
                    "RightShoulder",
                    "LeftTrigger",
                    "RightTrigger",
                    "LeftStick",
                    "RightStick"
                };

        this.DisabledSingleButtons = new List<string>
                {
                    "E",
                    "F",
                    "MouseLeft",
                    "MouseRight",
                    "MouseMiddle",
                    "MouseX1",
                    "MouseX2",
                    "Escape",
                    "Enter",
                    "F1",
                    "W",
                    "A",
                    "S",
                    "D",
                    "ControllerA",
                    "ControllerB",
                    "ControllerX",
                    "ControllerY",
                    "ControllerStart",
                    "ControllerBack"
                };
    }
}
