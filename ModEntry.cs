using StardewModdingAPI;
using StardewValley;
using StardewModdingAPI.Events;
using System.Collections.Generic;
using StardewValley.Menus;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace SlotToKey
{
    public class ModEntry : Mod
    {
        private ModConfig Config;
        private Dictionary<long, Dictionary<string, string>> playerSlotBindings = new Dictionary<long, Dictionary<string, string>>();
        private bool isBindingMode = false;
        private int currentSlotToBind = -1;
        private List<SButton> heldButtons = new List<SButton>();
        private long currentPlayerID;
        private string currentItemIdToBind;

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Input.ButtonReleased += OnButtonReleased;
            helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        }

        private string GetButtonKey(List<SButton> buttons)
        {
            return string.Join("+", buttons.Select(b => b.ToString()));
        }

        private void BindHeldButtons()
        {
            if (currentSlotToBind == -1 || !isBindingMode) return;
            if (!playerSlotBindings.TryGetValue(currentPlayerID, out var bindings))
            {
                bindings = new Dictionary<string, string>();
                playerSlotBindings[currentPlayerID] = bindings;
            }

            string buttonKey = GetButtonKey(heldButtons);

            if (heldButtons.Count == 1)
            {
                var forbiddenButtons = this.Config.DisabledSingleButtons;
                if (forbiddenButtons.Contains(heldButtons[0]))
                {
                    return;
                }

                bindings[buttonKey] = currentItemIdToBind;
                Monitor.Log($"Bound buttons {buttonKey} to item ID {currentItemIdToBind} for player {currentPlayerID}", LogLevel.Info);
            }
            else if (heldButtons.Count > 1)
            {
                bindings[buttonKey] = currentItemIdToBind;
                Monitor.Log($"Bound combo {buttonKey} to item ID {currentItemIdToBind} for player {currentPlayerID}", LogLevel.Info);
            }

            isBindingMode = false;
            currentSlotToBind = -1;
            currentItemIdToBind = null;
            heldButtons.Clear();
        }

        private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
        {
            if (!Context.IsWorldReady)
            {
                heldButtons.Clear();
                return;
            }
            Farmer currentPlayer = GetCurrentPlayer();

            if (heldButtons.Contains(SButton.OemTilde))
            {
                if (!playerSlotBindings.ContainsKey(currentPlayer.UniqueMultiplayerID))
                {
                    playerSlotBindings[currentPlayer.UniqueMultiplayerID] = new Dictionary<string, string>();
                }

                var bindings = playerSlotBindings[Game1.player.UniqueMultiplayerID];
                bindings.Clear();
            }

            if (e.Button == SButton.F1 || e.Button == SButton.ControllerBack)
            {
                return;
            }

            if (isBindingMode)
            {
                BindHeldButtons();

                if (Game1.activeClickableMenu != null)
                {
                    Game1.activeClickableMenu.exitThisMenu();
                }
                isBindingMode = false;
                heldButtons.Clear();
            }
            else
            {
                var forbiddenButtons = this.Config.DisabledSingleButtons;

                if (heldButtons.Count == 1 && forbiddenButtons.Contains(heldButtons[0]))
                {
                    heldButtons.Clear();
                    return;
                }

                if (!playerSlotBindings.ContainsKey(currentPlayer.UniqueMultiplayerID))
                {
                    playerSlotBindings[currentPlayer.UniqueMultiplayerID] = new Dictionary<string, string>();
                }

                var bindings = playerSlotBindings[Game1.player.UniqueMultiplayerID];
                string buttonKey = GetButtonKey(heldButtons);

                if (bindings.ContainsKey(buttonKey))
                {
                    Monitor.Log($"KEY FOUND: {buttonKey}", LogLevel.Debug);
                    string itemId = bindings[buttonKey];
                    SelectInventoryItem(Game1.player, itemId);

                }
                heldButtons.Clear();
            }
        }




        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            Farmer currentPlayer = GetCurrentPlayer();
            if (currentPlayer == null || !playerSlotBindings.ContainsKey(currentPlayer.UniqueMultiplayerID))
                return;

            var bindings = playerSlotBindings[currentPlayer.UniqueMultiplayerID];
            string buttonKey = GetButtonKey(e.Held.ToList());

            // Only suppress if the current button combination is found in the bindings dictionary
            if (bindings.ContainsKey(buttonKey))
            {
                foreach (SButton pressedButton in e.Pressed)
                {
                    this.Helper.Input.Suppress(pressedButton);
                }

                // Trigger the bound action for key combos
                string itemId = bindings[buttonKey];
                SelectInventoryItem(Game1.player, itemId);

                // Clear held buttons after handling the action
                heldButtons.Clear();
            }
        }


        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            Farmer currentPlayer = GetCurrentPlayer();
            if (currentPlayer == null || !playerSlotBindings.ContainsKey(currentPlayer.UniqueMultiplayerID))
                return;

            heldButtons.Add(e.Button);

            Monitor.Log($"Currently held buttons: {string.Join(", ", heldButtons)}", LogLevel.Info);
            Monitor.Log($"Button Pressed: {e.Button.ToString()}", LogLevel.Debug);

            if (!Context.IsWorldReady || Game1.activeClickableMenu != null)
                return;

            if (e.Button == SButton.F1 || e.Button == SButton.ControllerBack)
            {
                heldButtons.Clear();
                EnterBindingMode(currentPlayer);
                return;
            }

            // Handle keybindings for combos
            string buttonKey = GetButtonKey(heldButtons);

            if (playerSlotBindings.ContainsKey(currentPlayer.UniqueMultiplayerID)
                && playerSlotBindings[currentPlayer.UniqueMultiplayerID].ContainsKey(buttonKey))
            {
                string itemId = playerSlotBindings[currentPlayer.UniqueMultiplayerID][buttonKey];
                SelectInventoryItem(Game1.player, itemId);

                // Only clear held buttons if no modifier keys are held
                if (!IsModifierHeld())
                {
                    heldButtons.Clear(); // Clear after successfully selecting the item if no modifiers
                }
            }

            // Clear buttons if no valid binding is found and no modifier keys are held
            else if (!playerSlotBindings[currentPlayer.UniqueMultiplayerID].ContainsKey(buttonKey) && !IsModifierHeld())
            {
                heldButtons.Clear();
            }
        }



        // Utility method to determine if a button is for movement (D-pad buttons)
        private bool IsMovementButton(SButton button)
        {
            return button == SButton.DPadUp || button == SButton.DPadDown || button == SButton.DPadLeft || button == SButton.DPadRight;
        }

        // Check if any modifier keys are currently held
        private bool IsModifierHeld()
        {
            List<SButton> modButtons = new List<SButton>(this.Config.ModifierButtons);
            List<SButton> modKeys = new List<SButton>(this.Config.ModifierKeys);
            return modKeys.Any(heldButtons.Contains) || modButtons.Any(heldButtons.Contains);
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
            var currentItem = currentPlayer.CurrentItem;
            if (currentItem == null) return;

            currentSlotToBind = currentSlot;
            currentItemIdToBind = currentItem.Name; // Use the item's name as the identifier

            currentPlayerID = currentPlayer.UniqueMultiplayerID;
            isBindingMode = true;

            Game1.activeClickableMenu = new DialogueBoxWithCustomIcon(
                $"Press a button to bind to item '{currentItem.DisplayName}' for player {currentPlayer.Name}",
                new Rectangle(128, 256, 64, 64)
            );
        }

        private void SelectInventoryItem(Farmer player, string itemId)
        {
            if (player == null || player.Items == null || string.IsNullOrEmpty(itemId))
                return;

            bool itemFound = false;
            int desiredItemIndex = -1;
            int currentIndex = 0;
            foreach (var item in player.Items)
            {
                if (item?.Name == itemId)
                {
                    desiredItemIndex = currentIndex;
                    itemFound = true;
                    break;
                }
                currentIndex++;
            }

            if (!itemFound)
            {
                Game1.activeClickableMenu = new DialogueBoxWithCustomIcon(
                    $"Item with ID {itemId} not found in {player.Name}'s inventory. Removing keybind.",
                    new Rectangle(0, 0, 64, 64)
                );
                RemoveKeyBindForItem(player, itemId);
                return;
            }

            while (desiredItemIndex > 11)
            {
                desiredItemIndex -= 12;
                player.shiftToolbar(true);
            }

            player.CurrentToolIndex = desiredItemIndex;
            heldButtons.Clear();
        }

        private void RemoveKeyBindForItem(Farmer player, string itemId)
        {
            var bindings = playerSlotBindings[player.UniqueMultiplayerID];
            var keyToRemove = bindings.FirstOrDefault(kv => kv.Value == itemId).Key;
            if (keyToRemove != null)
            {
                bindings.Remove(keyToRemove);
            }
        }
    }

    public class DialogueBoxWithCustomIcon : DialogueBox
    {
        private Rectangle customIconSource;

        public DialogueBoxWithCustomIcon(string dialogue, Rectangle iconSource) : base(dialogue)
        {
            customIconSource = iconSource;
            this.characterIndexInDialogue = dialogue.Length;
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