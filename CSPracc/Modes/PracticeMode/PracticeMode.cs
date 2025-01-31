﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using CSPracc.CommandHandler;
using CSPracc.DataModules;
using CSPracc.DataModules.Constants;
using CSPracc.EventHandler;
using CSPracc.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static CSPracc.DataModules.Enums;
using T3MenuSharedApi;

namespace CSPracc.Modes
{
    public class PracticeMode : BaseMode
    {
        BotReplayManager BotReplayManager { get; set; }
        
        public IT3MenuManager? MenuManager;
        public static PluginCapability<IT3MenuManager> IT3MenuCapability { get; } = new("t3menu:manager");
  
        /// <summary>
        /// Get settings
        /// </summary>
        /// <param name="ccsplayerController">player who issued the command</param>
        /// <returns>return setting menu</returns>
        public IT3Menu GetPracticeSettingsMenu(CCSPlayerController ccsplayerController)
        {
            var manager = GetMenuManager();
            var menu = manager.CreateMenu("Practice Mode Settings", isSubMenu: false);

            // Build Menu and Submenus.
            
            // Show Personalized Nade Menu instead of Global?
            ccsplayerController.GetValueOfCookie("PersonalizedNadeMenu", out string? setting);
            bool default_option_value = false;
            
            if (setting == "yes")
                default_option_value = true;
            

            menu.AddBoolOption("Only show personal nades", default_option_value, (p, option) =>
                {
                    if (option is IT3Option boolOption)
                    {
                        bool isEnabled = boolOption.OptionDisplay!.Contains("✔");
                        if (isEnabled)
                        {
                            p.SetOrAddValueOfCookie("PersonalizedNadeMenu", "yes");
                        }
                        else
                        {
                            p.SetOrAddValueOfCookie("PersonalizedNadeMenu", "no");
                        }
                    }
                    
                    
                });
            
            // Allow choosing one or more roles to filter always filter nades by.

            var availableRoles = projectileManager.GetAllRoles();

            // Create roles menu
            var rolesMenu = manager.CreateMenu("Filter by Roles", isSubMenu: true);
            rolesMenu.ParentMenu = menu;

            // Get current player roles from cookie
            ccsplayerController.GetValueOfCookie("Roles",  out string? playerRoles);
            if (playerRoles != null)
            {
                CSPraccPlugin.Instance!.Logger.LogInformation($"Current player roles:{playerRoles}");
                // Convert player roles to list if it's not already a list
                var rolesList = new List<string>(playerRoles.Split(','));
                foreach (var role in availableRoles)
                {
                    // Create option for each role and set default value based on whether the role is in the player's list
                    bool defaultOptionValue = rolesList.Contains(role);
                    
                    // Add option to menu
                    rolesMenu.AddBoolOption(role, defaultOptionValue, (p, option) =>
                    {
                        if (option is IT3Option boolOption)
                        {
                            var isEnabled = boolOption.OptionDisplay!.Contains("✔");
                            
                            // Handle button click
                            if (isEnabled)
                            {
                                if (!rolesList.Contains(role))
                                {
                                    rolesList.Add(role);
                                }
                            }
                            else
                            {
                                if (rolesList.Contains(role))
                                {
                                    rolesList.Remove(role);
                                }
                            }
                            
                            // Update cookie with player's selected roles
                            string updatedRoles = string.Join(",", rolesList);
                            p.SetOrAddValueOfCookie("Roles", updatedRoles);
                        }
                    });
                }
            }
            else
            {
                CSPraccPlugin.Instance!.Logger.LogInformation($"Player has no roles configured.");
                // If no roles are saved in the cookie, initialize it.
                var rolesList = new List<string>();
                
                foreach (var role in availableRoles)
                {
                    rolesMenu.AddBoolOption(role, false, (p, option) =>
                    {
                        if (option is IT3Option boolOption)
                        {
                            var isEnabled = boolOption.OptionDisplay!.Contains("✔");
                            
                            // Handle button click
                            if (isEnabled)
                            {
                                if (!rolesList.Contains(role))
                                {
                                    rolesList.Add(role);
                                }
                            }
                            else
                            {
                                if (rolesList.Contains(role))
                                {
                                    rolesList.Remove(role);
                                }
                            }
                            
                            // Update cookie with player's selected roles
                            string updatedRoles = string.Join(",", rolesList);
                            
                            if (string.IsNullOrEmpty(updatedRoles))
                            {
                                updatedRoles = "";
                            }
                            CSPraccPlugin.Instance!.Logger.LogInformation($"Updated player roles:{updatedRoles}");
                            p.SetOrAddValueOfCookie("Roles", updatedRoles);
                        }
                    });
                }
            }
            menu.Add("Set Roles", (p, option) =>
            {
                manager.OpenSubMenu(ccsplayerController, rolesMenu);
            });
            
            return menu;
        }

        /// <summary>
        /// Return Mimic menu
        /// </summary>
        /// <param name="ccsplayerController">palyer who issued the command</param>
        /// <returns></returns>
        private HtmlMenu getBotMimicMenu(CCSPlayerController ccsplayerController)
        {
            HtmlMenu mimic_menu;
            List<KeyValuePair<string, Action>> menuOptions = new List<KeyValuePair<string, Action>>();
            menuOptions.Add( new KeyValuePair<string, Action>("List existing replay", () => CSPraccPlugin.Instance!.AddTimer(0.5f, () => ShowMimcReplays(ccsplayerController))));
            menuOptions.Add(new KeyValuePair<string, Action>("Create new replay", new Action(() => CreateReplay(ccsplayerController))));
            menuOptions.Add(new KeyValuePair<string, Action>("Delete existing replay", () => CSPraccPlugin.Instance!.AddTimer(0.5f, () => DeleteMimicReplay(ccsplayerController))));
            return mimic_menu = new HtmlMenu("Bot Mimic Menu", menuOptions);
        }

        /// <summary>
        /// Show mimic menu to the player
        /// </summary>
        /// <param name="player">player who issued the command</param>
        public void ShowMimicMenu(CCSPlayerController player)
        {
            HtmlMenu mimicMenu = getBotMimicMenu(player);
            GuiManager.AddMenu(player.SteamID, mimicMenu);
        }

        /// <summary>
        /// Rename last created replay set
        /// </summary>
        /// <param name="player">player who issued the command</param>
        /// <param name="newReplaySetName">new name</param>
        public void RenameCurrentReplaySet(CCSPlayerController player,string newReplaySetName)
        {
            if (newReplaySetName == "")
            {
                player.ChatMessage("Please pass a new replay set name");
                return;
            }
            BotReplayManager.RenameCurrentReplaySet(player,newReplaySetName);
        }

        /// <summary>
        /// Store the last recorded replay
        /// </summary>
        /// <param name="player">player who issued the command</param>
        public void StoreLastReplay(CCSPlayerController player)
        {
            BotReplayManager.SaveLastReplay(player);
        }

        /// <summary>
        /// List all replays and play on selection
        /// </summary>
        /// <param name="player">player who issued the command</param>
        public void ShowMimcReplays(CCSPlayerController player)
        {
            HtmlMenu replay_menu;
            List<KeyValuePair<string, Action>> menuOptions = new List<KeyValuePair<string, Action>>();
            List<KeyValuePair<int, ReplaySet>> replays = BotReplayManager.GetAllCurrentReplays();
            if(replays.Count == 0)
            {
                player.ChatMessage($"There are currently no replays existing. Create one using {PRACC_COMMAND.create_replay} 'name of the replay'");
                return;
            }
            for (int i = 0;i<replays.Count;i++)
            {
                ReplaySet set = replays[i].Value;
                menuOptions.Add(new KeyValuePair<string, Action>($"{replays[i].Value.SetName}", () => BotReplayManager.PlayReplaySet(set)));
            }
            replay_menu = new HtmlMenu("Replays", menuOptions);
            GuiManager.AddMenu(player.SteamID, replay_menu);
            return;
        }

        /// <summary>
        /// Show menu to delete replay
        /// </summary>
        /// <param name="ccsplayerController">player who issued the commands</param>
        public void DeleteMimicReplay(CCSPlayerController player)
        {
            if(!player.IsAdmin())
            {
                player.ChatMessage("Only admins can delete replays!");
                return;
            }
            HtmlMenu deletion_menu;
            List<KeyValuePair<string, Action>> menuOptions = new List<KeyValuePair<string, Action>>();
            List<KeyValuePair<int, ReplaySet>> replays = BotReplayManager.GetAllCurrentReplays();
            if(replays.Count == 0)
            {
                player.ChatMessage($"There are currently no replays existing. Create one using {PRACC_COMMAND.create_replay} 'name of the replay'");
                return;
            }
            for (int i = 0; i < replays.Count; i++)
            {
                Server.PrintToConsole($"Logging {replays[i].Value.SetName}");
                int id = replays[i].Key;
                menuOptions.Add(new KeyValuePair<string, Action>($"{replays[i].Value.SetName}", () => BotReplayManager.DeleteReplaySet(player, id)));
            }
            deletion_menu = new HtmlMenu("Delete Replay", menuOptions);
            GuiManager.AddMenu(player.SteamID, deletion_menu);
            return;
        }

        /// <summary>
        /// Create new replay set
        /// </summary>
        /// <param name="player"></param>
        /// <param name="name"></param>
        public void CreateReplay(CCSPlayerController player,string name = "new replayset")
        {
            if (name == "") name = "new replayset";
            player.ChatMessage($"You are now in editing mode. For replay '{name}'");
            player.ChatMessage($"Use {ChatColors.Green}{PRACC_COMMAND.record_role}{ChatColors.White} to record a new role.");
            player.ChatMessage($"Use {ChatColors.Green}{PRACC_COMMAND.stoprecord}{ChatColors.White} to stop the recording.");
            player.ChatMessage($"Use {ChatColors.Green}{PRACC_COMMAND.store_replay}{ChatColors.White} 'name' to save the record with the given name.");
            player.ChatMessage($"Use {ChatColors.Green}{PRACC_COMMAND.rename_replayset}{ChatColors.White} to set a new name.");
            BotReplayManager.CreateReplaySet(player,name);
        }

        private void SwitchPersonalizedNadeMenuOption(CCSPlayerController player)
        {
            if (!player.GetValueOfCookie("PersonalizedNadeMenu", out string? setting))
            {
                setting = "yes";
                player.SetOrAddValueOfCookie("PersonalizedNadeMenu", "yes");
            }
            switch(setting)
            {
                case "yes":
                    player.SetOrAddValueOfCookie("PersonalizedNadeMenu", "no");
                    break;
                case "no":
                    player.SetOrAddValueOfCookie("PersonalizedNadeMenu", "yes");
                    break;
                default:
                    player.SetOrAddValueOfCookie("PersonalizedNadeMenu", "yes");
                    break;
            }
        }

        ProjectileManager projectileManager;
        PracticeBotManager PracticeBotManager;
        SpawnManager SpawnManager;
        public PracticeMode() : base() 
        {
            projectileManager = new ProjectileManager();
            PracticeBotManager = new PracticeBotManager();
            SpawnManager = new SpawnManager();
            BotReplayManager = new BotReplayManager(ref PracticeBotManager, ref projectileManager);  
        }

        public void StartTimer(CCSPlayerController player)
        {
            if (player == null) return;
            base.GuiManager.StartTimer(player);
        }

        public void AddCountdown(CCSPlayerController player, int countdown)
        {
            if (player == null) return;
            base.GuiManager.StartCountdown(player,countdown);
        }

        public void ShowPlayerBasedNadeMenu(CCSPlayerController player,string tag = "",string name="")
        {
            if (player == null) return;
            if(!player.IsValid) return;
            
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties["tag"] = tag;
            properties["name"] = name;
            ShowT3Menu(player, projectileManager.GetPlayerBasedNadeMenu(player, properties));
        }
        
        // get the instance
        public IT3MenuManager? GetMenuManager()
        {
            if (MenuManager == null)
                MenuManager = IT3MenuCapability.Get();

            return MenuManager;
        }

        public void ShowT3Menu(CCSPlayerController player, IT3Menu menu)
        { 
            if (player == null) return;
            if(!player.IsValid) return;
            // get the manager and check of nullabilty
            var manager = GetMenuManager();
            if (manager == null)
                return;
            manager.OpenMainMenu(player, menu);
        }
        
        public void ShowTagsMenu(CCSPlayerController player)
        {
            if (player == null) return;
            if(!player.IsValid) return;

            var tagsMenu = projectileManager.GetTagsMenu(player.SteamID);
            ShowT3Menu(player, tagsMenu);
        }
        
        public void ShowRolesMenu(CCSPlayerController player)
        {
            if (player == null) return;
            if(!player.IsValid) return;

            var rolesMenu = projectileManager.GetRolesMenu(player);
            ShowT3Menu(player, rolesMenu);
        }
        
        public void ShowStratsMenu(CCSPlayerController player)
        {
            if (player == null) return;
            if(!player.IsValid) return;

            var stratsMenu = projectileManager.GetStratsMenu(player);
            ShowT3Menu(player, stratsMenu);
        }

        public void ShowNadeWizardMenu(CCSPlayerController player, int id = 0)
        {
            // Build NadeWizard Menu:
            if (player == null) return;
            if(!player.IsValid) return;
            // get the manager and check of nullabilty
            var manager = GetMenuManager();
            if (manager == null)
                return;
            var menu = manager.CreateMenu("Nade Wizard", isSubMenu: false);

            var current_nade = new ProjectileSnapshot();

            if (id != 0)
            {
                current_nade = projectileManager.GetNadeById(id);
            }

            // Build Tags submenu.
            var tagsmenu = manager.CreateMenu("Tags", isSubMenu: true);
            tagsmenu.ParentMenu = menu;

            List <string> tags = projectileManager.GetAllTags();

            foreach (string tag in tags)
            {
                bool default_option_value = false;
                default_option_value = current_nade.Tags.Contains(tag);
                tagsmenu.AddBoolOption(tag, default_option_value,  (p, option) =>
                {
                    if (option is IT3Option boolOption)
                    {
                        bool isEnabled = boolOption.OptionDisplay!.Contains("✔");
                        if (isEnabled)
                        {
                            projectileManager.AddTagToLastGrenade(player, tag);
                        }
                        else
                        {
                            projectileManager.RemoveTagFromLastGrenade(player.SteamID, tag);
                        }
                            
                    }
                });
            }
            
            menu.Add("Tags", (p, option) =>
            {
                manager.OpenSubMenu(player, tagsmenu);
            });
            
            // Get available roles.
            var rolesmenu = manager.CreateMenu("Roles", isSubMenu: true);
            rolesmenu.ParentMenu = menu;
            
            List <string> roles = projectileManager.GetAllRoles();

            foreach (string role in roles)
            {
                bool default_option_value = false;
                default_option_value = current_nade.Roles.Contains(role);
                
                rolesmenu.AddBoolOption(role, default_option_value,  (p, option) =>
                {
                    if (option is IT3Option boolOption)
                    {
                        bool isEnabled = boolOption.OptionDisplay!.Contains("✔");
                        if (isEnabled)
                        {
                            projectileManager.AddRoleToLastGrenade(player, role);
                        }
                        else
                        {
                            projectileManager.RemoveRoleFromLastGrenade(player, role);
                        }
                    }
                });
            }
            
            menu.Add("Roles", (p, option) =>
            {
                manager.OpenSubMenu(player, rolesmenu);
            });
            
            // Get available Strats.
            var stratsmenu = manager.CreateMenu("Strats", isSubMenu: true);
            stratsmenu.ParentMenu = menu;
            
            List <string> strats = projectileManager.GetAllStrats();

            foreach (string strat in strats)
            {
                bool default_option_value = false;
                default_option_value = current_nade.Strats.Contains(strat);
                
                stratsmenu.AddBoolOption(strat, default_option_value,  (p, option) =>
                {
                    if (option is IT3Option boolOption)
                    {
                        bool isEnabled = boolOption.OptionDisplay!.Contains("✔");
                        if (isEnabled)
                        {
                            projectileManager.AddStratToLastGrenade(player, strat);
                        }
                        else
                        {
                            projectileManager.RemoveStratFromLastGrenade(player, strat);
                        }
                    }
                });
            }
            
            menu.Add("Strats", (p, option) =>
            {
                manager.OpenSubMenu(player, stratsmenu);
            });
            
            // Add option to set or change the team.
            var teammenu = manager.CreateMenu("Team", isSubMenu: true);
            teammenu.ParentMenu = menu;
            List<CsTeam> teams = Enum.GetValues(typeof(CsTeam)).Cast<CsTeam>().ToList();
            
            foreach (CsTeam team in teams)
            {
                bool default_option_value = current_nade.Team == team;
                
                var option_name = team.ToString();
                
                if (default_option_value)
                    option_name += " ✔";
    
                teammenu.Add(option_name, (p, option) =>
                {   
                    projectileManager.SetTeamToLastGrenade(player, team);
                    manager.CloseMenu(player);
                    ShowNadeWizardMenu(player, id);
                });
            }

            menu.Add("Teams", (p, option) =>
            {
                manager.OpenSubMenu(player, teammenu);
            });
            
            ShowT3Menu(player, menu);
        }

        public void ShowCompleteNadeMenu(CCSPlayerController player)
        {
            if (player == null) return;
            if (!player.IsValid) return;

            GuiManager.AddMenu(player.SteamID, projectileManager.GetNadeMenu(player));
        }

        public override void ConfigureEnvironment(bool hotReload = true)
        {
            if(hotReload)
            {
                DataModules.Constants.Methods.MsgToServer("Loading practice mode.");
                Server.ExecuteCommand("exec CSPRACC\\pracc.cfg");
            }
            EventHandler?.Dispose();
            EventHandler = new PracticeEventHandler(CSPraccPlugin.Instance!, new PracticeCommandHandler(this, ref projectileManager,ref PracticeBotManager, ref SpawnManager),ref projectileManager, ref PracticeBotManager);
        }

        public void ShowPracticeMenu(CCSPlayerController player)
        {
            if (player == null) return;
            if (!player.IsValid) return;
            ShowT3Menu(player, GetPracticeSettingsMenu(player));
        }

        public void Record(CCSPlayerController playerController,string name = "")
        {
            BotReplayManager.RecordPlayer(playerController, name);
        }

        public void StopRecord(CCSPlayerController playerController)
        {
            BotReplayManager.StopRecording(playerController);
        }

        public void ReplayLastRecord(CCSPlayerController playerController)
        {
            BotReplayManager.ReplayLastReplay(playerController);
        }
        public override void Dispose()
        {
            Server.ExecuteCommand("exec CSPRACC\\undo_pracc.cfg");
            projectileManager.Dispose();
            base.Dispose();
        }
    }
}
