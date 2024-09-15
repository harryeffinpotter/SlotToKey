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
        private Dictionary<long, bool> isBindingMode = new Dictionary<long, bool>();
        private Dictionary<long, int> currentSlotToBind = new Dictionary<long, int>();
        private Dictionary<long, HashSet<SButton>> playerHeldButtons = new Dictionary<long, HashSet<SButton>>();
        private Dictionary<long, string> currentItemIdToBind = new Dictionary<long, string>();

        private HashSet<SButton> DisabledSingleButtons;
        private HashSet<string> ButtonKeysToEnterBindMode;
        private HashSet<string> ButtonKeysToClearAllBinds;
        private HashSet<SButton> ModifierKeys;
        private HashSet<SButton> ModifierButtons;

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Input.ButtonReleased += OnButtonReleased;
            helper.Events.Input.ButtonsChanged += OnButtonsChanged;

            // Parse the config into usable data structures
            this.DisabledSingleButtons = new HashSet<SButton>();
            foreach (var buttonStr in Config.DisabledSingleButtons)
            {
                if (Enum.TryParse<SButton>(buttonStr.Trim(), out SButton button))
                    this.DisabledSingleButtons.Add(button);
                else
                    Monitor.Log($"Invalid button '{buttonStr}' in DisabledSingleButtons", LogLevel.Warn);
            }

            this.ButtonKeysToEnterBindMode = new HashSet<string>();
            foreach (var combo in Config.ButtonsToEnterBindMode)
            {
                var comboSet = ParseButtonCombination(combo);
                var comboKey = GetButtonKey(comboSet);
                this.ButtonKeysToEnterBindMode.Add(comboKey);
            }

            this.ButtonKeysToClearAllBinds = new HashSet<string>();
            foreach (var combo in Config.ButtonsToClearAllBinds)
            {
                var comboSet = ParseButtonCombination(combo);
                var comboKey = GetButtonKey(comboSet);
                this.ButtonKeysToClearAllBinds.Add(comboKey);
            }

            this.ModifierKeys = new HashSet<SButton>();
            foreach (var buttonStr in Config.ModifierKeys)
            {
                if (Enum.TryParse<SButton>(buttonStr.Trim(), out SButton button))
                    this.ModifierKeys.Add(button);
                else
                    Monitor.Log($"Invalid button '{buttonStr}' in ModifierKeys", LogLevel.Warn);
            }

            this.ModifierButtons = new HashSet<SButton>();
            foreach (var buttonStr in Config.ModifierButtons)
            {
                if (Enum.TryParse<SButton>(buttonStr.Trim(), out SButton button))
                    this.ModifierButtons.Add(button);
                else
                    Monitor.Log($"Invalid button '{buttonStr}' in ModifierButtons", LogLevel.Warn);
            }
        }

        private string GetButtonKey(IEnumerable<SButton> buttons)
        {
            var sortedButtons = buttons.Select(b => b.ToString()).OrderBy(s => s);
            return string.Join("+", sortedButtons);
        }

        private HashSet<SButton> ParseButtonCombination(string combo)
        {
            HashSet<SButton> buttons = new HashSet<SButton>();
            var parts = combo.Split('+');
            foreach (var part in parts)
            {
                if (Enum.TryParse<SButton>(part.Trim(), out SButton button))
                {
                    buttons.Add(button);
                }
                else
                {
                    Monitor.Log($"Invalid button '{part}' in configuration", LogLevel.Warn);
                }
            }
            return buttons;
        }

        private void BindHeldButtons(Farmer player)
        {
            long playerID = player.UniqueMultiplayerID;

            if (!currentSlotToBind.ContainsKey(playerID) || currentSlotToBind[playerID] == -1 || !isBindingMode.ContainsKey(playerID) || !isBindingMode[playerID])
                return;

            if (!playerSlotBindings.TryGetValue(playerID, out var bindings))
            {
                bindings = new Dictionary<string, string>();
                playerSlotBindings[playerID] = bindings;
            }

            var heldButtons = playerHeldButtons[playerID];
            string buttonKey = GetButtonKey(heldButtons);

            if (heldButtons.Count == 1)
            {
                if (this.DisabledSingleButtons.Contains(heldButtons.First()))
                {
                    return;
                }

                bindings[buttonKey] = currentItemIdToBind[playerID];
                Monitor.Log($"Bound button {buttonKey} to item '{currentItemIdToBind[playerID]}' for player {player.Name}", LogLevel.Info);
            }
            else if (heldButtons.Count > 1)
            {
                bindings[buttonKey] = currentItemIdToBind[playerID];
                Monitor.Log($"Bound combo {buttonKey} to item '{currentItemIdToBind[playerID]}' for player {player.Name}", LogLevel.Info);
            }

            isBindingMode[playerID] = false;
            currentSlotToBind[playerID] = -1;
            currentItemIdToBind[playerID] = null;
            heldButtons.Clear();
        }

        private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            Farmer player = Game1.player;
            if (player == null)
                return;

            long playerID = player.UniqueMultiplayerID;

            if (!playerHeldButtons.ContainsKey(playerID))
                return;

            var heldButtons = playerHeldButtons[playerID];

            if (isBindingMode.ContainsKey(playerID) && isBindingMode[playerID])
            {
                // Wait until the player has pressed new buttons to bind
                if (heldButtons.Count == 0)
                    return;

                BindHeldButtons(player);

                if (Game1.activeClickableMenu != null)
                {
                    Game1.activeClickableMenu.exitThisMenu();
                }

                isBindingMode[playerID] = false;
                heldButtons.Clear();
            }
            else
            {
                if (heldButtons.Count == 0)
                    return;

                if (heldButtons.Count == 1 && this.DisabledSingleButtons.Contains(heldButtons.First()))
                {
                    heldButtons.Clear();
                    return;
                }

                if (!playerSlotBindings.ContainsKey(playerID))
                {
                    playerSlotBindings[playerID] = new Dictionary<string, string>();
                }

                var bindings = playerSlotBindings[playerID];
                string buttonKey = GetButtonKey(heldButtons);

                if (bindings.ContainsKey(buttonKey))
                {
                    Monitor.Log($"Key found: {buttonKey} for player {player.Name}", LogLevel.Debug);
                    string itemId = bindings[buttonKey];
                    SelectInventoryItem(player, itemId);
                }
                heldButtons.Clear();
            }
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            Farmer player = Game1.player;
            if (player == null)
                return;

            long playerID = player.UniqueMultiplayerID;

            if (!playerHeldButtons.ContainsKey(playerID))
                playerHeldButtons[playerID] = new HashSet<SButton>();

            var heldButtons = playerHeldButtons[playerID];

            // Update held buttons based on pressed and released buttons
            foreach (var button in e.Pressed)
            {
                heldButtons.Add(button);
            }

            foreach (var button in e.Released)
            {
                heldButtons.Remove(button);
            }

            // If we're already in binding mode, do not check for entering or clearing binds
            if (isBindingMode.ContainsKey(playerID) && isBindingMode[playerID])
                return;

            // Check if the held buttons match ButtonsToEnterBindMode
            string heldButtonsKey = GetButtonKey(heldButtons);

            if (this.ButtonKeysToEnterBindMode.Contains(heldButtonsKey))
            {
                EnterBindingMode(player);
                return;
            }

            // Check if the held buttons match ButtonsToClearAllBinds
            if (this.ButtonKeysToClearAllBinds.Contains(heldButtonsKey))
            {
                playerSlotBindings[playerID] = new Dictionary<string, string>();
                Game1.activeClickableMenu = new DialogueBoxWithCustomIcon(
                    $"Cleared all keybinds for player {player.Name}.",
                    new Rectangle(0, 0, 64, 64)
                );
                heldButtons.Clear();
                return;
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            Farmer player = Game1.player;
            if (player == null)
                return;

            long playerID = player.UniqueMultiplayerID;

            if (!playerHeldButtons.ContainsKey(playerID))
                playerHeldButtons[playerID] = new HashSet<SButton>();

            // No need to add to heldButtons here; OnButtonsChanged handles it

            Monitor.Log($"Player {player.Name} held buttons: {string.Join(", ", playerHeldButtons[playerID])}", LogLevel.Debug);
            Monitor.Log($"Button Pressed: {e.Button}", LogLevel.Debug);

            if (!Context.IsWorldReady || Game1.activeClickableMenu != null)
                return;

            // No need to check for entering bind mode here; it's handled in OnButtonsChanged

            if (isBindingMode.ContainsKey(playerID) && isBindingMode[playerID])
                return;

            if (!playerSlotBindings.ContainsKey(playerID))
            {
                playerSlotBindings[playerID] = new Dictionary<string, string>();
            }
        }

        public void EnterBindingMode(Farmer player)
        {
            long playerID = player.UniqueMultiplayerID;
            int currentSlot = player.CurrentToolIndex;
            var currentItem = player.CurrentItem;
            if (currentItem == null) return;

            currentSlotToBind[playerID] = currentSlot;
            currentItemIdToBind[playerID] = currentItem.Name; // Use the item's name as the identifier

            isBindingMode[playerID] = true;

            // Clear held buttons to prevent immediate binding
            if (playerHeldButtons.ContainsKey(playerID))
                playerHeldButtons[playerID].Clear();

            Game1.activeClickableMenu = new DialogueBoxWithCustomIcon(
                $"Press a button to bind to item '{currentItem.DisplayName}' for player {player.Name}",
                new Rectangle(128, 256, 64, 64)
            );
        }

        private void SelectInventoryItem(Farmer player, string itemId)
        {
            if (player == null || player.Items == null || string.IsNullOrEmpty(itemId))
                return;

            long playerID = player.UniqueMultiplayerID;

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
                    $"Item '{itemId}' not found in {player.Name}'s inventory. Removing keybind.",
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

            if (playerHeldButtons.ContainsKey(playerID))
                playerHeldButtons[playerID].Clear();
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

