using StardewModdingAPI;
using StardewValley;
using StardewModdingAPI.Events;
using System.Collections.Generic;
using StardewValley.Menus;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace QuickSelect
{
    public class ItemBinding
    {
        public string ItemName { get; set; } = "";
    }

    public class ModEntry : Mod
    {
        private ModConfig Config = null!;
        private Dictionary<long, Dictionary<string, ItemBinding>> playerSlotBindings = new();

        private bool isBindingMode = false;
        private string? currentItemNameToBind;
        private Item? currentItemToBind;
        private long currentPlayerID;
        private List<SButton> heldButtons = new();

        private int bindHoldStartTick = -1;
        private int bindCountdown = 0;
        private string? bindCountdownTarget;
        private int bindCooldown = 0;

        // dont fire til all buttons released
        private ItemBinding? pendingBinding = null;
        private Farmer? pendingPlayer = null;
        private List<SButton> pendingComboButtons = new();

        private bool suppressNextUse = false;
        private int suppressUseTimeout = 0;

        private enum SwapBackState { None, WaitingToEat, WaitingForEatFinish, SwapBack }
        private SwapBackState swapBackState = SwapBackState.None;
        private Item? previousItem = null;
        private Item? lastKnownItem = null;
        private int swapBackTimer = 0;
        private bool swapBackItemIsFood = false;
        private int stateWatchdog = 0;

        private static readonly HashSet<string> BuiltInConsumableIds = new()
        {
            "286", "287", "288", // bombs
            "749",  // stairs
            "891",  // qi seasoning
        };

        private Texture2D? buttonSheet;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            try { buttonSheet = helper.ModContent.Load<Texture2D>("assets/buttons.png"); }
            catch { buttonSheet = null; }

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Input.ButtonReleased += OnButtonReleased;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedHud += OnRenderedHud;
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            playerSlotBindings.Clear();
            Farmer? player = GetCurrentPlayer();
            if (player == null) return;

            var bindings = new Dictionary<string, ItemBinding>();
            foreach (var kv in Config.Bindings)
                bindings[kv.Key] = new ItemBinding { ItemName = kv.Value };

            playerSlotBindings[player.UniqueMultiplayerID] = bindings;
        }

        private void OnSaving(object? sender, SavingEventArgs e) => SaveBindings();

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            configMenu.AddSectionTitle(mod: ModManifest, text: () => "Binding Controls");

            configMenu.AddKeybind(
                mod: ModManifest,
                getValue: () => Config.BindModeKey,
                setValue: v => Config.BindModeKey = v,
                name: () => "Bind Mode Key (Keyboard)",
                tooltip: () => "Press this while holding an item to bind it"
            );

            configMenu.AddKeybind(
                mod: ModManifest,
                getValue: () => Config.BindModeButton,
                setValue: v => Config.BindModeButton = v,
                name: () => "Bind Mode Button (Controller)",
                tooltip: () => "Hold this while holding an item to bind it"
            );

            configMenu.AddKeybind(
                mod: ModManifest,
                getValue: () => Config.ClearAllBindingsKey,
                setValue: v => Config.ClearAllBindingsKey = v,
                name: () => "Clear All Bindings Key",
                tooltip: () => "Hold and release to wipe all keybinds"
            );

            configMenu.AddSectionTitle(mod: ModManifest, text: () => "Auto-Use");

            configMenu.AddBoolOption(
                mod: ModManifest,
                getValue: () => Config.AutoUseAndSwapBack,
                setValue: v => Config.AutoUseAndSwapBack = v,
                name: () => "Auto-Use & Swap Back",
                tooltip: () => "ON: food/bombs/stairs auto-use and swap back. OFF: just equips."
            );

            configMenu.AddSectionTitle(mod: ModManifest, text: () => "Display");

            configMenu.AddTextOption(
                mod: ModManifest,
                getValue: () => Config.ControllerStyle,
                setValue: v => Config.ControllerStyle = v,
                name: () => "Controller Style",
                tooltip: () => "Sony or Xbox button labels",
                allowedValues: new[] { "Sony", "Xbox" }
            );

            configMenu.AddSectionTitle(mod: ModManifest, text: () => "Advanced");

            configMenu.AddParagraph(
                mod: ModManifest,
                text: () => "Modifier keys, directional buttons, disabled buttons, and extra consumable IDs can be edited in config.json."
            );
        }

        private string GetButtonKey(IEnumerable<SButton> buttons)
            => string.Join("+", buttons.OrderBy(b => b).Select(b => b.ToString()));

        private bool IsDirectionalButton(SButton button) => Config.DirectionalButtons.Contains(button);

        private bool IsModifierHeld()
            => Config.ModifierKeys.Any(heldButtons.Contains) || Config.ModifierButtons.Any(heldButtons.Contains);

        // dont suppress A — its used for menus n shit
        private bool IsUseButton(SButton button)
            => button == SButton.MouseLeft || button == SButton.ControllerX;

        private Farmer? GetCurrentPlayer()
            => Game1.getAllFarmers().FirstOrDefault(f => f.IsLocalPlayer);

        private bool IsConsumable(Item? item)
        {
            if (item is not StardewValley.Object obj) return false;
            if (obj.Edibility > 0) return true;
            if (BuiltInConsumableIds.Contains(obj.ItemId)) return true;
            if (Config.ExtraConsumableIds.Contains(obj.ItemId)) return true;
            return false;
        }

        private bool IsEdible(Item? item)
            => item is StardewValley.Object obj && obj.Edibility > 0;

        private void ShowHUD(string message)
            => Game1.addHUDMessage(new HUDMessage(message) { noIcon = true });

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            Farmer? currentPlayer = GetCurrentPlayer();
            if (currentPlayer == null) return;

            // grab current item before R1 shifts the toolbar
            if (heldButtons.Count == 0 && currentPlayer.CurrentItem != null)
                lastKnownItem = currentPlayer.CurrentItem;

            if (!playerSlotBindings.ContainsKey(currentPlayer.UniqueMultiplayerID))
                playerSlotBindings[currentPlayer.UniqueMultiplayerID] = new Dictionary<string, ItemBinding>();

            if (suppressNextUse && IsUseButton(e.Button))
            {
                Helper.Input.Suppress(e.Button);
                return;
            }

            if (bindCountdown > 0)
            {
                Helper.Input.Suppress(e.Button);
                return;
            }

            if (isBindingMode && e.Button == Config.BindModeButton)
            {
                Helper.Input.Suppress(e.Button);
                return;
            }

            heldButtons.Add(e.Button);

            if (!Context.IsWorldReady) return;

            if (e.Button == Config.BindModeKey)
            {
                Helper.Input.Suppress(e.Button);
                heldButtons.Clear();
                StartBindMode(currentPlayer);
                return;
            }

            if (e.Button == Config.BindModeButton)
            {
                bindHoldStartTick = (int)Game1.ticks;
                heldButtons.Remove(e.Button);
                return;
            }

            if (bindCooldown > 0 || swapBackState != SwapBackState.None)
                return;

            var comboButtons = heldButtons.Where(b => !IsDirectionalButton(b)).ToList();
            if (comboButtons.Count == 0) return;
            if (comboButtons.Count == 1 && Config.DisabledSingleButtons.Contains(comboButtons[0])) return;

            string buttonKey = GetButtonKey(comboButtons);
            var bindings = playerSlotBindings[currentPlayer.UniqueMultiplayerID];

            if (bindings.TryGetValue(buttonKey, out var binding))
            {
                foreach (var btn in comboButtons)
                    Helper.Input.Suppress(btn);

                // if circle opened inventory before r1 came in, kill it
                if (Game1.activeClickableMenu != null)
                {
                    try { Game1.activeClickableMenu.exitThisMenu(playSound: false); } catch { }
                    Game1.activeClickableMenu = null;
                }

                pendingBinding = binding;
                pendingPlayer = currentPlayer;
                pendingComboButtons = new List<SButton>(comboButtons);
                heldButtons.Clear();
                return;
            }

            if (Game1.activeClickableMenu != null)
                return;

            if (!IsModifierHeld())
                heldButtons.Clear();
        }

        private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
        {
            // fires once everything is released
            if (pendingBinding != null)
            {
                pendingComboButtons.Remove(e.Button);
                if (pendingComboButtons.Count == 0 && pendingPlayer != null)
                {
                    var binding = pendingBinding;
                    var player = pendingPlayer;
                    pendingBinding = null;
                    pendingPlayer = null;
                    HandleBindingTriggered(player, binding);
                }
                return;
            }

            if (!Context.IsWorldReady)
            {
                heldButtons.Clear();
                bindHoldStartTick = -1;
                return;
            }

            if (e.Button == Config.BindModeButton && bindHoldStartTick >= 0)
            {
                bindHoldStartTick = -1;
                return;
            }

            if (bindCountdown > 0) return;

            Farmer? currentPlayer = GetCurrentPlayer();
            if (currentPlayer == null) return;

            if (heldButtons.Contains(Config.ClearAllBindingsKey))
            {
                if (playerSlotBindings.TryGetValue(currentPlayer.UniqueMultiplayerID, out var clearBindings))
                {
                    clearBindings.Clear();
                    SaveBindings();
                }
                heldButtons.Clear();
                return;
            }

            if (e.Button == Config.BindModeKey || e.Button == Config.BindModeButton)
                return;

            if (isBindingMode)
            {
                FinalizeBind();
                Game1.activeClickableMenu = null;
                Game1.player.completelyStopAnimatingOrDoingAction();
                suppressNextUse = false;
                swapBackState = SwapBackState.None;
                isBindingMode = false;
                heldButtons.Clear();
                bindCooldown = 30;
            }
            else
            {
                heldButtons.Remove(e.Button);
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            // watchdog, never let swap back get stuck
            if (swapBackState != SwapBackState.None || suppressNextUse)
            {
                stateWatchdog++;
                if (stateWatchdog > 180)
                {
                    swapBackState = SwapBackState.None;
                    suppressNextUse = false;
                    suppressUseTimeout = 0;
                    previousItem = null;
                    if (Context.IsWorldReady)
                    {
                        Game1.player.completelyStopAnimatingOrDoingAction();
                        Game1.player.forceCanMove();
                    }
                    stateWatchdog = 0;
                }
            }
            else
            {
                stateWatchdog = 0;
            }

            // if you somehow got frozen and theres no reason for it, unfreeze
            if (Context.IsWorldReady && !Game1.player.CanMove && swapBackState == SwapBackState.None
                && Game1.activeClickableMenu == null && !Game1.player.UsingTool && !Game1.player.isEating
                && !Game1.eventUp && !Game1.fadeToBlack)
            {
                Game1.player.completelyStopAnimatingOrDoingAction();
                Game1.player.forceCanMove();
                Game1.player.CanMove = true;
            }

            if (bindHoldStartTick >= 0 && Context.IsWorldReady)
            {
                int holdTicks = (int)(Config.BindHoldMs / 1000f * 60f);
                if ((int)Game1.ticks - bindHoldStartTick >= holdTicks)
                {
                    bindHoldStartTick = -1;
                    Farmer? player = GetCurrentPlayer();
                    if (player != null)
                    {
                        if (Game1.activeClickableMenu != null)
                            Game1.activeClickableMenu.exitThisMenu();
                        StartBindMode(player);
                    }
                }
            }

            if (bindCooldown > 0)
                bindCooldown--;

            if (bindCountdown > 0)
            {
                bindCountdown--;
                if (bindCountdown == 0)
                {
                    isBindingMode = true;
                    heldButtons.Clear();
                    Game1.activeClickableMenu = new DialogueBoxWithItemIcon(
                        $"Now press a button/combo to bind to {bindCountdownTarget}",
                        currentItemToBind
                    );
                }
                return;
            }

            if (suppressNextUse)
            {
                suppressUseTimeout--;
                if (suppressUseTimeout <= 0)
                    suppressNextUse = false;
            }

            switch (swapBackState)
            {
                case SwapBackState.WaitingToEat:
                    swapBackTimer--;
                    if (swapBackTimer <= 0)
                    {
                        Game1.player.eatHeldObject();
                        swapBackState = SwapBackState.WaitingForEatFinish;
                        swapBackTimer = 180; // hard cap, 3 sec
                    }
                    break;

                case SwapBackState.WaitingForEatFinish:
                    swapBackTimer--;
                    if (!Game1.player.isEating || swapBackTimer <= 0)
                    {
                        // force unstick if eating never finished
                        if (Game1.player.isEating)
                        {
                            try { Game1.player.doneEating(); } catch { }
                        }
                        Game1.player.completelyStopAnimatingOrDoingAction();
                        Game1.player.forceCanMove();
                        swapBackState = SwapBackState.SwapBack;
                        swapBackTimer = 2;
                    }
                    break;

                case SwapBackState.SwapBack:
                    swapBackTimer--;
                    if (swapBackTimer <= 0)
                    {
                        SwapBackToPreviousItem();
                        Game1.player.forceCanMove();
                        swapBackState = SwapBackState.None;
                    }
                    break;
            }
        }

        private void StartBindMode(Farmer player)
        {
            var currentItem = player.CurrentItem;
            if (currentItem == null) return;

            currentItemNameToBind = currentItem.Name;
            currentItemToBind = currentItem;
            currentPlayerID = player.UniqueMultiplayerID;

            bindCountdownTarget = $"'{currentItem.DisplayName}'";
            bindCountdown = 12;
        }

        private void FinalizeBind()
        {
            if (!isBindingMode) return;

            var validButtons = heldButtons.Where(b => !IsDirectionalButton(b)).ToList();
            if (validButtons.Count == 0) return;
            if (validButtons.Count == 1 && Config.DisabledSingleButtons.Contains(validButtons[0])) return;

            if (!playerSlotBindings.TryGetValue(currentPlayerID, out var bindings))
            {
                bindings = new Dictionary<string, ItemBinding>();
                playerSlotBindings[currentPlayerID] = bindings;
            }

            string buttonKey = GetButtonKey(validButtons);
            string itemName = currentItemNameToBind!;

            // no dupes
            if (bindings.ContainsKey(buttonKey))
                bindings.Remove(buttonKey);

            var existingKey = bindings.FirstOrDefault(kv => kv.Value.ItemName == itemName).Key;
            if (existingKey != null)
                bindings.Remove(existingKey);

            bindings[buttonKey] = new ItemBinding { ItemName = itemName };
            SaveBindings();

            isBindingMode = false;
            currentItemNameToBind = null;
            currentItemToBind = null;
            heldButtons.Clear();
        }

        private void HandleBindingTriggered(Farmer player, ItemBinding binding)
        {
            Item? prevItem = lastKnownItem;

            bool selected = SelectByItemName(player, binding.ItemName);
            if (!selected) return;

            if (Config.AutoUseAndSwapBack && IsConsumable(player.CurrentItem))
            {
                previousItem = prevItem;
                swapBackItemIsFood = IsEdible(player.CurrentItem);

                if (swapBackItemIsFood)
                {
                    // gotta wait a tick, calling eat during the press freezes shit
                    swapBackState = SwapBackState.WaitingToEat;
                    swapBackTimer = 3;
                }
                else if (player.CurrentItem is StardewValley.Object obj)
                {
                    // drop bomb/stairs right at our feet
                    DirectPlaceItem(obj);
                    swapBackState = SwapBackState.SwapBack;
                    swapBackTimer = 5;
                }
                suppressNextUse = false;
            }
            else
            {
                suppressNextUse = true;
                suppressUseTimeout = 10;
            }
        }

        private void DirectPlaceItem(StardewValley.Object obj)
        {
            try
            {
                int tileX = (int)Game1.player.Tile.X;
                int tileY = (int)Game1.player.Tile.Y;
                if (obj.placementAction(Game1.currentLocation, tileX * 64, tileY * 64, Game1.player))
                {
                    obj.Stack--;
                    if (obj.Stack <= 0)
                        Game1.player.removeItemFromInventory(obj);
                }
            }
            catch { }
        }

        private bool SelectByItemName(Farmer player, string itemName)
        {
            int idx = -1;
            for (int i = 0; i < player.Items.Count; i++)
            {
                if (player.Items[i]?.Name == itemName)
                {
                    idx = i;
                    break;
                }
            }

            if (idx == -1)
            {
                ShowHUD($"'{itemName}' not in inventory — removing keybind.");
                RemoveBindingByItemName(player, itemName);
                return false;
            }

            int shifts = idx / 12;
            int slot = idx % 12;
            for (int i = 0; i < shifts; i++)
                player.shiftToolbar(true);
            player.CurrentToolIndex = slot;
            heldButtons.Clear();
            return true;
        }

        private void SwapBackToPreviousItem()
        {
            Farmer player = Game1.player;
            if (previousItem == null) return;

            for (int i = 0; i < player.Items.Count; i++)
            {
                if (player.Items[i] == previousItem)
                {
                    int shifts = i / 12;
                    int slot = i % 12;
                    for (int j = 0; j < shifts; j++)
                        player.shiftToolbar(true);
                    player.CurrentToolIndex = slot;
                    previousItem = null;
                    return;
                }
            }
            previousItem = null;
        }

        private void SaveBindings()
        {
            Config.Bindings.Clear();
            foreach (var playerBinds in playerSlotBindings.Values)
                foreach (var kv in playerBinds)
                    Config.Bindings[kv.Key] = kv.Value.ItemName;
            Helper.WriteConfig(Config);
        }

        private void RemoveBindingByItemName(Farmer player, string itemName)
        {
            if (!playerSlotBindings.TryGetValue(player.UniqueMultiplayerID, out var bindings))
                return;
            var key = bindings.FirstOrDefault(kv => kv.Value.ItemName == itemName).Key;
            if (key != null)
                bindings.Remove(key);
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null || Game1.eventUp)
                return;

            Farmer? player = GetCurrentPlayer();
            if (player == null) return;

            if (!playerSlotBindings.TryGetValue(player.UniqueMultiplayerID, out var binds) || binds.Count == 0)
                return;

            Color outlineColor = new Color(127, 255, 220);
            var boundInfo = new Dictionary<string, (Color color, string combo)>();
            foreach (var kv in binds)
            {
                if (!boundInfo.ContainsKey(kv.Value.ItemName))
                    boundInfo[kv.Value.ItemName] = (outlineColor, ShortenCombo(kv.Key));
            }

            Toolbar? toolbar = Game1.onScreenMenus.OfType<Toolbar>().FirstOrDefault();
            if (toolbar == null) return;

            var b2 = e.SpriteBatch;
            for (int i = 0; i < toolbar.buttons.Count && i < player.Items.Count; i++)
            {
                var item = player.Items[i];
                if (item == null || !boundInfo.TryGetValue(item.Name, out var info))
                    continue;

                var bounds = toolbar.buttons[i].bounds;
                int thickness = 2;
                int x = bounds.X - 2;
                int y = bounds.Y - 2;
                int w = bounds.Width + 4;
                int h = bounds.Height + 4;
                b2.Draw(Game1.staminaRect, new Rectangle(x, y, w, thickness), info.color);
                b2.Draw(Game1.staminaRect, new Rectangle(x, y + h - thickness, w, thickness), info.color);
                b2.Draw(Game1.staminaRect, new Rectangle(x, y, thickness, h), info.color);
                b2.Draw(Game1.staminaRect, new Rectangle(x + w - thickness, y, thickness, h), info.color);

                DrawComboLabel(b2, info.combo, bounds, info.color);
            }
        }

        private void DrawComboLabel(SpriteBatch b, string combo, Rectangle slot, Color color)
        {
            var font = Game1.smallFont;
            float textScale = 0.6f;
            int iconSize = 24;
            int textHeight = (int)(font.MeasureString("Mg").Y * textScale);

            float totalWidth = 0;
            var segments = ParseSegments(combo);
            foreach (var seg in segments)
            {
                if (seg.shape != null)
                    totalWidth += iconSize;
                else
                    totalWidth += font.MeasureString(seg.text!).X * textScale;
            }

            int labelBottom = slot.Y + 6;
            int curX = slot.X + (slot.Width - (int)totalWidth) / 2;

            foreach (var seg in segments)
            {
                if (seg.shape != null)
                {
                    int sy = labelBottom - iconSize;
                    DrawShape(b, seg.shape, curX, sy, iconSize, color);
                    curX += iconSize;
                }
                else
                {
                    int ty = labelBottom - textHeight - (iconSize - textHeight) / 2;
                    var pos = new Vector2(curX, ty);
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dy = -2; dy <= 2; dy++)
                            if (dx != 0 || dy != 0)
                                b.DrawString(font, seg.text!, pos + new Vector2(dx, dy), Color.Black, 0f, Vector2.Zero, textScale, SpriteEffects.None, 1f);
                    b.DrawString(font, seg.text!, pos, Color.White, 0f, Vector2.Zero, textScale, SpriteEffects.None, 1f);
                    curX += (int)(font.MeasureString(seg.text!).X * textScale);
                }
            }
        }

        private struct Segment { public string? text; public string? shape; }

        private List<Segment> ParseSegments(string combo)
        {
            var result = new List<Segment>();
            var buf = new System.Text.StringBuilder();
            foreach (char ch in combo)
            {
                string? shape = ch switch
                {
                    '✕' => "X",
                    '○' => "O",
                    '□' => "S",
                    '△' => "T",
                    _ => null
                };

                if (shape != null)
                {
                    if (buf.Length > 0)
                    {
                        result.Add(new Segment { text = buf.ToString() });
                        buf.Clear();
                    }
                    result.Add(new Segment { shape = shape });
                }
                else
                {
                    buf.Append(ch);
                }
            }
            if (buf.Length > 0)
                result.Add(new Segment { text = buf.ToString() });
            return result;
        }

        private void DrawShape(SpriteBatch b, string shape, int x, int y, int size, Color color)
        {
            if (buttonSheet == null) return;

            bool sony = Config.ControllerStyle == "Sony";
            int row = sony ? 1 : 0;
            int col = shape switch
            {
                "X" => 0,
                "O" => 1,
                "S" => 2,
                "T" => 3,
                _ => 0
            };
            var src = new Rectangle(col * 32, row * 32, 32, 32);
            var dst = new Rectangle(x, y, size, size);
            b.Draw(buttonSheet, dst, src, Color.White);
        }

        private string ShortenCombo(string combo)
            => string.Join("+", combo.Split('+').Select(ShortenButton));

        private string ShortenButton(string name)
        {
            bool sony = Config.ControllerStyle == "Sony";
            return name switch
            {
                "LeftShoulder"   => sony ? "L1" : "LB",
                "RightShoulder"  => sony ? "R1" : "RB",
                "LeftTrigger"    => sony ? "L2" : "LT",
                "RightTrigger"   => sony ? "R2" : "RT",
                "LeftStick"      => sony ? "L3" : "LS",
                "RightStick"     => sony ? "R3" : "RS",
                "ControllerA"    => sony ? "✕" : "A",
                "ControllerB"    => sony ? "○" : "B",
                "ControllerX"    => sony ? "□" : "X",
                "ControllerY"    => sony ? "△" : "Y",
                "ControllerStart" => sony ? "Opt" : "Menu",
                "ControllerBack"  => sony ? "Shr" : "View",
                "DPadUp"    => "Up",
                "DPadDown"  => "Dn",
                "DPadLeft"  => "Lt",
                "DPadRight" => "Rt",
                "LeftControl"  => "Ctrl",
                "RightControl" => "Ctrl",
                "LeftShift"    => "Shift",
                "RightShift"   => "Shift",
                "LeftAlt"  => "Alt",
                "RightAlt" => "Alt",
                "OemTilde"         => "~",
                "OemComma"         => ",",
                "OemPeriod"        => ".",
                "OemQuestion"      => "/",
                "OemSemicolon"     => ";",
                "OemQuotes"        => "'",
                "OemOpenBrackets"  => "[",
                "OemCloseBrackets" => "]",
                "OemMinus"         => "-",
                "OemPlus"          => "+",
                "OemBackslash"     => "\\",
                "OemPipe"          => "|",
                _ => name
            };
        }
    }

    public class DialogueBoxWithItemIcon : DialogueBox
    {
        private readonly Item? displayItem;
        private float glowTimer = 0f;

        public DialogueBoxWithItemIcon(string dialogue, Item? item = null) : base(dialogue)
        {
            displayItem = item;
            characterIndexInDialogue = dialogue.Length;
        }

        public override void draw(SpriteBatch b)
        {
            base.draw(b);
            Vector2 pos = new Vector2(xPositionOnScreen + width - 100, yPositionOnScreen + height - 100);

            glowTimer += 0.05f;
            float pulse = 0.8f + 0.2f * (float)System.Math.Sin(glowTimer);

            IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                new Rectangle(128, 128, 64, 64),
                (int)pos.X, (int)pos.Y, 80, 80,
                Color.White * pulse, 1f, false);

            if (displayItem != null)
                displayItem.drawInMenu(b, pos + new Vector2(8, 8), 1f);
        }
    }
}
