using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using System.Drawing;
using T3MenuSharedApi;

namespace SLAYER_Conquest;

public partial class SLAYER_Conquest : BasePlugin, IPluginConfig<SLAYER_ConquestConfig>
{
    private void CTFSettingsMenu(CCSPlayerController player)
    {
        // Ensure player is valid and match is ongoing
        if (player == null || !player.IsValid || MatchStatus.Status != MatchStatusType.Ongoing) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        // Create menu
        var settingsMenu = manager.CreateMenu(Localizer["Menu.Settings.Title"], false, true, true, false);

        // Add option to create a new flag
        settingsMenu.AddOption($"{Localizer["Menu.CreateFlag.Title"]}", (p, option) =>
        {
            CreateFlagMenu(p, settingsMenu);
        });

        // Add option to edit an existing flag
        settingsMenu.AddOption($"{Localizer["Menu.EditFlag.Title"]}", (p, option) =>
        {
            CreateFlagEditMenu(p, settingsMenu);
        });

        // Add option to delete an existing flag
        settingsMenu.AddOption($"{Localizer["Menu.DeleteFlag.Title"]}", (p, option) =>
        {
            CreateFlagDeleteMenu(p, settingsMenu);
        });

        // Add option to change the deploy camera position
        settingsMenu.AddOption($"{Localizer["Menu.ChangeDeployCameraPosition"]}", (p, option) =>
        {
            p.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.DeployCameraPositionChanged", $"{DeployCameraPosition}", $"{p.PlayerPawn.Value!.AbsOrigin!}"]}");
            DeployCameraPosition = p.PlayerPawn.Value.AbsOrigin!; // Set the deploy camera position to the player's current position
            if (fileHandler != null) fileHandler.SaveFlagPositions();
        });

        settingsMenu.AddOption($"{Localizer["Menu.ChangeMatchEndCameraPosition"]}", (p, option) =>
        {
            var position = new Vector(p.PlayerPawn.Value!.AbsOrigin!.X, p.PlayerPawn.Value.AbsOrigin.Y, p.PlayerPawn.Value.AbsOrigin.Z + 64); // player eye position
            p.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.MatchEndCameraPositionChanged", $"({MatchEndCameraPosition.Item1} | {MatchEndCameraPosition.Item2})", $"({position} | {p.PlayerPawn.Value.AbsRotation!})"]}");
            MatchEndCameraPosition = (position, p.PlayerPawn.Value.AbsRotation!); // Set the match end camera position to the player's current position
            fileHandler.SaveFlagPositions();
        });

        // Open the menu for the player
        manager.OpenMainMenu(player, settingsMenu);
    }
    private void CreateFlagMenu(CCSPlayerController player, IT3Menu parentMenu, string flagName = "...", bool isEditMode = false)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        // Check if player is in a valid position
        if (player.Pawn.Value == null || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.MustBeAliveToCreateFlag"]}");
            manager.CloseMenu(player); // Close the confirmation menu
            CTFSettingsMenu(player); // Reopen the settings menu
            return;
        }
        // Create menu
        var createFlagMenu = manager.CreateMenu(Localizer["Menu.CreateFlag.Title"], false, true, true, true);

        if (!isEditMode)
        {
            createFlagMenu.AddInputOption($"{Localizer["Menu.EnterFlagName"]}", $"{flagName}", (p, option, input) =>
            {
                input = input.Trim();

                // Check if a flag already exists with this name
                if (FlagPositions.ContainsKey(input))
                {
                    player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.NameAlreadyExist"]}");
                    return;
                }

                // Handle flag name input
                flagName = input;
            }, $"{Localizer["Chat.Prefix"]} {ChatColors.Yellow}Enter Flag Name (no spaces):");
        }

        // Flag Position & Rotation option
        if (isEditMode)
        {
            var flag = Flagpoles?.FirstOrDefault(f => f.Name == flagName);
            if (flag != null)
            {
                // Separate option for changing position
                createFlagMenu.AddOption($"<font color='lime'>Change Flag Position</font>", (p, option) =>
                {
                    var position = GetPlayerAimPosition(player);
                    if (position == null) return;

                    flag.Model?[0].Teleport(position); // teleport flag pole
                    flag.Model?[1].Teleport(new Vector(position.X, position.Y, flag.Model?[1].AbsOrigin!.Z)); // teleport flag

                    // Update the flag object position
                    flag.Position = position;

                    // Update in FlagPositions dictionary directly
                    if (FlagPositions != null && FlagPositions.ContainsKey(flagName))
                    {
                        // Get the existing FlagData and update its position
                        var existingFlagData = FlagPositions[flagName];
                        existingFlagData.Position = ConvertVectorToString(position);

                        // Put the updated FlagData back in the dictionary
                        FlagPositions[flagName] = existingFlagData;
                    }

                    player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.FlagPositionUpdated", position]}");
                    fileHandler.SaveFlagPositions();
                    fileHandler.LoadFlagPositions();
                });

                createFlagMenu.AddOption($"<font color='cyan'>Change Flag Rotation</font>", (p, option) =>
                {
                    FlagSquareRotationMenu(p, createFlagMenu, "Flag", flag);
                });

                // Didn't feel the much use of the Square Rotation
                /*createFlagMenu.AddOption($"<font color='yellow'>Change Square Rotation</font>", (p, option) =>
                {
                    FlagSquareRotationMenu(p, createFlagMenu, "Square", flag);
                });*/

                createFlagMenu.AddOption($"<font color='orange'>Advanced Square Settings</font>", (p, option) =>
                {
                    AdvancedSquareSettingsMenu(p, createFlagMenu, flagName, isEditMode);
                });
            }
        }

        if (!isEditMode)
        {
            // Add option to create a new flag
            createFlagMenu.AddOption($"{Localizer["Menu.CreateFlag"]}", (p, option) =>
            {
                if (string.IsNullOrWhiteSpace(flagName) || flagName == "..." || flagName == " ")
                {
                    p.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.InvalidFlagName"]}");
                    return;
                }
                // Get position where player is aiming
                var position = GetPlayerAimPosition(player);
                if (position == null) return;

                // Create and Save the flag
                var flagsquare = new FlagData(ConvertVectorToString(position), 0, "", "", "", "");
                CreateFlag(flagName, flagsquare);
                FlagPositions.Add(flagName, flagsquare);
                fileHandler.SaveFlagPositions();
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.FlagPositionSet", flagName, position]}");
                fileHandler.LoadFlagPositions();

                // Close the menu and reopen settings
                manager.CloseMenu(p);
                CTFSettingsMenu(p); // Reopen the settings menu after creating the flag
            });
        }

        manager.OpenSubMenu(player, createFlagMenu);
    }
    private void FlagSquareRotationMenu(CCSPlayerController player, IT3Menu parentMenu, string Name, FlagStatus flag)
    {
        if (player == null || !player.IsValid || flag == null) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        var rotationMenu = manager.CreateMenu(Localizer["Menu.ChangeFlagRotation.Title", Name], false, true, true, true);
        rotationMenu.ParentMenu = parentMenu;
        var currentRotation = Name == "Flag" ? flag.Rotation : flag.CaptureSquare.Rotation;
        // Current rotation display
        var rotationOption = rotationMenu.AddOption($"<font color='yellow'>Current Rotation: {currentRotation:F0}°</font>", (p, option) => { });

        rotationMenu.AddOption($"<font color='aqua'>Rotate +1°</font>", (p, option) =>
        {
            float newRotation = (currentRotation + 1f) % 360f;
            if (Name == "Flag") UpdateFlagRotation(flag, newRotation);
            else UpdateSquareRotation(flag, newRotation);
            currentRotation = Name == "Flag" ? flag.Rotation : flag.CaptureSquare.Rotation;
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.FlagRotated", Name, $"{newRotation:F0}"]}");
            if (rotationOption != null) rotationOption.Value.OptionDisplay = $"<font color='yellow'>Current Rotation: {newRotation:F0}°</font>";
            manager.Refresh();
        });

        rotationMenu.AddOption($"<font color='orange'>Rotate -1°</font>", (p, option) =>
        {
            float newRotation = currentRotation - 1f;
            if (newRotation < 0) newRotation += 360f;
            if (Name == "Flag") UpdateFlagRotation(flag, newRotation);
            else UpdateSquareRotation(flag, newRotation);
            currentRotation = Name == "Flag" ? flag.Rotation : flag.CaptureSquare.Rotation;
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.FlagRotated", Name, $"{newRotation:F0}"]}");
            if (rotationOption != null) rotationOption.Value.OptionDisplay = $"<font color='yellow'>Current Rotation: {newRotation:F0}°</font>";
            manager.Refresh();
        });

        rotationMenu.AddOption($"<font color='lime'>Rotate +5°</font>", (p, option) =>
        {
            float newRotation = (currentRotation + 5f) % 360f;
            if (Name == "Flag") UpdateFlagRotation(flag, newRotation);
            else UpdateSquareRotation(flag, newRotation);
            currentRotation = Name == "Flag" ? flag.Rotation : flag.CaptureSquare.Rotation;
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.FlagRotated", Name, $"{newRotation:F0}"]}");
            if (rotationOption != null) rotationOption.Value.OptionDisplay = $"<font color='yellow'>Current Rotation: {newRotation:F0}°</font>";
            manager.Refresh();
        });

        rotationMenu.AddOption($"<font color='red'>Rotate -5°</font>", (p, option) =>
        {
            float newRotation = currentRotation - 5f;
            if (newRotation < 0) newRotation += 360f;
            if (Name == "Flag") UpdateFlagRotation(flag, newRotation);
            else UpdateSquareRotation(flag, newRotation);
            currentRotation = Name == "Flag" ? flag.Rotation : flag.CaptureSquare.Rotation;
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.FlagRotated", Name, $"{newRotation:F0}"]}");
            if (rotationOption != null) rotationOption.Value.OptionDisplay = $"<font color='yellow'>Current Rotation: {newRotation:F0}°</font>";
            manager.Refresh();
        });

        manager.OpenSubMenu(player, rotationMenu);
    }

    private void AdvancedSquareSettingsMenu(CCSPlayerController player, IT3Menu parentMenu, string flagName, bool isEditMode)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        var advancedMenu = manager.CreateMenu(Localizer["Menu.AdvancedSquareSettings.Title"], false, true, true, true);
        advancedMenu.ParentMenu = parentMenu;

        var flag = Flagpoles?.FirstOrDefault(f => f.Name == flagName);
        if (flag == null && !isEditMode) return;

        // Enhanced individual corner control
        advancedMenu.AddOption($"<font color='gold'>Individual Corner Control</font>", (p, option) =>
        {
            IndividualCornerMenu(p, advancedMenu, flagName, isEditMode);
        });

        advancedMenu.AddOption($"<font color='gold'>Sides Control</font>", (p, option) =>
        {
            SidesAdjustmentMenu(p, advancedMenu, flagName, isEditMode);
        });

        // Reset to perfect square option
        advancedMenu.AddOption($"<font color='RoyalBlue'>Reset to Perfect Square</font>", (p, option) =>
        {
            if (isEditMode && flag != null)
            {
                float averageSize = flag.CaptureSquare.GetAverageSize();
                UpdateFlagSquareSize(flag, averageSize);
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.SquareResetToShape", $"{averageSize:F0}"]}");
            }
        });

        manager.OpenSubMenu(player, advancedMenu);
    }
    private void IndividualCornerMenu(CCSPlayerController player, IT3Menu parentMenu, string flagName, bool isEditMode)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        var cornerMenu = manager.CreateMenu(Localizer["Menu.IndividualCornerControl.Title"], false, true, true, true);
        cornerMenu.ParentMenu = parentMenu;

        var flag = Flagpoles?.FirstOrDefault(f => f.Name == flagName);
        if (flag == null && !isEditMode) return;

        // Corner 1
        cornerMenu.AddOption($"<font color='cyan'>Corner 1: {flag!.CaptureSquare.Corner1.X:F0}, {flag.CaptureSquare.Corner1.Y:F0}</font>", (p, option) =>
        {
            CornerAdjustmentMenu(p, cornerMenu, flagName, 1, isEditMode);
        });

        // Corner 2
        cornerMenu.AddOption($"<font color='lime'>Corner 2: {flag.CaptureSquare.Corner2.X:F0}, {flag.CaptureSquare.Corner2.Y:F0}</font>", (p, option) =>
        {
            CornerAdjustmentMenu(p, cornerMenu, flagName, 2, isEditMode);
        });

        // Corner 3
        cornerMenu.AddOption($"<font color='orange'>Corner 3: {flag.CaptureSquare.Corner3.X:F0}, {flag.CaptureSquare.Corner3.Y:F0}</font>", (p, option) =>
        {
            CornerAdjustmentMenu(p, cornerMenu, flagName, 3, isEditMode);
        });

        // Corner 4
        cornerMenu.AddOption($"<font color='red'>Corner 4: {flag.CaptureSquare.Corner4.X:F0}, {flag.CaptureSquare.Corner4.Y:F0}</font>", (p, option) =>
        {
            CornerAdjustmentMenu(p, cornerMenu, flagName, 4, isEditMode);
        });

        manager.OpenSubMenu(player, cornerMenu);
    }
    private void SidesAdjustmentMenu(CCSPlayerController player, IT3Menu parentMenu, string flagName, bool isEditMode)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        var adjustMenu = manager.CreateMenu(Localizer["Menu.SidesControl.Title"], false, true, true, true);
        adjustMenu.ParentMenu = parentMenu;

        var flag = Flagpoles?.FirstOrDefault(f => f.Name == flagName);
        if (flag == null && !isEditMode) return;

        string[] directions = { "Expand Front Side", "Contract Front Side", "Expand Back Side", "Contract Back Side", "Expand Right Side", "Contract Right Side", "Expand Left Side", "Contract Left Side" };
        string[] colors = { "lime", "orange", "cyan", "RoyalBlue" };


        var selected_unit = 5;
        List<object> units = new List<object> { 1, 5, 10, 15 };
        List<LinkedListNode<IT3Option>> movementOptions = new List<LinkedListNode<IT3Option>>();
        adjustMenu.AddSliderOption($"<font color='yellow'>Adjustment Units: </font>", units, units[1], 4, (p, option, value) =>
        {
            foreach (var op in movementOptions.Where(o => o != null))
            {
                op.Value.OptionDisplay = op.Value.OptionDisplay!.Replace($"({selected_unit})", $"({units[value]})");
            }
            selected_unit = (int)units[value];
            manager.Refresh();
        });

        // Movement options
        int colorcounter = -1;
        for (int i = 0; i < directions.Length; i++)
        {
            string direction = directions[i];
            bool isExpand = direction.Contains("Expand");
            string side = direction.Contains("Front") ? "front" : direction.Contains("Back") ? "back" : direction.Contains("Left") ? "left" : direction.Contains("Right") ? "right" : "";
            if (isExpand) colorcounter++;

            movementOptions.Add(adjustMenu.AddOption($"<font color='{(isExpand ? colors[colorcounter] : "red")}'>{direction} ({selected_unit})</font>", (p, option) =>
            {
                AdjustSquareSide(flag!, side, isExpand ? -selected_unit : selected_unit);
                manager.Refresh();
            }));
        }

        manager.OpenSubMenu(player, adjustMenu);
    }
    /// <summary>
    /// Menu for adjusting individual corner positions
    /// </summary>
    private void CornerAdjustmentMenu(CCSPlayerController player, IT3Menu parentMenu, string flagName, int cornerNumber, bool isEditMode)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        var adjustMenu = manager.CreateMenu(Localizer["Menu.CornerAdjustment.Title", cornerNumber], false, true, true, true);
        adjustMenu.ParentMenu = parentMenu;

        var flag = Flagpoles?.FirstOrDefault(f => f.Name == flagName);
        if (flag == null && !isEditMode) return;

        Vector corner = GetCornerByNumber(flag!.CaptureSquare, cornerNumber);
        string[] directions = { "Move Forward (-Y)", "Move Backward (+Y)", "Move Right (-X)", "Move Left (+X)", "Move Up (+Z)", "Move Down (-Z)" };
        string[] colors = { "lime", "red", "orange", "cyan", "RoyalBlue", "silver" };

        // Current corner position display
        var positionOption = adjustMenu.AddOption($"<font color='yellow'>Position: {corner.X:F0}, {corner.Y:F0}, {corner.Z:F0}</font>", (p, option) => { });

        var selected_unit = 5;
        List<object> units = new List<object> { 1, 5, 10, 15 };
        List<LinkedListNode<IT3Option>> movementOptions = new List<LinkedListNode<IT3Option>>();
        adjustMenu.AddSliderOption($"<font color='yellow'>Adjustment Units: </font>", units, units[1], 4, (p, option, value) =>
        {
            foreach (var op in movementOptions.Where(o => o != null))
            {
                op.Value.OptionDisplay = op.Value.OptionDisplay!.Replace($"(±{selected_unit})", $"(±{units[value]})");
            }
            selected_unit = (int)units[value];
            manager.Refresh();
        });

        // Movement options
        for (int i = 0; i < directions.Length; i++)
        {
            string direction = directions[i];
            bool isPositive = direction.Contains("+");
            string axis = direction.Contains("X") ? "X" : direction.Contains("Y") ? "Y" : "Z";

            movementOptions.Add(adjustMenu.AddOption($"<font color='{colors[i]}'>{direction} (±{selected_unit})</font>", (p, option) =>
            {
                AdjustIndividualCorner(flag!, cornerNumber, axis, isPositive ? selected_unit : -selected_unit);
                if (positionOption != null) positionOption.Value.OptionDisplay = $"<font color='yellow'>Position: {corner.X:F0}, {corner.Y:F0}, {corner.Z:F0}</font>";
                manager.Refresh();
            }));
        }

        manager.OpenSubMenu(player, adjustMenu);
    }
    private void CreateFlagEditMenu(CCSPlayerController player, IT3Menu parentMenu)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        // Create menu
        var editMenu = manager.CreateMenu(Localizer["Menu.EditFlag.Title"], false, true, true, true);
        editMenu.ParentMenu = parentMenu;

        // Add each Flag to the menu
        foreach (var flag in FlagPositions.Keys)
        {
            editMenu.AddOption($"<font color='orange'>{flag}</font>", (p, option) =>
            {
                CreateFlagMenu(p, editMenu, flag, true);
            });
        }

        // Open the menu for the player
        manager.OpenSubMenu(player, editMenu);
    }
    private void CreateFlagDeleteMenu(CCSPlayerController player, IT3Menu parentMenu)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        // Create menu
        var deleteMenu = manager.CreateMenu(Localizer["Menu.DeleteFlag.Title"], false, true, true, true);
        deleteMenu.ParentMenu = parentMenu;

        // Add each Flag to the menu
        foreach (var flag in FlagPositions.Keys)
        {
            deleteMenu.AddOption($"<font color='orange'>{flag}</font>", (p, option) =>
            {
                ConfirmDeleteFlag(p, flag, deleteMenu);
            });
        }

        // Open the menu for the player
        manager.OpenSubMenu(player, deleteMenu);
    }

    private void ConfirmDeleteFlag(CCSPlayerController player, string flagName, IT3Menu parentMenu)
    {
        if (player == null || string.IsNullOrWhiteSpace(flagName)) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        // Confirmation menu
        var confirmMenu = manager.CreateMenu(Localizer["Menu.ConfirmDelete", flagName], false, true, true, true, false);
        confirmMenu.ParentMenu = parentMenu;

        // Add confirmation options
        confirmMenu.AddOption($"{Localizer["Menu.Confirm"]}", (p, option) =>
        {
            DeleteFlag(p, flagName);
            manager.CloseMenu(p); // Close the confirmation menu
            CTFSettingsMenu(p); // Reopen the settings menu
        });

        confirmMenu.AddOption($"{Localizer["Menu.Cancel"]}", (p, option) =>
        {
            manager.CloseMenu(p); // Close the confirmation menu
            CTFSettingsMenu(p); // Reopen the settings menu
        });

        // Open the confirmation menu
        manager.OpenSubMenu(player, confirmMenu);
    }

    private void OpenPlayerClassMenu(CCSPlayerController player, IT3Menu? parentMenu = null)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;
        // Create player class selection menu
        var classMenu = manager.CreateMenu(Localizer["Menu.SelectClass.Title"], false, false, true, false);
        if (parentMenu != null)
        {
            classMenu = manager.CreateMenu(Localizer["Menu.SelectClass.Title"], false, false, true, true, false);
            classMenu.ParentMenu = parentMenu;
        }

        // Add header with current class info
        var squad = GetPlayerSquad(player);
        PlayerClassType currentClass = PlayerClassType.Assault;
        if (squad != null && squad.Members.ContainsKey(player))
        {
            currentClass = squad.Members[player];
            classMenu.AddOption($"<font class='fontSize-m' color='yellow'>Current Class: </font><font class='fontSize-m' color='lime'>{Enum.GetName(currentClass)}</font>", (p, option) => { });
        }
        // Add each class as an option
        foreach (var classType in Enum.GetValues(typeof(PlayerClassType)).Cast<PlayerClassType>())
        {
            var config = _classConfigs[classType];
            string classDisplay = $"<font color='orange'>{config.Name}</font>";

            // Show if currently selected
            if (currentClass == classType)
            {
                classDisplay += " <font color='lime'>[SELECTED]</font>";
            }

            classMenu.AddOption(classDisplay, (p, option) =>
            {
                if (parentMenu == null) OpenPlayerClassSubMenu(p, classType, classMenu);
                else
                {
                    SelectPlayerClass(p, classType);
                    MenuManager!.OpenMainMenu(p, parentMenu); // Reopen the parent menu after selection
                }
            });
        }

        if (parentMenu == null) manager.OpenMainMenu(player, classMenu);
        else manager.OpenSubMenu(player, classMenu);
    }
    private void OpenPlayerClassSubMenu(CCSPlayerController player, PlayerClassType classType, IT3Menu? parentMenu)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        // Create simplified class submenu
        var classSubMenu = manager.CreateMenu(Localizer["Menu.ClassOptions.Title", _classConfigs[classType].Name], true, true, true, false);
        classSubMenu.ParentMenu = parentMenu;

        // Add select button
        classSubMenu.AddOption($"<font color='lime'>SELECT THIS CLASS</font>", (p2, opt2) =>
        {
            SelectPlayerClass(player, classType);
            if (parentMenu != null) MenuManager!.OpenMainMenu(player, parentMenu); // Reopen the parent menu after selection
            else MenuManager!.CloseMenu(player); // Close the menu if there is no parent
        });

        // Add option to print full class details
        classSubMenu.AddOption($"<font color='yellow'>Print Class Details</font>", (p2, opt2) =>
        {
            PrintClassDetailsToChat(p2, classType);
        });

        // Add option to select primary weapon
        classSubMenu.AddOption($"<font color='orange'>Select Primary Weapon</font>", (p2, opt2) =>
        {
            OpenPrimaryWeaponMenu(p2, classType, manager, classSubMenu);
        });

        // Add option to select secondary weapon
        classSubMenu.AddOption($"<font color='orange'>Select Secondary Weapon</font>", (p2, opt2) =>
        {
            OpenSecondaryWeaponMenu(p2, classType, manager, classSubMenu);
        });

        // Add option to select equipment
        classSubMenu.AddOption($"<font color='orange'>Select Equipment</font>", (p2, opt2) =>
        {
            OpenEquipmentMenu(p2, classType, manager, classSubMenu);
        });

        // Open the submenu
        manager.OpenSubMenu(player, classSubMenu);
    }
    // Open menu to select primary weapon
    private void OpenPrimaryWeaponMenu(CCSPlayerController player, PlayerClassType classType, IT3MenuManager manager, IT3Menu parentMenu)
    {
        if (player == null || !player.IsValid) return;

        var config = _classConfigs[classType];
        var primaryMenu = manager.CreateMenu(Localizer["Menu.SelectPrimaryWeapon.Title"], true, true, true, false);
        primaryMenu.ParentMenu = parentMenu;

        foreach (var weapon in config.PrimaryWeapons)
        {
            string weaponName = weapon.Replace("weapon_", "");
            // Capitalize first letter
            weaponName = char.ToUpper(weaponName[0]) + weaponName.Substring(1);
            string displayName = $"<font color='orange'>{weaponName}</font>";

            // Mark if this is the currently selected weapon
            if (PlayerStatuses.TryGetValue(player, out var status) &&
                status.SelectedWeapons.PrimaryWeapon == weapon &&
                PlayerStatuses.TryGetValue(player, out var playerStatus) &&
                playerStatus.ClassType == classType)
            {
                displayName += " <font color='lime'>[SELECTED]</font>";
            }

            primaryMenu.AddOption($"{displayName}", (p, option) =>
            {
                PlayerStatuses[player].SelectedWeapons.PrimaryWeapon = weapon;
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.WeaponSelected", weaponName, Localizer["Weapon.Primary"]]}");
                // Refresh the menu
                OpenPrimaryWeaponMenu(p, classType, manager, parentMenu);
                MenuManager!.Refresh();
            });
        }

        // Add confirm button
        primaryMenu.AddOption($"{Localizer["Menu.ConfirmSelection"]}", (p, option) =>
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.WeaponSelectionSaved", Localizer["Weapon.Primary"]]}");
            manager.OpenSubMenu(player, parentMenu);
        });

        manager.OpenSubMenu(player, primaryMenu);
    }

    // Open menu to select secondary weapon
    private void OpenSecondaryWeaponMenu(CCSPlayerController player, PlayerClassType classType, IT3MenuManager manager, IT3Menu parentMenu)
    {
        if (player == null || !player.IsValid) return;

        var config = _classConfigs[classType];
        var secondaryMenu = manager.CreateMenu(Localizer["Menu.SelectSecondaryWeapon.Title"], true, true, true, false);
        secondaryMenu.ParentMenu = parentMenu;

        foreach (var weapon in config.SecondaryWeapons)
        {
            string weaponName = weapon.Replace("weapon_", "");
            // Capitalize first letter
            weaponName = char.ToUpper(weaponName[0]) + weaponName.Substring(1);
            string displayName = $"<font color='orange'>{weaponName}</font>";

            // Mark if this is the currently selected weapon
            if (PlayerStatuses.TryGetValue(player, out var status) &&
                status.SelectedWeapons.SecondaryWeapon == weapon &&
                PlayerStatuses.TryGetValue(player, out var playerStatus) &&
                playerStatus.ClassType == classType)
            {
                displayName += " <font color='lime'>[SELECTED]</font>";
            }

            secondaryMenu.AddOption($"<font color='lime'>{displayName}</font>", (p, option) =>
            {
                PlayerStatuses[player].SelectedWeapons.SecondaryWeapon = weapon;
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.WeaponSelected", weaponName, Localizer["Weapon.Secondary"]]}");
                // Refresh the menu
                OpenSecondaryWeaponMenu(p, classType, manager, parentMenu);
                MenuManager!.Refresh();
            });
        }

        // Add confirm button
        secondaryMenu.AddOption($"{Localizer["Menu.ConfirmSelection"]}", (p, option) =>
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.WeaponSelectionSaved", Localizer["Weapon.Secondary"]]}");
            manager.OpenSubMenu(player, parentMenu);
        });

        manager.OpenSubMenu(player, secondaryMenu);
    }

    // Open menu to select equipment
    private void OpenEquipmentMenu(CCSPlayerController player, PlayerClassType classType, IT3MenuManager manager, IT3Menu parentMenu)
    {
        if (player == null || !player.IsValid) return;

        var config = _classConfigs[classType];
        var equipmentMenu = manager.CreateMenu(Localizer["Menu.SelectEquipment.Title"], true, true, true, false);
        equipmentMenu.ParentMenu = parentMenu;

        // Get player's current equipment selection or create empty list
        if (!PlayerStatuses.TryGetValue(player, out var status))
        {
            status = new PlayerStatus();
            status.SelectedWeapons = new PlayerSelectedWeapons();
            PlayerStatuses[player] = status;
        }

        // Display current equipment selections
        equipmentMenu.AddOption($"<font color='yellow'>Current Equipment:</font> " +
            (status.SelectedWeapons.Equipment.Count > 0 ? string.Join(", ", status.SelectedWeapons.Equipment.Select(e => e.Replace("weapon_", ""))) : "None"),
            (p, option) => { });

        // Add equipment options
        foreach (var item in config.Equipment)
        {
            string itemName = item.Replace("weapon_", "");
            // Capitalize first letter
            itemName = char.ToUpper(itemName[0]) + itemName.Substring(1);
            string displayName = itemName;

            // Mark if this item is selected
            bool isSelected = status.SelectedWeapons.Equipment.Contains(item) &&
                            PlayerStatuses.TryGetValue(player, out var playerStatus) &&
                            playerStatus.ClassType == classType;

            string color = isSelected ? "lime" : "orange";
            displayName = isSelected ? $"{displayName} [SELECTED]" : displayName;

            equipmentMenu.AddOption($"<font color='{color}'>{displayName}</font>", (p, option) =>
            {
                // Toggle selection
                if (isSelected)
                {
                    status.SelectedWeapons.Equipment.Remove(item);
                    player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.EquipmentRemoved", itemName]}");
                }
                else
                {
                    // Check if we've reached the limit for this type of equipment
                    int grenadeCount = status.SelectedWeapons.Equipment.Count(e => e.Contains("grenade") || e.Contains("flash") || e.Contains("smoke") || e.Contains("molotov") || e.Contains("decoy"));
                    if ((item.Contains("grenade") || item.Contains("flash") || item.Contains("smoke") || item.Contains("molotov") || item.Contains("decoy")) && grenadeCount >= 4)
                    {
                        player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.MaxGrenadesReached"]}");
                    }
                    else
                    {
                        status.SelectedWeapons.Equipment.Add(item);
                        player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.EquipmentAdded", itemName]}");
                    }
                }

                // Refresh the menu
                OpenEquipmentMenu(p, classType, manager, parentMenu);
                MenuManager!.Refresh();
            });
        }

        // Add confirm button
        equipmentMenu.AddOption($"{Localizer["Menu.ConfirmSelection"]}", (p, option) =>
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.EquipmentSelectionSaved"]}");
            manager.OpenSubMenu(player, parentMenu);
        });

        manager.OpenSubMenu(player, equipmentMenu);
    }
    private void GetReviveOrRespawnMenu(CCSPlayerController player)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        var menu = manager.CreateMenu(Localizer["Menu.ReviveOrRespawn.Title"], false, false, true, false, false);

        menu.MaxOptionLenght = 50; // Set max option length to 50 characters

        if (DeadPlayersTimer.ContainsKey(player))
        {
            if (DeadPlayersTimer[player].Item1 != null) DeadPlayersTimer[player].Item1.Kill(); // Kill the existing timer if it exists
            DeadPlayersTimer.Remove(player); // Remove the player from the dead players timer list
        }
        var reviveCounterOption = new LinkedListNode<IT3Option>(null!); // Initialize the revive counter option
        var medicsOption = new LinkedListNode<IT3Option>(null!); // Initialize the medics option

        var medicsPlayers = FindNearbyMedicsOrSquadmates(player).Take(5).ToList(); // Find nearby medics or squadmates
        List<object> medics = new List<object>();
        foreach (var medic in medicsPlayers)
        {
            medics.Add($"<font color='{(IsPlayerSquadmate(player, medic) ? "lime" : "aqua")}'>{PlayerStatuses[medic].DefaultName}</font>");
        }
        // Create a new timer for the player
        DeadPlayersTimer[player] = (AddTimer(1.0f, () =>
        {
            if (DeadPlayersTimer.ContainsKey(player) && player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !IsPlayerGettingRevived(player) && DeadPlayersTimer[player].Item2 > 0)
            {
                var timerTuple = DeadPlayersTimer[player];
                timerTuple.Item2 -= 1; // Decrease the timer by 1 second
                DeadPlayersTimer[player] = timerTuple;
                if (reviveCounterOption != null) reviveCounterOption.Value!.OptionDisplay = $"<font color='lime'>Request Revive:</font> <font color='red'>{DeadPlayersTimer[player].Item2}s</font>";
            }
            else if (DeadPlayersTimer.ContainsKey(player!) && player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !IsPlayerGettingRevived(player) && DeadPlayersTimer[player].Item2 <= 0)
            {
                if (DeadPlayersTimer[player].Item1 != null) DeadPlayersTimer[player].Item1.Kill();
                DeadPlayersTimer.Remove(player);
                manager.CloseMenu(player);
                DeploySettingsMenu(player);
            }
            else if (DeadPlayersTimer.ContainsKey(player!) && player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && IsPlayerGettingRevived(player))
            {
                var reviveEntry = GetPlayerReviveEntry(player);
                if (reviveCounterOption != null) reviveCounterOption.Value!.OptionDisplay = $"<font color='lime'>Getting Revived:</font> <font color='red'>{GenerateLoadingText(Server.CurrentTime - reviveEntry!.reviveTime, reviveEntry.reviveDuration)}s</font>";
            }

            if (DeadPlayersTimer.ContainsKey(player!) && player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected)
            {
                medicsPlayers = FindNearbyMedicsOrSquadmates(player).Take(5).ToList(); // Find nearby medics or squadmates
                List<object> medics = new List<object>();
                foreach (var medic in medicsPlayers)
                {
                    medics.Add($"<font color='{(IsPlayerSquadmate(player, medic) ? "lime" : "aqua")}'>{PlayerStatuses[medic].DefaultName}</font>");
                }
                if (medicsOption != null)
                {
                    medicsOption.Value!.OptionDisplay = $"<font color='gold'>Nearby Medics:</font>";
                    medicsOption.Value!.CustomValues = medics;
                }
            }

            manager.Refresh();

        }, TimerFlags.REPEAT), Config.PlayerGetRevivedTimer > 0 ? Config.PlayerGetRevivedTimer : 30);

        medicsOption = menu.AddSliderOption($"<font color='gold'>Nearby Medics:</font>", medics, null, 5, (p, option, value) =>
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.MedicDistance", PlayerStatuses[medicsPlayers.ElementAt(value)].DefaultName, (int)(CalculateDistanceBetween(medicsPlayers.ElementAt(value).PlayerPawn.Value!.AbsOrigin!, player.PlayerPawn.Value!.AbsOrigin!) / 39.37f)]}");
        });
        reviveCounterOption = menu.AddOption($"<font color='gold'>Request Revive:</font> <font color='red'>{DeadPlayersTimer[player].Item2}s</font>", (p, option) =>
        {
            RequestRevive(player);
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.ReviveRequested"]}"); // Notify the player that their request has been sent

        });
        menu.AddOption($"<font color='gold'>Skip Revive</font>", (p, option) =>
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.ReviveSkipped"]}");
            if (DeadPlayersTimer.ContainsKey(player))
            {
                if (DeadPlayersTimer[player].Item1 != null) DeadPlayersTimer[player].Item1.Kill(); // Kill the existing timer if it exists
                DeadPlayersTimer.Remove(player); // Remove the player from the dead players timer list
            }
            manager.CloseMenu(p);
            DeploySettingsMenu(p);
        });

        manager.OpenMainMenu(player, menu);
        manager.Refresh(1.0f); // Refresh the menu every second
    }
    private void DeploySettingsMenu(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.PlayerPawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE) return;

        if (PlayerStatuses.ContainsKey(player)) PlayerStatuses[player].Status = PlayerStatusType.Dead;
        RemoveGlowOnPlayerWhoRequestMedic(player); // Remove the glow effect on the player who requested revive
        RemoveAllGlowOfPlayer(player);
        RemovePlayerReviveEntry(player);

        PlayersRedeployTimer[player] = (AddTimer(1f, () =>
        {
            var timerTuple = PlayersRedeployTimer[player];
            timerTuple.Item2 -= 1; // Decrease the timer by 1 second
            PlayersRedeployTimer[player] = timerTuple;
            if (PlayersRedeployTimer[player].Item2 <= 0)
            {
                if (PlayersRedeployTimer[player].Item1 != null) PlayersRedeployTimer[player].Item1.Kill();
                PlayersRedeployTimer.Remove(player);
            }
        }, TimerFlags.REPEAT), Config.PlayerRedeployDelay);

        var manager = GetMenuManager();
        if (manager == null) return;

        var menu = manager.CreateMenu(Localizer["Menu.DeploySettings.Title"], false, false, true, false, false);

        menu.AddOption($"{Localizer["Menu.SelectDeployPosition"]}", (p, option) =>
        {
            manager.CloseMenu(p);
            ColorScreen(player, Color.Black, 0.2f, 0.3f, FadeFlags.FADE_OUT);
            SelectDeployPositionsMenu(p); // Open the deploy positions menu
        });

        menu.AddOption($"<font color='gold'>Change Loadout</font>", (p, option) =>
        {
            OpenPlayerClassSubMenu(p, PlayerStatuses[player].ClassType, menu);
        });

        menu.AddOption($"<font color='orange'>Change Player Class</font>", (p, option) =>
        {
            OpenPlayerClassMenu(player, menu);
        });

        manager.OpenMainMenu(player, menu);
    }
    private void SelectDeployPositionsMenu(CCSPlayerController player, QAngle? DeployCameraRotation = null)
    {
        if (player == null || !player.IsValid || player.PlayerPawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE) return;

        // Change player's camera position to a high position for better visibility
        player.Pawn.Value!.ObserverServices!.Pawn.Value.Teleport(DeployCameraPosition, DeployCameraRotation == null ? new QAngle(90, 0, 0) : DeployCameraRotation);
        var manager = GetMenuManager();
        if (manager == null) return;

        var menu = manager.CreateMenu(Localizer["Menu.DeployPositions.Title"], false, false, true, false, false);


        foreach (var position in PlayerDeployPositions[player].Where(pos => pos.Player == null && pos.Model != null && pos.Name.Contains("Strategic Beacon"))) // Add strategic beacon deploy positions
        {
            menu.AddOption($"<font color='aqua'>{position.Name}</font>", (p, option) =>
            {
                manager.CloseMenu(p);
                ConfirmDeployMenu(player, position, Config.FocusTheDeployCameraOnDeployPosition); // Confirm deploy menu for player deploy positions
            });
        }
        foreach (var position in PlayerDeployPositions[player].Where(pos => pos.Player == null && pos.Model != null && pos.Name.Contains("Recon Radio"))) // Add recon radio deploy positions
        {
            menu.AddOption($"<font color='deepskyblue'>{position.Name}</font>", (p, option) =>
            {
                manager.CloseMenu(p);
                ConfirmDeployMenu(player, position, Config.FocusTheDeployCameraOnDeployPosition); // Confirm deploy menu for player deploy positions
            });
        }
        foreach (var position in PlayerDeployPositions[player].Where(pos => pos.Player != null && pos.Model == null)) // add player deploy positions 
        {
            menu.AddOption($"<font color='lime'>{position.Name}</font>", (p, option) =>
            {
                if (position.Player!.PlayerPawn.Value!.LifeState != (byte)LifeState_t.LIFE_ALIVE)
                {
                    player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.CannotDeployToDeadPlayer"]}");
                    manager.CloseMenu(p);
                    SelectDeployPositionsMenu(p, player.Pawn.Value!.ObserverServices.Pawn.Value.V_angle); // Reopen the deploy positions menu
                    return;
                }
                manager.CloseMenu(p);
                ConfirmDeployMenu(player, position, Config.FocusTheDeployCameraOnDeployPosition); // Confirm deploy menu for player deploy positions
            });
        }
        foreach (var position in PlayerDeployPositions[player].Where(pos => pos.Player == null && pos.Model != null && pos.Name.Contains("Flag"))) // add flags deploy positions
        {
            menu.AddOption($"<font color='{(player.TeamNum == 2 ? Config.TerroristTeamColor : Config.CTerroristTeamColor)}'>{position.Name}</font>", (p, option) =>
            {
                manager.CloseMenu(p);
                ConfirmDeployMenu(player, position, Config.FocusTheDeployCameraOnDeployPosition); // Confirm deploy menu for player deploy positions
            });
        }
        menu.AddOption($"{Localizer["Menu.DefaultDeployPosition"]}", (p, option) =>
        {
            if (!PlayersRedeployTimer.ContainsKey(player))
            {
                player.Respawn(); // Respawn the player
                manager.CloseMenu(p);
            }
            else
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.RedeployWait", PlayersRedeployTimer[player].Item2]}");
            }
        });
        menu.AddOption($"{Localizer["Menu.DeployRandomly"]}", (p, option) =>
        {
            if (!PlayersRedeployTimer.ContainsKey(player))
            {
                var randomPosition = GetRandomDeployPosition(player); // Get a random deploy position for the player
                while (randomPosition == null) // If no valid random position found, keep trying until a valid position is found
                {
                    randomPosition = GetRandomDeployPosition(player);
                }
                var spawned = SpawnPlayerAtDeployPosition(player, randomPosition); // Spawn the player at the random deploy position
                if (spawned) manager.CloseMenu(p);
            }
            else
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.RedeployWait", PlayersRedeployTimer[player].Item2]}");
            }
        });

        manager.OpenMainMenu(player, menu);
    }
    private void ConfirmDeployMenu(CCSPlayerController player, DeployPositions deployPosition, bool FocusTheDeployCameraOnDeployPosition = true)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        var positionToTeleport = GetFrontPosition(deployPosition.Position, deployPosition.Rotation, -150f);
        List<CDynamicProp>? glow = null;

        // Change player's camera to deploy position
        if (FocusTheDeployCameraOnDeployPosition)
        {
            if (deployPosition.Player != null) positionToTeleport = GetFrontPosition(deployPosition.Player.PlayerPawn.Value!.AbsOrigin!, deployPosition.Player.PlayerPawn.Value.AbsRotation!, -80f); // Get a position in backward (-80f) of the deploy position
            player.Pawn.Value!.ObserverServices!.Pawn.Value.Teleport(new Vector(positionToTeleport.X, positionToTeleport.Y, positionToTeleport.Z + (deployPosition.Player != null ? 65f : 250f)), new QAngle(30, deployPosition.Rotation.Y, deployPosition.Rotation.Z));
        }
        else // Show Glow on Deploy Position
        {
            var lookAtAngle = GetLookAtAngle(player.Pawn.Value!.ObserverServices!.Pawn.Value.AbsOrigin!, deployPosition.Position);
            player.Pawn.Value!.ObserverServices!.Pawn.Value.Teleport(DeployCameraPosition, lookAtAngle); // Make the deploy camera to look at deploy position

            // Now set the glow on the deploy position
            if (deployPosition.Player == null)
            {
                glow = SetGlowOnEntity(deployPosition.Model, Color.Gold, "Flagpole,1", GlowRangeMax: 0); // Set Glow on Flagpole
            }
            else
            {
                if (PlayerStatuses.ContainsKey(deployPosition.Player) && PlayerStatuses[deployPosition.Player].Status == PlayerStatusType.Combat) glow = SetGlowOnPlayer(deployPosition.Player, Color.Red, 0, 0, player.TeamNum);
                glow = SetGlowOnPlayer(deployPosition.Player, Color.Gold, 0, 0, player.TeamNum); // Set glow on player deploy position
            }
            if (!PlayerSeeableGlow.ContainsKey(player)) PlayerSeeableGlow.Add(player, new List<PlayerGlow>()); // Add player to PlayerSeeableGlow if not already present
            if (glow != null && PlayerSeeableGlow.ContainsKey(player))
            {
                var playerGlow = new PlayerGlow
                {
                    EntityIndex = deployPosition.Player != null ? deployPosition.Player.Index : deployPosition.Model!.Index,
                    GlowType = PlayerGlowType.DeployPosition,
                    Glows = glow
                };
                PlayerSeeableGlow[player].Add(playerGlow);
            }
        }

        var menu = manager.CreateMenu(Localizer["Menu.ConfirmDeploy.Title"], false, false, true, false, false);

        menu.AddOption($"{Localizer["Menu.ConfirmDeploy"]}", (p, option) =>
        {
            if (!PlayersRedeployTimer.ContainsKey(player))
            {
                if (deployPosition.Player != null && PlayerStatuses.ContainsKey(deployPosition.Player) && PlayerStatuses[deployPosition.Player].Status == PlayerStatusType.Combat)
                {
                    player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.PlayerInCombat", PlayerStatuses[deployPosition.Player].DefaultName]}");
                    manager.CloseMenu(p);
                    SelectDeployPositionsMenu(p, player.Pawn.Value!.ObserverServices.Pawn.Value.V_angle); // Reopen the deploy menu
                }
                else
                {
                    var spawned = SpawnPlayerAtDeployPosition(player, deployPosition); // Spawn the player at the deploy position
                    if (spawned)
                    {
                        if (deployPosition.Player == null && deployPosition.Name.Contains("Radio")) GivePlayerPoints(player, Config.PlayerPoints.ReconRadioSpawnPoints); // Give points for using deploy position
                        else if (deployPosition.Player != null) GivePlayerPoints(player, Config.PlayerPoints.SquadSpawnPoints); // Give points for using squadmate deploy position
                        manager.CloseMenu(p);
                    }
                    else
                    {
                        if (deployPosition.Player == null) player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.InsufficientSpaceHere"]}");
                        else player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.InsufficientSpaceNearPlayer", PlayerStatuses[deployPosition.Player].DefaultName]}");
                    }
                }
                var glow = PlayerSeeableGlow[player].FirstOrDefault(g => g.EntityIndex == (deployPosition.Player == null ? deployPosition.Model!.Index : deployPosition.Player.Index) && g.GlowType == PlayerGlowType.DeployPosition);
                if (glow != null && PlayerSeeableGlow.ContainsKey(player))
                {
                    PlayerSeeableGlow[player].Remove(glow);
                    RemoveGlow(glow.Glows); // Remove the glow from the player
                }
            }
            else
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.RedeployWait", PlayersRedeployTimer[player].Item2]}");
            }
        });

        menu.AddOption($"{Localizer["Menu.CancelDeploy"]}", (p, option) =>
        {
            manager.CloseMenu(p);
            SelectDeployPositionsMenu(p, player.Pawn.Value!.ObserverServices.Pawn.Value.V_angle); // Reopen the deploy menu
            var glow = PlayerSeeableGlow[player].FirstOrDefault(g => g.EntityIndex == (deployPosition.Player == null ? deployPosition.Model!.Index : deployPosition.Player.Index) && g.GlowType == PlayerGlowType.DeployPosition);
            if (glow != null && PlayerSeeableGlow.ContainsKey(player))
            {
                PlayerSeeableGlow[player].Remove(glow);
                RemoveGlow(glow.Glows); // Remove the glow from the player
            }
        });

        manager.OpenMainMenu(player, menu);
    }

    private void MatchEndStatusMenu(CCSPlayerController player)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        string Winner = MatchStatus.Status == MatchStatusType.TerroristWin ? "Terrorists" : "Counter-Terrorists";
        var menu = manager.CreateMenu(Localizer["Menu.MatchEndWinner.Title", (MatchStatus.Status == MatchStatusType.TerroristWin ? Config.TerroristTeamColor : Config.CTerroristTeamColor), Winner], false, false, true, false, false);

        menu.AddOption($"{Localizer["Menu.MatchStats"]}", (p, option) =>
        {
            ShowMatchStats(p, menu);
        });
        menu.AddOption($"{Localizer["Menu.BestSquadStats"]}", (p, option) =>
        {
            ShowBestSquadStats(p, menu);
        });
        menu.AddOption($"{Localizer["Menu.YourSquadStats"]}", (p, option) =>
        {
            ShowPlayerSquadStats(p, menu);
        });

        manager.OpenMainMenu(player, menu);
    }
    private void ShowMatchStats(CCSPlayerController player, IT3Menu parentMenu)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        var menu = manager.CreateMenu(Localizer["Menu.MatchStats.Title"], false, false, true, true, false);
        menu.ParentMenu = parentMenu;
        menu.IsExitable = false;

        string Winner = MatchStatus.Status == MatchStatusType.TerroristWin ? "Terrorists" : "Counter-Terrorists";
        var matchDuration = TimeSpan.FromSeconds(MatchStatus.MatchEndTime - MatchStatus.MatchStartTime);
        string durationStr = $"{(int)matchDuration.TotalMinutes:D2}:{matchDuration.Seconds:D2}";
        menu.AddOption($"{Localizer["Menu.Winner", (MatchStatus.Status == MatchStatusType.TerroristWin ? Config.TerroristTeamColor : Config.CTerroristTeamColor), Winner]}", (p, option) => { });
        menu.AddOption($"{Localizer["Menu.MatchDuration", durationStr]}", (p, option) => { });
        menu.AddOption($"{Localizer["Menu.RemainingTickets", (MatchStatus.Status == MatchStatusType.TerroristWin ? MatchStatus.TerroristTickets : MatchStatus.CounterTerroristTickets)]}", (p, option) => { });
        menu.AddOption($"{Localizer["Menu.FlagsCaptured", (MatchStatus.Status == MatchStatusType.TerroristWin ? GetFlagsCapturedBy(CsTeam.Terrorist) : GetFlagsCapturedBy(CsTeam.CounterTerrorist))]}", (p, option) => { });

        manager.OpenSubMenu(player, menu);
    }
    private void ShowBestSquadStats(CCSPlayerController player, IT3Menu parentMenu)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        var bestSquad = MatchStatus.BestSquad;
        if (bestSquad == null)
        {
            manager.CloseMenu(player);
            manager.OpenMainMenu(player, parentMenu);
        }

        var menu = manager.CreateMenu(Localizer["Menu.BestSquad.Title", bestSquad!.SquadName], false, false, true, true, false);
        menu.ParentMenu = parentMenu;

        menu.AddOption($"{Localizer["Menu.TotalPoints", bestSquad.TotalPoints]}", (p, option) => { });
        menu.AddOption($"{Localizer["Menu.TotalKills", bestSquad.TotalKills]}", (p, option) => { });
        menu.AddOption($"{Localizer["Menu.TotalDeaths", bestSquad.TotalDeaths]}", (p, option) => { });
        menu.AddOption($"{Localizer["Menu.TotalAssists", bestSquad.TotalAssists]}", (p, option) => { });
        menu.AddOption($"{Localizer["Menu.TotalRevives", bestSquad.TotalRevives]}", (p, option) => { });
        menu.AddSliderOption($"{Localizer["Menu.Members"]}", bestSquad.Members.Select(m => PlayerStatuses[m.Key].DefaultName).ToList<object>(), bestSquad.Members.Select(m => PlayerStatuses[m.Key].DefaultName).ToList<object>()[0], 4, (p, option, value) =>
        {
            var member = bestSquad.Members.ElementAt(value).Key;
            if (member != null && member.IsValid)
            {
                PrintPlayerStats(member);
            }
        });

        manager.OpenSubMenu(player, menu);

        // Now change the PlayerLookingAtSquadPoseEntities to best Squad
        var worldtext = MatchStatus.PlayerLookingAtSquadPoseEntities[player].Item2;
        UpdateWorldText(worldtext!, $"Best Squad:\n{bestSquad.SquadName}", bestSquad.TeamNum == 2 ? Config.TerroristTeamColor : Config.CTerroristTeamColor);
        MatchStatus.PlayerLookingAtSquadPoseEntities[player] = (bestSquad, worldtext);
    }
    private void ShowPlayerSquadStats(CCSPlayerController player, IT3Menu parentMenu)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        var playerSquad = GetPlayerSquad(player)!;
        var menu = manager.CreateMenu(Localizer["Menu.YourSquad.Title", playerSquad.SquadName], false, false, true, true, false);
        menu.ParentMenu = parentMenu;

        menu.AddOption($"{Localizer["Menu.TotalPoints", playerSquad.TotalPoints]}", (p, option) => { });
        menu.AddOption($"{Localizer["Menu.TotalKills", playerSquad.TotalKills]}", (p, option) => { });
        menu.AddOption($"{Localizer["Menu.TotalDeaths", playerSquad.TotalDeaths]}", (p, option) => { });
        menu.AddOption($"{Localizer["Menu.TotalAssists", playerSquad.TotalAssists]}", (p, option) => { });
        menu.AddOption($"{Localizer["Menu.TotalRevives", playerSquad.TotalRevives]}", (p, option) => { });
        menu.AddSliderOption($"{Localizer["Menu.Members"]}", playerSquad.Members.Select(m => PlayerStatuses[m.Key].DefaultName).ToList<object>(), playerSquad.Members.Select(m => PlayerStatuses[m.Key].DefaultName).ToList<object>()[0], 4, (p, option, value) =>
        {
            var member = playerSquad.Members.ElementAt(value).Key;
            if (member != null && member.IsValid)
            {
                PrintPlayerStats(member);
            }
        });

        manager.OpenSubMenu(player, menu);

        // Now change the PlayerLookingAtSquadPoseEntities to his squad
        var worldtext = MatchStatus.PlayerLookingAtSquadPoseEntities[player].Item2;
        UpdateWorldText(worldtext!, $"Your Squad:\n{playerSquad.SquadName}", playerSquad.TeamNum == 2 ? Config.TerroristTeamColor : Config.CTerroristTeamColor);
        MatchStatus.PlayerLookingAtSquadPoseEntities[player] = (playerSquad, worldtext);
    }
    public void PrintPlayerStats(CCSPlayerController player)
    {
        if (player == null || !player.IsValid) return;

        var stats = PlayerStatuses[player];
        if (stats == null) return;

        player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.YourStats"]}");
        player.PrintToChat($"{Localizer["Chat.StatsPoints", stats.TotalPoints]}");
        player.PrintToChat($"{Localizer["Chat.StatsKills", stats.TotalKills]}");
        player.PrintToChat($"{Localizer["Chat.StatsDeaths", stats.TotalDeaths]}");
        player.PrintToChat($"{Localizer["Chat.StatsAssists", stats.TotalAssists]}");
        player.PrintToChat($"{Localizer["Chat.StatsDamageDealt", stats.TotalDamageDealt]}");
        player.PrintToChat($"{Localizer["Chat.StatsRevives", stats.TotalRevives]}");
    }
    public void OpenCallInAttackMenu(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || !PlayerStatuses.ContainsKey(player)) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        // Stop Shooting
        StopShootingForSpecificTime(player);
        var menu = manager.CreateMenu(Localizer["Menu.CallInAttacks.Title"], false, true, true, false, true);

        // Add Call-In Attack options
        foreach (var attack in Config.CallInAttacks)
        {
            if(!PlayerStatuses[player].CallInAttacksUsage.ContainsKey(attack.Name)) PlayerStatuses[player].CallInAttacksUsage[attack.Name] = (0, 0);

            menu.AddOption($"<font color='red'>{attack.Name}</font> <font color='lime'>[{attack.Cost + PlayerStatuses[player].CallInAttacksUsage[attack.Name].Item1} Points]</font>", (p, option) =>
            {
                if(attack.Name != "Smoke Barrage" && IsCallInAttackAlreadyCalledByTeam(attack.Name, player.TeamNum))
                {
                    player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.CallInAttackAlreadyCalled", attack.Name]}");
                   return;
                }
                if (attack.MaxCount > 0 && (PlayerStatuses[player].CallInAttacksUsage[attack.Name].Item1 / attack.IncreaseCostPerUse) >= attack.MaxCount)
                {
                    player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.CallInAttackMaxUsageReached", attack.Name, PlayerStatuses[player].CallInAttacksUsage[attack.Name].Item1, attack.MaxCount]}");
                    return;
                }
                if (PlayerStatuses[player].CallInAttacksUsage[attack.Name].Item2 > Server.CurrentTime)
                {
                    player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.CallInAttackOnCooldown", attack.Name, (int)(PlayerStatuses[player].CallInAttacksUsage[attack.Name].Item2 + attack.Cooldown - Server.CurrentTime)]}");
                    return;
                }
                if (PlayerStatuses[p].TotalCallInPoints >= (attack.Cost + PlayerStatuses[player].CallInAttacksUsage[attack.Name].Item1))
                {
                    manager.CloseMenu(p);
                    CallInAttackConfirmationMenu(p, attack);
                }
                else
                {
                    player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.NotEnoughPoints", attack.Name, (attack.Cost + PlayerStatuses[player].CallInAttacksUsage[attack.Name].Item1) - PlayerStatuses[player].TotalCallInPoints]}");
                    return;
                }
            });
        }
        manager.OpenMainMenu(player, menu);

        manager.OnMenuClose += OnCallInAttackMenuClose;
    }
    public void OnCallInAttackMenuClose(CCSPlayerController player, IT3Menu menu)
    {
        if (player == null || !player.IsValid) return;

        StartShooting(player);
    }
    public void CallInAttackConfirmationMenu(CCSPlayerController player, CallInAttacks attack)
    {
        if (player == null || !player.IsValid) return;

        var manager = GetMenuManager();
        if (manager == null) return;

        // Reset call-in attack position
        PlayerStatuses[player].CallInAttackPosition = Vector.Zero;
        // Change player's camera position to a high position for better visibility
        if (attack.Name != "Strategic Beacon") PlayerStatuses[player].PlayerCallInAttackCamera = CreatePlayerCallInAttackCameraProp(player, DeployCameraPosition, new QAngle(90, 0, 0));

        var menu = manager.CreateMenu(Localizer["Menu.CallInConfirm", attack.Name, (attack.Cost + PlayerStatuses[player].CallInAttacksUsage[attack.Name].Item1)], false, false, true, false, false);
        menu.FreezePlayer = false;
        if (attack.Name != "Strategic Beacon") menu.FreezePlayer = true; // Freeze player if not Strategic Beacon

        // Stop Shooting
        StopShootingForSpecificTime(player);

        menu.AddOption($"{Localizer["Menu.ConfirmCallIn", attack.Name]}", (p, option) =>
        {
            if (attack.Name != "Strategic Beacon")
            {
                RemoveLaserBeams(PlayerStatuses[player].CallInAttackBeams); // Remove laser beams if any
                if (PlayerStatuses[player].CallInAttackPosition == Vector.Zero)
                {
                    player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.MustSelectPositionForCallIn"]}");
                    return;
                }
                if (PlayerStatuses[player].PlayerCallInAttackCamera != null && PlayerStatuses[player].PlayerCallInAttackCamera!.IsValid) PlayerStatuses[player].PlayerCallInAttackCamera!.Remove(); // Remove the call-in attack camera prop
                Utilities.SetStateChanged(player.PlayerPawn!.Value!, "CBasePlayerPawn", "m_pCameraServices");
            }
            else
            {
                PlayerStatuses[player].CallInAttackPosition = GetFrontPosition(player.PlayerPawn!.Value!.AbsOrigin!, player.PlayerPawn!.Value!.AbsRotation!, 15f); // For Strategic Beacon, set the position in front of the player
            }    
            StartShooting(player);
            ExecuteCallInAttack(player, attack, PlayerStatuses[player].CallInAttackPosition);
            manager.CloseMenu(p);
        });

        menu.AddOption($"{Localizer["Menu.Cancel"]}", (p, option) =>
        {
            RemoveLaserBeams(PlayerStatuses[player].CallInAttackBeams); // Remove laser beams if any
            if (attack.Name != "Strategic Beacon")
            {
                if (PlayerStatuses[player].PlayerCallInAttackCamera != null && PlayerStatuses[player].PlayerCallInAttackCamera!.IsValid) PlayerStatuses[player].PlayerCallInAttackCamera!.Remove(); // Remove the call-in attack camera prop
                Utilities.SetStateChanged(player.PlayerPawn!.Value!, "CBasePlayerPawn", "m_pCameraServices");
            }
            StartShooting(player);
            manager.CloseMenu(p);
            OpenCallInAttackMenu(p); // Reopen the call-in attack menu
        });

        manager.OpenMainMenu(player, menu);
    }
    
}