using StardewModdingAPI;
using StardewValley;
using StardewModdingAPI.Events;
using System.Collections.Generic;
using StardewValley.Menus;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SlotToKey
{
    public class ModEntry : Mod
    {
        private ModConfig Config;
        
        private Dictionary<long, Dictionary<SButton, int>> playerSlotBindings = new Dictionary<long, Dictionary<SButton, int>>();
        private bool isBindingMode = false;
        private int currentSlotToBind = -1;
        private List<SButton> heldButtons = new List<SButton>();

        private long currentPlayerID;

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Input.ButtonReleased += OnButtonReleased; // Capture release event
            helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        }

        private void BindHeldButtons()
        {
            if (currentSlotToBind == -1 || !isBindingMode) return;

            if (!playerSlotBindings.TryGetValue(currentPlayerID, out var bindings))
            {
                bindings = new Dictionary<SButton, int>();
                playerSlotBindings[currentPlayerID] = bindings;
            }

            if (heldButtons.Count() == 1)
            {
                var forbiddenButtons = this.Config.DisabledSingleButtons;
                // Single button binding
                var button = heldButtons[0];
                if (forbiddenButtons.Contains(button))
                {
                    return;
                }
                bindings[button] = currentSlotToBind;
                Monitor.Log($"Bound button {button} to slot {currentSlotToBind + 1} for player {currentPlayerID}", LogLevel.Info);
            }
            else if (heldButtons.Count() > 1)
            {
                // Combo binding (first button as modifier)
                var modifier = heldButtons[0];
                var mainButton = heldButtons[1];
                bindings[modifier | mainButton] = currentSlotToBind;
                Monitor.Log($"Bound combo {modifier} + {mainButton} to slot {currentSlotToBind + 1} for player {currentPlayerID}", LogLevel.Info);
            }

            // Reset binding mode
            isBindingMode = false;
            currentSlotToBind = -1;
            heldButtons.Clear();
        }
        private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
        {
            // Only handle release when in binding mode and waiting for release
            if (heldButtons.Count() == 0 || !isBindingMode) return;
            if (isBindingMode)
            {
                BindHeldButtons();
            }
            if (Game1.activeClickableMenu != null)
            {
                Game1.activeClickableMenu.exitThisMenu();
            }
            heldButtons.Clear();
        }
        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            Farmer currentPlayer = GetCurrentPlayer();
            if (currentPlayer == null) return;
            List<SButton> modKeys = new List<SButton>(this.Config.ModifierKeys);
            List<SButton> modButtons = new List<SButton>(this.Config.ModifierButtons);
            List<SButton> disabledButtons = new List<SButton>(this.Config.DisabledSingleButtons);

            if (modKeys.Any(key => e.Pressed.Contains(key)) || modButtons.Any(button => e.Pressed.Contains(button)))
            {
                heldButtons.AddRange(e.Pressed);
                // Wait until all buttons are released to bind the combo
            }
            else
            {
                heldButtons.AddRange(e.Pressed);
            }
            if (isBindingMode) return;
            if (!playerSlotBindings.ContainsKey(currentPlayer.UniqueMultiplayerID))
            {
                playerSlotBindings[currentPlayer.UniqueMultiplayerID] = new Dictionary<SButton, int>();
            }

            var bindings = playerSlotBindings[currentPlayer.UniqueMultiplayerID]; // Create a set of currently held buttons

            // Check if the bindings dictionary contains the held combo
            // Check if the bindings dictionary contains the held combo
            SButton heldButtonsSet = new();
            if (heldButtons.Contains(SButton.MouseLeft))
            {
                heldButtons.Remove(SButton.MouseLeft);
            }
            if (heldButtons.Count() > 1)
            {
                Monitor.Log($"Adding {heldButtons[0]} | {heldButtons[1]}", LogLevel.Debug);

                heldButtonsSet = (heldButtons[0] | heldButtons[1]);
                heldButtons.Clear();
            }
            if (heldButtons.Count() == 1)
            {
                if (modButtons.Contains(heldButtons[0]) || modKeys.Contains(heldButtons[0]))
                { return; }
                Monitor.Log($"Adding {heldButtons[0]}", LogLevel.Debug);
                List<SButton> disabledButtonList = this.Config.DisabledSingleButtons;

                // Default controller buttons and keyboard keys to disable for single bind

                heldButtonsSet = (heldButtons[0]);
                heldButtons.Clear();
            }
            if (bindings.ContainsKey(heldButtonsSet))
            {
                Monitor.Log($"KEY FOUND: {heldButtonsSet}", LogLevel.Debug);

                int slot = bindings[heldButtonsSet]; // Get the slot bound to the current key combination
                SelectInventorySlot(currentPlayer, slot); // Select the corresponding slot
            }
            else
            {
                //heldButtons.Clear();
            }
        }
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null)
                return;

            Monitor.Log($"Button Pressed: {e.Button.ToString()}", LogLevel.Debug);

            Farmer currentPlayer = GetCurrentPlayer();
            if (currentPlayer == null) return;
            if (e.Button == SButton.F1 || e.Button == SButton.ControllerBack)
            {
                heldButtons.Clear();
                EnterBindingMode(currentPlayer);
                return;
            }
        }

        private Farmer? GetCurrentPlayer()
        {
            foreach (Farmer farmer in Game1.getAllFarmers())
            {
                if (farmer.IsLocalPlayer)
                {
                    return farmer;
                }
            }

            return null;
        }

        public void EnterBindingMode(Farmer currentPlayer)
        {
            int currentSlot = currentPlayer.CurrentToolIndex;
            currentSlotToBind = currentSlot;
            currentPlayerID = currentPlayer.UniqueMultiplayerID;
            isBindingMode = true;

            Game1.activeClickableMenu = new DialogueBoxWithCustomIcon(
                $"Press a button to bind to slot {currentSlot + 1} for player {currentPlayer.Name}",
                new Rectangle(128, 256, 64, 64) // Custom checkmark
            );
        }



        private void SelectInventorySlot(Farmer player, int slotIndex)
        {
            if (player == null || player.Items == null || slotIndex < 0 || slotIndex >= player.MaxItems)
                return;

            player.CurrentToolIndex = slotIndex;
            heldButtons.Clear();
        }

        private bool IsMouseButton(SButton button)
        {
            return button == SButton.MouseLeft || button == SButton.MouseRight ||
                   button == SButton.MouseMiddle || button == SButton.MouseX1 ||
                   button == SButton.MouseX2;
        }

        private bool IsControllerButton(SButton button)
        {
            return button == SButton.ControllerA || button == SButton.ControllerB ||
                   button == SButton.ControllerX || button == SButton.ControllerY ||
                   button == SButton.ControllerBack || button == SButton.ControllerStart ||
                   button == SButton.LeftShoulder || button == SButton.RightShoulder ||
                   button == SButton.LeftTrigger || button == SButton.RightTrigger ||
                   button == SButton.LeftStick || button == SButton.RightStick ||
                   button == SButton.DPadUp || button == SButton.DPadDown ||
                   button == SButton.DPadLeft || button == SButton.DPadRight;
        }

        private bool IsKeyboardButton(SButton button)
        {
            return (button >= SButton.A && button <= SButton.Z) ||  // A-Z
                   (button >= SButton.D0 && button <= SButton.D9) || // 0-9
                   (button >= SButton.F1 && button <= SButton.F24) || // F1-F24
                   button == SButton.Space || button == SButton.Enter ||
                   button == SButton.Tab || button == SButton.Escape ||
                   button == SButton.LeftControl || button == SButton.RightControl ||
                   button == SButton.LeftShift || button == SButton.RightShift ||
                   button == SButton.LeftAlt || button == SButton.RightAlt ||
                   button == SButton.LeftWindows || button == SButton.RightWindows;
        }
    }

    // Override DialogueBox to make text render faster
    public class DialogueBoxWithCustomIcon : DialogueBox
    {
        private Rectangle customIconSource;

        public DialogueBoxWithCustomIcon(string dialogue, Rectangle iconSource) : base(dialogue)
        {
            customIconSource = iconSource;
            this.characterIndexInDialogue = dialogue.Length;  // Instantly display all text
        }

        public override void draw(SpriteBatch b)
        {
            base.draw(b);

            b.Draw(
                Game1.mouseCursors,
                new Vector2(xPositionOnScreen + width - 64, yPositionOnScreen + height - 64),
                customIconSource,
                Color.White,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                1f
            );
        }
    }
}
