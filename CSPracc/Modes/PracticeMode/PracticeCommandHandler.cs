﻿using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using CSPracc.DataModules.Constants;
using CSPracc.Managers;
using CSPracc.DataModules;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CSPracc.Modes;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System.Reflection.Metadata.Ecma335;
using CounterStrikeSharp.API.Modules.Cvars;

namespace CSPracc.CommandHandler
{
    public class PracticeCommandHandler : BaseCommandHandler
    {

        Dictionary<CCSPlayerController, Position> checkpoints = new Dictionary<CCSPlayerController, Position>();
        PracticeBotManager BotManager {  get; set; }
        PracticeMode PracticeMode { get; set; }
        SpawnManager SpawnManager { get; set; }

        ProjectileManager ProjectileManager { get; set; }
        public PracticeCommandHandler(PracticeMode mode,ref ProjectileManager projectileManager, ref PracticeBotManager botManager, ref SpawnManager spawnManager): base(mode)
        {
            PracticeMode = mode;
            BotManager = botManager;
            ProjectileManager =  projectileManager;
            SpawnManager = spawnManager;
            CSPraccPlugin.Instance.AddCommand("css_pracc_smokecolor_enabled", "Enables / disabled smoke color", PraccSmokecolorEnabled);
        }

        public static bool PraccSmokeColorEnabled = true;
        private void PraccSmokecolorEnabled(CCSPlayerController? player, CommandInfo command)
        {
            if (command == null)
            {
                return;
            }
            if(command.ArgString.Length == 0)
            {
                Server.PrintToConsole("PraccSmokecolorEnabled used with invalid args");
            }
            if(command.ArgString.ToLower() == "true" || command.ArgString.ToLower() == "1")
            {
                PraccSmokeColorEnabled=true;
            }
            if (command.ArgString.ToLower() == "false" || command.ArgString.ToLower() == "0")
            {
                PraccSmokeColorEnabled = false;
            }
            return;
        }

        public override bool PlayerChat(EventPlayerChat @event, GameEventInfo info)
        {
            if (!CheckAndGetCommand(@event.Userid, @event.Text, out string command, out string args, out CCSPlayerController player))
            {
                return false;
            }
            switch (command)
            {
                case PRACC_COMMAND.SPAWN:
                    {
                        SpawnManager.TeleportToSpawn(player, args);
                        break;
                    }
                case PRACC_COMMAND.TSPAWN:
                    {
                        SpawnManager.TeleportToTeamSpawn(player, args, CsTeam.Terrorist);
                        break;
                    }
                case PRACC_COMMAND.CTSPAWN:
                    {
                        SpawnManager.TeleportToTeamSpawn(player, args, CsTeam.CounterTerrorist);
                        break;
                    }
                case PRACC_COMMAND.NADES:
                    {
                        if (args.Length > 0)
                        {
                            if(args.Trim().ToLower() == "all")
                            {
                                PracticeMode.ShowCompleteNadeMenu(player);
                                break;
                            }
                            else if (int.TryParse(args, out int id))
                            {
                                ProjectileManager.RestoreSnapshot(player, id);
                                ProjectileManager.SetLastAddedProjectileSnapshot(player.SteamID, id);
                                break;
                            }
                            else
                            {
                                PracticeMode.ShowPlayerBasedNadeMenu(player,args.ToLower());
                                break;
                            }
                            player.PrintToCenter("Could not parse argument for nade menu");
                        }
                        PracticeMode.ShowPlayerBasedNadeMenu(player);
                        break;
                    }
                case PRACC_COMMAND.SAVE:
                    {
                        var useNadeWizard = CSPraccPlugin.Instance.Config.UseNadeWizard;
                        ProjectileManager.SaveSnapshot(player, args); 
                        if (useNadeWizard)
                        {
                         PracticeMode.ShowNadeWizardMenu(player, ProjectileManager.getLastAddedProjectileSnapshot(player.SteamID).Key);
                        }
                        break;
                    }
                case PRACC_COMMAND.Delete:
                    {
                        ProjectileManager.RemoveSnapshot(player, args);
                        break;
                    }
                case PRACC_COMMAND.BOT:
                    {
                        BotManager.AddBot(player);
                        break;
                    }
                case PRACC_COMMAND.tBOT:
                    {
                        BotManager.AddBot(player,false,CsTeam.Terrorist);
                        break;
                    }
                case PRACC_COMMAND.ctBOT:
                    {
                        BotManager.AddBot(player, false, CsTeam.CounterTerrorist);
                        break;
                    }
                case PRACC_COMMAND.BOOST:
                    {
                        BotManager.Boost(player);
                        break;
                    }
                case PRACC_COMMAND.NOBOT:
                    {
                        BotManager.NoBot(player);
                        break;
                    }
                case PRACC_COMMAND.CLEARBOTS:
                    {
                        BotManager.ClearBots(player);
                        break;
                    }
                case PRACC_COMMAND.WATCHME:
                    {
                        //TODO: Utilities.GetPlayers?
                        var playerEntities = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
                        foreach (var playerEnt in playerEntities)
                        {
                            if (playerEnt == null) continue;
                            if (!playerEnt.IsValid) continue;
                            if (playerEnt.UserId == player.UserId) continue;
                            if (playerEnt.IsBot) continue;
                            playerEnt.ChangeTeam(CsTeam.Spectator);
                            Logging.LogMessage($"Switching {playerEnt.PlayerName} to spectator");
                        }
                        break;
                    }
                case PRACC_COMMAND.CROUCHBOT:
                    {
                        BotManager.CrouchBot(player);
                        break;
                    }
                case PRACC_COMMAND.CROUCHBOOST:
                    {
                        BotManager.CrouchingBoostBot(player);
                        break;
                    }
                case PRACC_COMMAND.CLEAR:
                    {
                        ProjectileManager.ClearNades(player, false);
                        break;
                    }
                case PRACC_COMMAND.ClearAll:
                    {
                        ProjectileManager.ClearNades(player, true);
                        break;
                    }
                case PRACC_COMMAND.CHECKPOINT:
                    {
                        if(!checkpoints.ContainsKey(player))
                        {
                            checkpoints.Add(player, player.GetCurrentPosition());
                            player.PrintToCenter("Saved current checkpoint");
                        }
                        else
                        {
                            checkpoints[player] = player.GetCurrentPosition();
                            player.PrintToCenter("Saved current checkpoint");
                        }
                        break;
                    }
                case PRACC_COMMAND.TELEPORT:
                    {
                        if (!checkpoints.ContainsKey(player))
                        {
                            player.PrintToCenter($"You dont have a saved checkpoint");
                        }
                        else
                        {
                            player.PlayerPawn.Value!.Teleport(checkpoints[player].PlayerPosition, checkpoints[player].PlayerAngle,new Vector(0,0,0));
                            player.PrintToCenter("Teleported to your checkpoint");
                        }
                        break;
                    }
                case PRACC_COMMAND.timer:
                    {
                        PracticeMode.StartTimer(player);
                        break;
                    }
                case PRACC_COMMAND.countdown:
                    {
                        if (args.Length > 0)
                        {
                            if (int.TryParse(args, out int time))
                            {
                                PracticeMode.AddCountdown(player, time);
                                break;
                            }
                        }
                        Utils.ClientChatMessage($"{ChatColors.Red}Could not parse parameter", player);
                        break;
                    }
                case PRACC_COMMAND.rethrow:
                    {                                              
                        ProjectileManager.ReThrow(player,args);
                        break;
                    }
                case PRACC_COMMAND.flash:
                    {       
                        ProjectileManager.Flash(player);
                        break;
                    }
                case PRACC_COMMAND.noflash:
                    {
                        ProjectileManager.NoFlash(player);
                        break;
                    }
                case PRACC_COMMAND.stop:
                    {
                        ProjectileManager.Stop(player);
                        break;
                    }
                case PRACC_COMMAND.Description:
                    {
                        ProjectileManager.AddDescription(player.SteamID, args);
                        break;
                    }
                case PRACC_COMMAND.delay:
                    {
                        ProjectileManager.SetDelay(player.SteamID, args);
                        break;
                    }
                case PRACC_COMMAND.Rename:
                    {
                        ProjectileManager.RenameLastSnapshot(player.SteamID, args);
                        break;
                    }
                case PRACC_COMMAND.AddTag:
                    {
                        ProjectileManager.AddTagToLastGrenade(player, args);
                        break;
                    }
                case PRACC_COMMAND.RemoveTag:
                    {
                        ProjectileManager.RemoveTagFromLastGrenade(player.SteamID, args);
                        break;
                    }
                case PRACC_COMMAND.ClearTags:
                    {
                        ProjectileManager.ClearTagsFromLastGrenade(player.SteamID);
                        break;
                    }
                case PRACC_COMMAND.DeleteTag:
                    {
                        ProjectileManager.DeleteTagFromAllNades(player.SteamID,args);
                        break;
                    }
                case PRACC_COMMAND.ADDROLE:
                    {
                        ProjectileManager.AddRoleToLastGrenade(player, args);
                        break;
                      }
                case PRACC_COMMAND.REMOVEROLE:
                     {
                        ProjectileManager.RemoveRoleFromLastGrenade(player, args);
                        break;
                    }
                case PRACC_COMMAND.SHOWROLES:
                {
                    PracticeMode.ShowRolesMenu(player);
                    break;
                }
                case PRACC_COMMAND.ADDSTRAT:
                {
                    ProjectileManager.AddStratToLastGrenade(player, args);
                    break;
                }
                case PRACC_COMMAND.REMOVESTRAT:
                {
                    ProjectileManager.RemoveStratFromLastGrenade(player, args);
                    break;
                }
                case PRACC_COMMAND.SHOWSTRAT:
                {
                    PracticeMode.ShowStratsMenu(player);
                    break;
                }
                case PRACC_COMMAND.UpdatePos:
                    ProjectileManager.UpdatePosition(player);
                    break;
                case PRACC_COMMAND.Last:
                    {
                        ProjectileManager.RestorePlayersLastThrownGrenade(player,-1); 
                        break;
                    }
                case PRACC_COMMAND.BACK:
                    {
                        if (args.Length > 0)
                        {
                            if (int.TryParse(args, out int id))
                            {
                                ProjectileManager.RestorePlayersLastThrownGrenade(player,id);
                                break;
                            }
                        }
                        ProjectileManager.RestorePlayersLastThrownGrenade(player);
                        break;
                    }
                case PRACC_COMMAND.forward:
                    {
                        if (args.Length > 0)
                        {
                            if (int.TryParse(args, out int id))
                            {
                                ProjectileManager.RestoreNextPlayersLastThrownGrenade(player,id);
                                break;
                            }
                        }
                        ProjectileManager.RestoreNextPlayersLastThrownGrenade(player);
                        break;
                    }
                case PRACC_COMMAND.bestspawn:
                    {
                        SpawnManager.TeleportToBestSpawn(player);
                        break;
                    }
                case PRACC_COMMAND.worstspawn:
                    {
                        SpawnManager.TeleportToWorstSpawn(player);
                        break;
                    }
                case PRACC_COMMAND.SwapBot:
                    {
                        BotManager.SwapBot(player);
                        break;
                    }
                case PRACC_COMMAND.MoveBot:
                    {
                        BotManager.MoveBot(player);
                        break;
                    }
                case PRACC_COMMAND.settings:
                    PracticeMode.ShowPracticeMenu(player);
                    break;
                case PRACC_COMMAND.editnade:
                    if (args.Length > 0)
                    {
                        var useNadeWizard = CSPraccPlugin.Instance.Config.UseNadeWizard;
                        if (int.TryParse(args, out int id))
                        {
                            ProjectileManager.SetLastAddedProjectileSnapshot(player.SteamID, id);
                            if (useNadeWizard)
                            {
                                PracticeMode.ShowNadeWizardMenu(player, id);
                            }

                            break;
                        }
                        Utils.ClientChatMessage("Invalid parameter id", player);                       
                    }    
                    else
                    {
                        var useNadeWizard = CSPraccPlugin.Instance.Config.UseNadeWizard;
                        KeyValuePair<int, ProjectileSnapshot> lastSnapshot = ProjectileManager.getLastAddedProjectileSnapshot(player.SteamID);
                        ProjectileManager.SetLastAddedProjectileSnapshot(player.SteamID, lastSnapshot.Key);
                        if (useNadeWizard)
                        {
                            PracticeMode.ShowNadeWizardMenu(player, lastSnapshot.Key);
                        }
                        break;
                    }
                    break;
                case PRACC_COMMAND.showtags:
                    PracticeMode.ShowTagsMenu(player);
                    break;
                case PRACC_COMMAND.find:
                    PracticeMode.ShowPlayerBasedNadeMenu(player, "", args);
                    break;
                case PRACC_COMMAND.breakstuff:
                    Utils.BreakAll();
                    break;
                case PRACC_COMMAND.impacts:
                    ConVar? showimpacts = ConVar.Find("sv_showimpacts");
                    if (showimpacts == null) return false;
                    int currVal = showimpacts.GetPrimitiveValue<int>();
                    if(currVal != 0)
                    {
                        Utils.ServerMessage("Disabling impacts.");
                        Server.ExecuteCommand("sv_showimpacts 0");
                    }
                    else
                    {
                        Utils.ServerMessage("Enabling impacts.");
                        Server.ExecuteCommand("sv_showimpacts 1");
                    }
                    break;
                case PRACC_COMMAND.mimic_menu:
                    PracticeMode.ShowMimicMenu(player);
                    break;
                case PRACC_COMMAND.create_replay:
                    PracticeMode.CreateReplay(player, args);
                    break;
                case PRACC_COMMAND.record_role:
                    PracticeMode.Record(player,args);
                    break;
                case PRACC_COMMAND.stoprecord:
                    PracticeMode.StopRecord(player);
                    break;
                case PRACC_COMMAND.store_replay:
                    PracticeMode.StoreLastReplay(player);
                    break;
                case PRACC_COMMAND.rename_replayset:
                    PracticeMode.RenameCurrentReplaySet(player, args);
                    break;
                case PRACC_COMMAND.replay_menu:
                    PracticeMode.ShowMimcReplays(player);
                    break;
                default:
                    {
                        base.PlayerChat(@event, info);
                        return false;
                    }
            }
            return true;
        }

        public override void Dispose()
        {
            CSPraccPlugin.Instance.RemoveCommand("css_pracc_smokecolor_enabled", PraccSmokecolorEnabled);
            base.Dispose();
        }

        public override void PrintHelp(CCSPlayerController? player, string args = "")
        {
            base.PrintHelp(player, args);
            List<string> message = new List<string>();
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.SPAWN}{ChatColors.Red} 'number'{ChatColors.White}. Teleports you to the given spawn of your current team.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.TSPAWN}{ChatColors.White}{ChatColors.Red} 'number'{ChatColors.White}. Teleports you to the given spawn of the terrorist team.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.CTSPAWN}{ChatColors.White}{ChatColors.Red} 'number'{ChatColors.White}. Teleports you to the given spawn of the counter-terrorist team.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.bestspawn}{ChatColors.White} Go to the closest spawn of your position.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.worstspawn}{ChatColors.White} Go to the worst spawn of your position.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.NADES}{ChatColors.White}{ChatColors.Blue} [id]{ChatColors.White}. If id is passed an available. Teleport to given nade lineup. Else open nade menu.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.find}{ChatColors.White}{ChatColors.Red} 'name'{ChatColors.White}. Open nade menu containing nades which contain the given keyword.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.SAVE}{ChatColors.White}{ChatColors.Red} 'name,description'{ChatColors.White}. Saves current lineup. Description is optional.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.settings}{ChatColors.White} Switch between global or personalized nade menu.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.Last}{ChatColors.White} Goto last thrown grenade position.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.BACK}{ChatColors.White}{ChatColors.Blue} [number]{ChatColors.White} Go back in your grenade history, can give a number on how many positions.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.forward}{ChatColors.White}{ChatColors.Blue} [number]{ChatColors.White} Go forward in your grenade history, can give a number on how many positions.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.Rename}{ChatColors.White}{ChatColors.Red} 'new name'{ChatColors.White} Re-name last saved grenade.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.editnade}{ChatColors.White}{ChatColors.Red} 'nade id'{ChatColors.White} Select the specified nade to using editing commands.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.Description}{ChatColors.White}{ChatColors.Red} 'decription'{ChatColors.White} Add description to your last saved grenade.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.AddTag}{ChatColors.White}{ChatColors.Red} 'tag'{ChatColors.White} Add a tag to your last saved grenade.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.RemoveTag}{ChatColors.White}{ChatColors.Red} 'tag'{ChatColors.White} Remove a tag from your last saved grenade.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.ClearTags}{ChatColors.White} Remove all tags from your last saved grenade.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.DeleteTag}{ChatColors.White}{ChatColors.Red} 'tag'{ChatColors.White} Delete a tag from all of your nades.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.showtags}{ChatColors.White} Show all available tags from your currently selected nade menu. To change the nade menu use .settings .");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.rethrow}{ChatColors.White} Rethrows your last grenade.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.BOT}{ChatColors.White} Spawns bot at your current location.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.BOOST}{ChatColors.White} Spawns bot at your current location and teleports you ontop.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.CROUCHBOT}{ChatColors.White} Spawns a croucing bot at your current location.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.CROUCHBOOST}{ChatColors.White} Spawns a croucing bot at your current location and teleports you ontop.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.NOBOT}{ChatColors.White} Removes closest bot to your location that you spawned.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.SwapBot}{ChatColors.White} Swap your current position with the closest bot.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.MoveBot}{ChatColors.White} Change the position of the last added bot to your current one.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.CLEARBOTS}{ChatColors.White} Clears all of your bots.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.CLEAR}{ChatColors.White} Clear your smokes / molotovs.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.ClearAll}{ChatColors.White} Clear all smokes / molotovs on the server");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.flash}{ChatColors.White} Save current position, if flash is thrown, then you will be teleported back to the position.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.stop}{ChatColors.White} Stop the flash command.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.noflash}{ChatColors.White} Removing flash effect.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.timer}{ChatColors.White} Toggle Timer. Use command again to stop timer.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.countdown}{ChatColors.Red} 'time in seconds'{ChatColors.White}. Run countdown with given parameter.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.CHECKPOINT}{ChatColors.White} Save current position as checkpoint. Use .back to return.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.TELEPORT}{ChatColors.White} Return to saved checkpoint.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.WATCHME}{ChatColors.White} Moves all players except you to spectator.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.breakstuff}{ChatColors.White} Breaks all breakable stuff.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.impacts}{ChatColors.White} Toggle impacts.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.mimic_menu}{ChatColors.White} Open the bot mimic menu.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.create_replay}{ChatColors.White}{ChatColors.Blue} [replayset name]{ChatColors.White} Creates a new replayset.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.record_role}{ChatColors.White}{ChatColors.Blue} [role name]{ChatColors.White} Records a new role.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.stoprecord}{ChatColors.White} Stops the current recording.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.store_replay}{ChatColors.White} Save the last recording permanently.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.rename_replayset}{ChatColors.White}{ChatColors.Red} 'new name'{ChatColors.White} Rename the current replay set.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.replay_menu}{ChatColors.White} Open menu with all replays.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.ADDROLE}{ChatColors.White}{ChatColors.Red} 'name'{ChatColors.White}. Add a gameplay role to the nade. Creates a new role is the role does not exist.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.ADDROLE}{ChatColors.White}{ChatColors.Red} 'name'{ChatColors.White}. Remove a gameplay role from the nade.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.SHOWROLES}{ChatColors.White} Show all available gameplay roles.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.ADDSTRAT}{ChatColors.White}{ChatColors.Red} 'name'{ChatColors.White}. Add a strat to the nade. Creates a new strat is the strat does not exist.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.REMOVESTRAT}{ChatColors.White}{ChatColors.Red} 'name'{ChatColors.White}. Remove a strat from the nade.");
            message.Add($" {ChatColors.Green}{PRACC_COMMAND.SHOWSTRAT}{ChatColors.White} Show all available strats.");

            foreach (string s in message)
            {
                if (args == "" || s.ToLower().Contains(args.ToLower()))
                {
                    player?.PrintToChat(s);
                }
            }
        }
    }
}
