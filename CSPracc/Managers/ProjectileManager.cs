using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CounterStrikeSharp.API.Modules.Utils;
using CSPracc.DataModules;
using CSPracc.CommandHandler;
using CounterStrikeSharp.API.Modules.Entities;
using System.Xml.Linq;
using CSPracc.Extensions;
using CSPracc.DataModules.Constants;
using CSPracc.Modes;
using CSPracc.DataStorages.JsonStorages;
using System.Reflection;
using System.Numerics;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using System.Globalization;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using Microsoft.Extensions.Logging;
using static System.Formats.Asn1.AsnWriter;
using CounterStrikeSharp.API.Modules.Memory;
using System.ComponentModel;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System.Reflection.Metadata;
using CSPracc.Managers;
using T3MenuSharedApi;

namespace CSPracc
{
    public class ProjectileManager : IDisposable
    {
        
        public IT3MenuManager? MenuManager;
        public static PluginCapability<IT3MenuManager> IT3MenuCapability { get; } = new("t3menu:manager");
        
        /// <summary>
        /// Save history of thrown grenades
        /// </summary>
        public Dictionary<ulong, List<ProjectileSnapshot>> LastThrownGrenade
        {
            get; set;
        } = new Dictionary<ulong, List<ProjectileSnapshot>>();
        /// <summary>
        /// List of plugin re-thrown grenades
        /// </summary>
        public List<CBaseCSGrenadeProjectile?> SelfThrownGrenade
        {
            get; set;
        } = new List<CBaseCSGrenadeProjectile?>();
        /// <summary>
        /// Last thrown smokes for printing timings
        /// </summary>
        public Dictionary<int, DateTime> LastThrownSmoke
        {
            get; set;
        } = new Dictionary<int, DateTime>();
        /// <summary>
        /// Saved positions for .flash command
        /// </summary>
        Dictionary<ulong, Position> FlashPosition
        {
            get; set;
        } = new Dictionary<ulong, Position>();
        /// <summary>
        /// List of players to remove blinding effect from
        /// </summary>
        public List<ulong> NoFlashList
        {
            get; set;
        } = new List<ulong>();
        /// <summary>
        /// Last nade a player added
        /// </summary>
        Dictionary<ulong, int> lastSavedNade
        {
            get; set;
        } = new Dictionary<ulong, int>();

        /// <summary>
        /// When using .back or .forward the int is set to the position where the player currently is in the last thrown grenades
        /// </summary>
        Dictionary<ulong, int> playerGrenadeHistorePosition
        {
            get; set;
        } = new Dictionary<ulong, int>();


        /// <summary>
        /// Stored nades
        /// </summary>
        protected Dictionary<string, ProjectileStorage> projectileStorages { get; init; } = new Dictionary<string, ProjectileStorage>();
        /// <summary>
        /// Gets Projectile Storage for current map
        /// </summary>
        protected ProjectileStorage CurrentProjectileStorage
        {
            get
            {
                return GetOrAddProjectileStorage(Server.MapName);
            }
        }
        public ProjectileManager()
        {
            //CSPraccPlugin.Instance.RegisterListener<Listeners.OnTick>(OnTick);
        }

                
        // get the instance
        public IT3MenuManager? GetMenuManager()
        {
            if (MenuManager == null)
                MenuManager = IT3MenuCapability.Get();

            return MenuManager;
        }

        public void Dispose()
        {
            
        }
        /// <summary>
        /// Gets or Adds Projectile Storage for given map
        /// </summary>
        /// <param name="mapName">Map name</param>
        /// <returns>Projectile Storage for given map</returns>
        protected ProjectileStorage GetOrAddProjectileStorage(string mapName)
        {
            if (!projectileStorages.ContainsKey(mapName))
            {
                //TODO: Get Directory from config.
                projectileStorages.Add(mapName, new ProjectileStorage(new DirectoryInfo(Path.Combine(CSPraccPlugin.ModuleDir.FullName, "Projectiles"))));
            }
            return projectileStorages[mapName];
        }

        /// <summary>
        /// Create nade menu
        /// </summary>
        /// <param name="player">player who called the nade menu</param>
        /// <returns></returns>
        public HtmlMenu GetNadeMenu(CCSPlayerController player)
        {
            List<KeyValuePair<string, Action>> nadeOptions = new List<KeyValuePair<string, Action>>();


            ProjectileSnapshot? latestSnapshot = getLatestProjectileSnapshot(player.SteamID);
            if (latestSnapshot != null)
            {
                nadeOptions.Add(new KeyValuePair<string, Action>($"Your last thrown projectile", new Action(() => RestorePlayersLastThrownGrenade(player))));
            }

            foreach (KeyValuePair<int, ProjectileSnapshot> entry in CurrentProjectileStorage.GetAll())
            {
                nadeOptions.Add(new KeyValuePair<string, Action>($"{entry.Value.Title} ID:{entry.Key}", new Action(() =>

                {

                    RestoreSnapshot(player, entry.Key);
                    SetLastAddedProjectileSnapshot(player.SteamID, entry.Key);
                    })));
            }
            HtmlMenu htmlNadeMenu = new HtmlMenu("Nade Menu", nadeOptions, false); ;
            return htmlNadeMenu;
        }

        public List<string> GetAllTags()
        {
            // Loop through all nades to get the available tags.
            var nades = CurrentProjectileStorage.GetAll();
            var tags = new HashSet<string>();

            foreach (KeyValuePair<int, ProjectileSnapshot> nade in nades)
            {
                foreach (string tag in nade.Value.Tags)
                {
                    if (!tags.Contains(tag))
                        tags.Add(tag);
                }
            }

            return tags.ToList();
        }
        
        public List<string> GetAllRoles()
        {
            // Loop through all nades to get the available tags.
            var nades = CurrentProjectileStorage.GetAll();
            var roles = new HashSet<string>();

            foreach (KeyValuePair<int, ProjectileSnapshot> nade in nades)
            {
                foreach (string role in nade.Value.Roles)
                {
                    roles.Add(role);
                }
            }

            return roles.ToList();
        }
        
        public List<string> GetAllStrats()
        {
            // Loop through all nades to get the available tags.
            var nades = CurrentProjectileStorage.GetAll();
            var strats = new HashSet<string>();

            foreach (KeyValuePair<int, ProjectileSnapshot> nade in nades)
            {
                foreach (string strat in nade.Value.Strats)
                {
                    if (!strats.Contains(strat))
                        strats.Add(strat);
                }
            }

            return strats.ToList();
        }

        public ProjectileSnapshot GetNadeById(int id)
        {
            CurrentProjectileStorage.Get(id, out ProjectileSnapshot? snapshot);
            return snapshot;
        }

        public List<KeyValuePair<int,ProjectileSnapshot>> GetNades(CCSPlayerController player, Dictionary<string, object> properties)
        {
            string tag = properties.ContainsKey("tag") ? properties["tag"].ToString().ToLower() : "";
            string role = properties.ContainsKey("role") ? properties["role"].ToString().ToLower() : "";
            string strat = properties.ContainsKey("strat") ? properties["strat"].ToString().ToLower() : "";
            bool usePersonalNadeMenu = (bool)properties.ContainsKey("usePersonalNadeMenu") ? (bool)properties["usePersonalNadeMenu"] : false;
            
            player.GetValueOfCookie("Roles", out string? playerRoles);
            CSPraccPlugin.Instance!.Logger.LogInformation($"Player Roles: {playerRoles}");
            
            bool FilterByTeam = CSPraccPlugin.Instance.Config.FilterByTeam;
            CSPraccPlugin.Instance!.Logger.LogInformation($"Filter by Team?: {FilterByTeam}");
            
            bool AlwaysShowNadesWithoutTeam = CSPraccPlugin.Instance.Config.AlwaysShowNadesWithoutTeam;
            CSPraccPlugin.Instance!.Logger.LogInformation($"Always Show Nades without Team?: {AlwaysShowNadesWithoutTeam}");
            
            string name = properties.ContainsKey("name") ? properties["name"].ToString() : "";
            
            List<KeyValuePair<int,ProjectileSnapshot>> nade_result = new List<KeyValuePair<int, ProjectileSnapshot>>();
            List<KeyValuePair<int,ProjectileSnapshot>> nades = new List<KeyValuePair<int, ProjectileSnapshot>>();
            if (usePersonalNadeMenu)
            {
                nades = getAllNadesFromPlayer(player.SteamID);
            }
            else
            {
                nades = CurrentProjectileStorage.GetAll();
            }
            
            // Filter available nades by tag, role, name and team.
            foreach (KeyValuePair<int, ProjectileSnapshot> nade in nades)
            {
                bool filter = true;
                
                if (FilterByTeam)
                {
                    CsTeam currentTeam = player.Team;
                    CsTeam nadeTeam = nade.Value.Team;
                    if (nadeTeam == CsTeam.None && AlwaysShowNadesWithoutTeam)
                    {
                        CSPraccPlugin.Instance!.Logger.LogInformation($"Nade is not assigned to a team but config is set to show the nade.");
                    }
                    else
                    {
                        CSPraccPlugin.Instance!.Logger.LogInformation($"Nade: {nade.Value.Title} Current team / Nade Team: {currentTeam} /  {nadeTeam}");
                        if (currentTeam == nadeTeam)
                        {
                            CSPraccPlugin.Instance!.Logger.LogInformation($"Nade is for the current team.");
                        }
                        else
                        {
                            CSPraccPlugin.Instance!.Logger.LogInformation($"Nade is not for the current team of the player, filtering out.");
                            continue;
                        }
                    }
                }
                
                // Filter by preconfigured roles if not explicitly called via menu.
                if (string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(playerRoles))
                {
                    CSPraccPlugin.Instance!.Logger.LogInformation($"Filter by preset roles.");
                    string[] filterroles = playerRoles.Split(',');
                    bool not_found_any_role = true;
                    foreach (string filterrole in filterroles)
                    {
                        if (snapshotContainRole(nade.Value, filterrole))
                        {
                            not_found_any_role = false;
                        }
                    }
                    if (not_found_any_role)
                    {
                        CSPraccPlugin.Instance!.Logger.LogInformation($"Nade matches preset roles.");
                        filter = false;
                    }
                }
                
                if (!string.IsNullOrEmpty(name))
                    if (!nade.Value.Title.Contains(name))
                        filter = false;
                if (!string.IsNullOrEmpty(tag))
                {
                    if (!snapshotContainTag(nade.Value, tag) && !tag.Equals(""))
                        filter = false;
                }
                if (!string.IsNullOrEmpty(role))
                {
                    if (!snapshotContainRole(nade.Value, role) && !role.Equals(""))
                        filter = false;
                }
                if (!string.IsNullOrEmpty(strat))
                {
                    if (!snapshotContainStrat(nade.Value, strat) && !strat.Equals(""))
                        filter = false;
                }

                if (filter)
                {
                    CSPraccPlugin.Instance!.Logger.LogInformation($"Adding nade.");
                    nade_result.Add(nade);
                }
                    
            }
            
            return nade_result;
        }

        /// <summary>
        /// Create nade menu
        /// </summary>
        /// <param name="player">player who called the nade menu</param>
        /// <returns></returns>
        public IT3Menu GetPlayerBasedNadeMenu(CCSPlayerController player, Dictionary<string, object> properties)
        {
            
            string tag = properties.ContainsKey("tag") ? properties["tag"].ToString() : "";
            string role = properties.ContainsKey("role") ? properties["role"].ToString() : "";
            string strat = properties.ContainsKey("strat") ? properties["strat"].ToString() : "";
            bool isSubMenu = (bool)properties.ContainsKey("isSubMenu") ? (bool)properties["isSubMenu"] : false;
            string name = properties.ContainsKey("name") ? properties["name"].ToString() : "";
           
            CSPraccPlugin.Instance!.Logger.LogInformation($"Tag: {tag}");
            CSPraccPlugin.Instance!.Logger.LogInformation($"Role: {role}");
            CSPraccPlugin.Instance!.Logger.LogInformation($"Strat: {strat}");
            CSPraccPlugin.Instance!.Logger.LogInformation($"Name: {name}");
            
            player.GetValueOfCookie("PersonalizedNadeMenu", out string? value);
            
            bool usePersonalNadeMenu = (value == "yes") || (value == null && CSPraccPlugin.Instance!.Config!.UsePersonalNadeMenu) ? true : false;
            
            // Get list of nades filtered by the arguments.
            List<KeyValuePair<int,ProjectileSnapshot>> nades = GetNades(player, properties);

            string MenuTitle = string.Empty;
            var manager = GetMenuManager();
            if (usePersonalNadeMenu)
            {                
                CSPraccPlugin.Instance!.Logger.LogInformation("Personal Menu");
                MenuTitle = "Personal Nade Menu";
            }
            else
            {
                CSPraccPlugin.Instance!.Logger.LogInformation("Global nades menu");
                MenuTitle = "Global Nade Menu";
            }

            var Menu = manager.CreateMenu(MenuTitle, isSubMenu);
            
            foreach (KeyValuePair<int, ProjectileSnapshot> entry in nades)
            {
                Menu.Add(entry.Value.Title, (p, option) =>
                {
                    RestoreSnapshot(player, entry.Key);
                    SetLastAddedProjectileSnapshot(player.SteamID, entry.Key);
                    manager.CloseMenu(player);
                });
            }

            return Menu;
        }     

        private bool snapshotContainTag(ProjectileSnapshot snapshot, string tagToSearch)
        {
            foreach(string tag in snapshot.Tags)
            {
                if(tag.ToLower() == tagToSearch.ToLower()) return true;
            }
            return false;
        }
        
        private bool snapshotContainRole(ProjectileSnapshot snapshot, string roleToSearch)
        {
            CSPraccPlugin.Instance!.Logger.LogInformation($"Looking for role {roleToSearch}");
            
            foreach(string role in snapshot.Roles)
            {
                CSPraccPlugin.Instance!.Logger.LogInformation($"Nade Role: {role}");
                if(role.ToLower() == roleToSearch.ToLower()) return true;
            }
            return false;
        }
        
        private bool snapshotContainStrat(ProjectileSnapshot snapshot, string stratToSearch)
        {
            foreach(string strat in snapshot.Strats)
            {
                if(strat.ToLower() == stratToSearch.ToLower()) return true;
            }
            return false;
        }

        /// <summary>
        /// Restoring the last thrown smoke
        /// </summary>
        /// <param name="player"></param>
        public void RestorePlayersLastThrownGrenade(CCSPlayerController player, int count = 1)
        {
            if (player == null || !player.IsValid) return;

            if (!LastThrownGrenade.TryGetValue(player.SteamID, out List<ProjectileSnapshot>? snapshots))
            {
                Utils.ClientChatMessage($"{ChatColors.Red}Failed to get your last grenades", player);
                return;
            }
            if (snapshots == null)
            {
                Utils.ClientChatMessage($"{ChatColors.Red}You did not throw any grenades yet.", player);
                return;
            }

            if (!playerGrenadeHistorePosition.TryGetValue(player.SteamID, out int pos))
            {
                pos = -1;
                playerGrenadeHistorePosition.SetOrAdd(player.SteamID, pos);
            }
            if (count == -1)
            {
                snapshots[0].Restore(player);
                return;
            }
            pos += count;
            if (pos >= snapshots.Count)
            {
                pos--;
                Utils.ClientChatMessage($"Reached the end of your grenade history, teleporting to the last grenade.", player);               
            }
            ProjectileSnapshot? snapshot = snapshots[pos];
            if (snapshot != null)
            {
                playerGrenadeHistorePosition.SetOrAdd(player.SteamID, pos);
                snapshot.Restore(player);
                return;
            }
            player.PrintToCenter("You did not throw a projectile yet!");
        }

        /// <summary>
        /// Restoring the last thrown smoke
        /// </summary>
        /// <param name="player"></param>
        public void RestoreNextPlayersLastThrownGrenade(CCSPlayerController player, int count = 1)
        {
            if (player == null || !player.IsValid) return;

            if (!LastThrownGrenade.TryGetValue(player.SteamID, out List<ProjectileSnapshot>? snapshots))
            {
                Utils.ClientChatMessage($"{ChatColors.Red}Failed to get your last grenades", player);
                return;
            }
            if (snapshots == null)
            {
                Utils.ClientChatMessage($"{ChatColors.Red}You did not throw any grenades yet.", player);
                return;
            }

            if (!playerGrenadeHistorePosition.TryGetValue(player.SteamID, out int pos))
            {
                pos = 1;
                playerGrenadeHistorePosition.SetOrAdd(player.SteamID, pos);
            }
            pos -= count;
            if (pos < 0)
            {
                pos = 0;
                Utils.ClientChatMessage($"You are at your latest grenade.", player);
            }
            ProjectileSnapshot? snapshot = snapshots[pos];
            if (snapshot != null)
            {
                playerGrenadeHistorePosition.SetOrAdd(player.SteamID, pos);
                snapshot.Restore(player);
                return;
            }
            player.PrintToCenter("You did not throw a projectile yet!");
        }


        private List<KeyValuePair<int, ProjectileSnapshot>> getCurrentPlayerNades(CCSPlayerController player)
        {
            player.GetValueOfCookie("PersonalizedNadeMenu", out string? value);
            string MenuTitle = string.Empty;
            List<KeyValuePair<int, ProjectileSnapshot>> nadeList = new List<KeyValuePair<int, ProjectileSnapshot>>();
            if (value == null)
            {
                Server.PrintToConsole("Could not get cookie");
                if(CSPraccPlugin.Instance.Config.UsePersonalNadeMenu)
                {
                    Server.PrintToConsole("personal nade menu");
                    nadeList = getAllNadesFromPlayer(player.SteamID);
                }
                else
                {
                    Server.PrintToConsole("global");
                    nadeList = CurrentProjectileStorage.GetAll();
                }
                return nadeList;
            }
            if(value == "yes")
            {
                Server.PrintToConsole("Could get cookie");
                nadeList = getAllNadesFromPlayer(player.SteamID);
            }
            else
            {
                nadeList = CurrentProjectileStorage.GetAll();
            }
            return nadeList;
        }

        /// <summary>
        /// Get the last projectilesnapshot a player added
        /// </summary>
        /// <param name="steamId">player</param>
        /// <returns>snapshot</returns>
        public IT3Menu GetTagsMenu(ulong steamId)
        {
            
            CCSPlayerController? player = Utilities.GetPlayerFromSteamId(steamId);

            List<string> tags = new List<string>();
            
            List<KeyValuePair<string, Action>> tagOptions = new List<KeyValuePair<string, Action>>();

            var manager = GetMenuManager();
            var tagMenu = manager.CreateMenu("Available Tags", isSubMenu: false);
            
            player.GetValueOfCookie("PersonalizedNadeMenu", out string? value);
            
            bool usePersonalNadeMenu = (value == "yes") || (value == null && CSPraccPlugin.Instance!.Config!.UsePersonalNadeMenu) ? true : false;
            List<KeyValuePair<int, ProjectileSnapshot>> nades = new List<KeyValuePair<int, ProjectileSnapshot>>();
            
            Dictionary<string, object> getNadesProperties = new Dictionary<string, object>();
            getNadesProperties["usePersonalNadeMenu"] = usePersonalNadeMenu;
            
            nades = GetNades(player, getNadesProperties);

            foreach(KeyValuePair<int,ProjectileSnapshot> nade in nades)
            {
                foreach(string tag in nade.Value.Tags)
                {
                    CSPraccPlugin.Instance!.Logger.LogInformation($"Tag: {tag}");
                    Dictionary<string, object> properties = new Dictionary<string, object>();
                    properties["tag"] = tag;
                    properties["isSubMenu"] = true;
                    
                    var submenu = GetPlayerBasedNadeMenu(player, properties);
                    submenu.ParentMenu = tagMenu;
                    // Build submenu for the nades.
                    tagMenu.Add(tag, (p, option) =>
                    {
                        manager.OpenSubMenu(player, submenu);
                    });
                }
            }
            
            return tagMenu;
        }
        
        public IT3Menu GetRolesMenu(CCSPlayerController player)
        {

            List<string> roles = new List<string>();
            
            List<KeyValuePair<string, Action>> rolesOptions = new List<KeyValuePair<string, Action>>();

            var manager = GetMenuManager();
            var roleMenu = manager.CreateMenu("Available Roles", isSubMenu: false);
            
            player.GetValueOfCookie("PersonalizedNadeMenu", out string? value);
            List<KeyValuePair<int, ProjectileSnapshot>> nadelist = new List<KeyValuePair<int, ProjectileSnapshot>>();
            if (value == null || value == "yes")
            {
                nadelist = getAllNadesFromPlayer(player.SteamID);
            }
            else
            {
                nadelist = CurrentProjectileStorage.GetAll();
            }

            foreach(KeyValuePair<int,ProjectileSnapshot> nade in nadelist)
            {
                foreach(string role in nade.Value.Roles)
                {
                    CSPraccPlugin.Instance!.Logger.LogInformation($"Role: {role}");
                    
                    Dictionary<string, object> properties = new Dictionary<string, object>();
                    if (!roles.Contains(role))
                    {
                        properties["role"] = role;
                        properties["isSubMenu"] = true;
                    
                        var submenu = GetPlayerBasedNadeMenu(player, properties);
                        submenu.ParentMenu = roleMenu;
                        // Build submenu for the nades.
                        roleMenu.Add(role, (p, option) =>
                        {
                            manager.OpenSubMenu(player, submenu);
                        });    
                        roles.Add(role);
                    }
                    
                }
            }
            
            return roleMenu;
        }
        
        public IT3Menu GetStratsMenu(CCSPlayerController player)
        {
            List<string> strats = new List<string>();
            

            var manager = GetMenuManager();
            var stratMenu = manager.CreateMenu("Available Strats", isSubMenu: false);
            
            player.GetValueOfCookie("PersonalizedNadeMenu", out string? value);
            List<KeyValuePair<int, ProjectileSnapshot>> nadelist = new List<KeyValuePair<int, ProjectileSnapshot>>();
            if (value == null || value == "yes")
            {
                nadelist = getAllNadesFromPlayer(player.SteamID);
            }
            else
            {
                nadelist = CurrentProjectileStorage.GetAll();
            }

            foreach(KeyValuePair<int,ProjectileSnapshot> nade in nadelist)
            {
                foreach(string strat in nade.Value.Strats)
                {
                    CSPraccPlugin.Instance!.Logger.LogInformation($"Strat: {strat}");
                    
                    Dictionary<string, object> properties = new Dictionary<string, object>();
                    if (!strats.Contains(strat))
                    {
                        properties["strat"] = strat;
                        properties["isSubMenu"] = true;
                    
                        var submenu = GetPlayerBasedNadeMenu(player, properties);
                        if (submenu.Options.Count > 0)
                        {
                            submenu.ParentMenu = stratMenu;
                            // Build submenu for the nades.
                            stratMenu.Add(strat, (p, option) =>
                            {
                                manager.OpenSubMenu(player, submenu);
                            });
                            strats.Add(strat);    
                        }
                    }
                    
                }
            }
            return stratMenu;
        }


        /// <summary>
        /// Set the last projectilesnapshot a player added
        /// </summary>
        /// <param name="steamId">player</param>
        /// <returns>snapshot</returns>
        public void SetLastAddedProjectileSnapshot(ulong steamId, int snapshotid)
        {
            if(CurrentProjectileStorage.Get(snapshotid, out ProjectileSnapshot? snapshot))
            {
                if(snapshot != null)
                {
                    if(snapshot.initialThrower == 0 || snapshot.initialThrower == steamId)
                    {
                        lastSavedNade.SetOrAdd(steamId, snapshotid);
                        Utils.ClientChatMessage($"You can now edit {snapshot.Title}",steamId);
                    }
                }               
            }
        }
                
        /// <summary>
        /// Get the last projectilesnapshot a player added
        /// </summary>
        /// <param name="steamId">player</param>
        /// <returns>snapshot</returns>
        public KeyValuePair<int, ProjectileSnapshot> getLastAddedProjectileSnapshot(ulong steamId)
        {
            CSPraccPlugin.Instance!.Logger.LogInformation($"steamID: {steamId}");
            CSPraccPlugin.Instance!.Logger.LogInformation($"lastSavedNade: {lastSavedNade}");
            if(!lastSavedNade.TryGetValue(steamId,out int snapshotid))
            {
                Server.PrintToChatAll("could not get snapshotid");
                return new KeyValuePair<int, ProjectileSnapshot>();           
            }
            if(snapshotid == 0) return new KeyValuePair<int, ProjectileSnapshot>();

            CurrentProjectileStorage.Get(snapshotid, out ProjectileSnapshot? snapshot);

            if(snapshot == null) return new KeyValuePair<int, ProjectileSnapshot>();
            if (snapshot.initialThrower != steamId && snapshot.initialThrower != 0)
            {
                return new KeyValuePair<int, ProjectileSnapshot>();
            }
            return new KeyValuePair<int, ProjectileSnapshot>(snapshotid,snapshot);
        }

        private List<KeyValuePair<int,ProjectileSnapshot>> getAllNadesFromPlayer(ulong steamId)
        {
            List<KeyValuePair<int, ProjectileSnapshot>> grenadeList = new List<KeyValuePair<int, ProjectileSnapshot>>();
            foreach(KeyValuePair<int,ProjectileSnapshot> kvp in CurrentProjectileStorage.GetAll())
            {
                if(kvp.Value.initialThrower == steamId)
                {
                    grenadeList.Add(kvp);
                }
            }
            return grenadeList;
        }

        private ProjectileSnapshot? getLatestProjectileSnapshot(ulong steamId)
        {
            if (LastThrownGrenade.TryGetValue(steamId, out List<ProjectileSnapshot>? savedNades))
            {
                if (savedNades != null)
                {
                    ProjectileSnapshot? projectileSnapshot = savedNades.FirstOrDefault();
                    if (projectileSnapshot != null)
                    {
                        return projectileSnapshot;
                    }
                }
            }
            return null;
        }


        /// <summary>
        /// Teleport player to grenade position
        /// </summary>
        /// <param name="player">player to teleport</param>
        /// <param name="grenadeName">grenade destination</param>
        private void RestoreSnapshot(CCSPlayerController player, string grenadeName)
        {
            int index = grenadeName.IndexOf(":");
            if(index == -1)
            {
                //: not found in string
                ProjectileSnapshot? projectileSnapshot = getLatestProjectileSnapshot(player.SteamID);
                if(projectileSnapshot != null)
                {
                    projectileSnapshot.Restore(player);
                }
                player.PrintToCenter($"Could not find id in grenade name {grenadeName}");
                return;
            }
            string idofNade = grenadeName.Substring(index + 1);
            if (!int.TryParse(idofNade, out int snapshotId))
            {
                player.PrintToCenter($"Failed to parse protectile id from {idofNade}");
                return;
            }
            RestoreSnapshot(player, snapshotId);
        }
        internal void RestoreSnapshot(CCSPlayerController player, int snapshotId)
        {
            if (CurrentProjectileStorage.Get(snapshotId, out ProjectileSnapshot snapshot))
            {
                snapshot.Restore(player);
                return;
            }
            else
            {
                player.PrintToCenter($"No projectile found with id {snapshotId}");
                return;
            }
        }
        /// <summary>
        /// Add grenade to the list
        /// </summary>
        /// <param name="player">player who issued the command</param>
        /// <param name="args">Arguments shall look like <Name> <Description></param>
        public void SaveSnapshot(CCSPlayerController player,string args)
        {
            if (player == null) return;
            if (args == String.Empty) return;
            CounterStrikeSharp.API.Modules.Utils.Vector playerPosition = player.PlayerPawn.Value.CBodyComponent!.SceneNode!.AbsOrigin;
            //TODO provide actual projectile Position
            CounterStrikeSharp.API.Modules.Utils.Vector projectilePosition = new CounterStrikeSharp.API.Modules.Utils.Vector();
            QAngle playerAngle = player.PlayerPawn.Value.EyeAngles;
            string name;
            
            var parts = args.Split(',');
            if (parts.Length == 1)
                name = parts[0].Trim();
            else
                name = parts[0].Trim();

            string description;
            if (parts.Length > 1)
                description = parts[1].Trim() ?? "";
            else
                description = "";

            ProjectileSnapshot? snapshotToAdd = getLatestProjectileSnapshot(player.SteamID);
            if(snapshotToAdd == null)
            {
                Utils.ClientChatMessage("Could not save last thrown projectile.",player);
                return;
            }
            snapshotToAdd.Title = name;
            
            var team = player.Team;
            CSPraccPlugin.Instance!.Logger.LogInformation($"Team: {team.ToString()}");
            
            snapshotToAdd.Team = team;
            
            int newid = CurrentProjectileStorage.Add(snapshotToAdd);
            lastSavedNade.SetOrAdd(player.SteamID, newid);        
            player.ChatMessage($"Successfully added grenade {ChatColors.Blue}\"{name}\"{ChatColors.White} at id: {ChatColors.Green}{newid}");
            if (description != String.Empty)
                AddDescription(player.SteamID, description);
        }
        
        /// <summary>
        /// Add grenade to the list
        /// </summary>
        /// <param name="player">player who issued the command</param>
        /// <param name="args">Arguments shall look like <Name> <Description></param>
        public void RemoveSnapshot(CCSPlayerController player, string args)
        {
            if (player == null) return;
            //if (args == String.Empty) return;
            args = args.Trim();
            int id = -1;
            if(args == String.Empty)
            {
               KeyValuePair<int,ProjectileSnapshot> snapshot = getLastAddedProjectileSnapshot(player.SteamID);
                CurrentProjectileStorage.RemoveKey(snapshot.Key);
                CurrentProjectileStorage.Save();
                Utils.ClientChatMessage($"Removed the last added grenade: {snapshot.Value.Title}",player);
                return;
            }
            try
            {
                id = Convert.ToInt32(args);
            }
            catch
            {
                player.PrintToCenter("invalid argument, needs to be a number");
                return;
            }
            if(!CurrentProjectileStorage.ContainsKey(id))
            {
                player.PrintToCenter($"Projectile with id {id} does not exist on current map");
                return;
            }
            if (CurrentProjectileStorage.RemoveKey(id))
            {
                player.PrintToCenter($"Successfully removed projectile with id {id}");
            }
            else
            {
                player.PrintToCenter($"Failed to remove projectile with id {id}");
            }

        }
        public static MemoryFunctionWithReturn<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int> CSmokeGrenadeProjectile_CreateFuncWindows = new(@"\x48\x89\x5C\x24\x08\x48\x89\x6C\x24\x10\x48\x89\x74\x24\x18\x57\x41\x56\x41\x57\x48\x83\xEC\x50\x4C\x8B\xB4\x24\x90\x00\x00\x00\x49\x8B\xF8");

        public static MemoryFunctionWithReturn<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int> CSmokeGrenadeProjectile_CreateFuncLinux = new(@"\x55\x4c\x89\xc1\x48\x89\xe5\x41\x57\x41\x56\x49\x89\xd6\x48\x89\xf2\x48\x89\xfe\x41\x55\x45\x89\xcd\x41\x54\x4d\x89\xc4\x53\x48\x83\xec\x28\x48\x89\x7d\xb8\x48");
        public void OnEntitySpawned(CEntityInstance entity)
        {
            if(entity == null) return;
            if (!entity.IsProjectile())
            {
                return;
            }
            CBaseCSGrenadeProjectile projectile;

            switch (entity.Entity!.DesignerName){
                case (DesignerNames.ProjectileSmoke):
                    {
                        projectile = new CSmokeGrenadeProjectile(entity.Handle);
                        break;
                    }
                default:
                    {
                        projectile = new CBaseCSGrenadeProjectile(entity.Handle);
                        break;
                    }
            };

                Server.NextFrame(() =>
                {
                    CCSPlayerController player = new CCSPlayerController(projectile.Thrower.Value.Controller.Value.Handle);
                    CounterStrikeSharp.API.Modules.Utils.Vector playerPosition = player.PlayerPawn.Value.CBodyComponent!.SceneNode!.AbsOrigin;
                    //TODO provide actual projectile Position
                    CounterStrikeSharp.API.Modules.Utils.Vector projectilePosition = new CounterStrikeSharp.API.Modules.Utils.Vector();
                    QAngle playerAngle = player.PlayerPawn.Value.EyeAngles;
                    string name = "LastThrown";
                    //TODO parse actual description if provided
                    string description = "";
                    GrenadeType_t type = GrenadeType_t.GRENADE_TYPE_SMOKE;
                    switch(projectile.DesignerName)
                    {
                        case DesignerNames.ProjectileSmoke:
                            {
                               
                                type = GrenadeType_t.GRENADE_TYPE_SMOKE;
                                break;
                            }
                        case DesignerNames.ProjectileFlashbang:
                            {
                                type = GrenadeType_t.GRENADE_TYPE_FLASH;
                                TpToFlashPos(player);
                                break;
                            }
                        case DesignerNames.ProjectileHE:
                            {
                                type = GrenadeType_t.GRENADE_TYPE_EXPLOSIVE;
                                break;
                            }
                        case DesignerNames.ProjectileMolotov:
                            {
                                type = GrenadeType_t.GRENADE_TYPE_FIRE;
                                break;
                            }
                        case DesignerNames.ProjectileDecoy:
                            {
                                type = GrenadeType_t.GRENADE_TYPE_DECOY;
                                break;
                            }
                        default :
                            {
                                type = GrenadeType_t.GRENADE_TYPE_SMOKE;
                                break;
                            }
                            
                    }                                   
                    if ( projectile.Globalname != "custom")
                    {                       
                        ProjectileSnapshot tmpSnapshot = new ProjectileSnapshot(playerPosition.ToVector3(), projectile.InitialPosition.ToVector3(), playerAngle.ToVector3(), projectile.InitialVelocity.ToVector3(), name, description, type,player.SteamID);
                        List<ProjectileSnapshot>? projectileSnapshots = new List<ProjectileSnapshot>();                        
                        if (LastThrownGrenade.ContainsKey((player.SteamID)) && LastThrownGrenade.TryGetValue(player.SteamID, out projectileSnapshots))
                        {
                            if(projectileSnapshots== null)
                            {
                                projectileSnapshots = new List<ProjectileSnapshot>();
                            }
                            ProjectileSnapshot? projectileSnapshot = projectileSnapshots.FirstOrDefault();
                            if(projectileSnapshot != null)
                            {
                                if (projectileSnapshot.ProjectilePosition !=projectile.InitialPosition.ToVector3())
                                {

                                    projectileSnapshots.Insert(0, tmpSnapshot);
                                }
                                else
                                {
                                    projectile.Thrower.Raw = player.PlayerPawn.Raw;
                                    projectile.OriginalThrower.Raw = player.PlayerPawn.Raw;
                                }
                            }
                            else
                            {
                                projectileSnapshots.Insert(0, tmpSnapshot);
                            }                                              
                        }
                        else
                        {
                            LastThrownGrenade.SetOrAdd(player.SteamID, new List<ProjectileSnapshot>() { tmpSnapshot });
                        }                      
                    }

                });

            if (!PracticeCommandHandler.PraccSmokeColorEnabled) return;
            if (projectile is CSmokeGrenadeProjectile)
            {
                Server.NextFrame(() =>
                {
                    CSmokeGrenadeProjectile smokeProjectile = (CSmokeGrenadeProjectile)projectile;
                    CCSPlayerController player = new CCSPlayerController(projectile.Thrower.Value.Controller.Value.Handle);
                    smokeProjectile.SmokeColor.X = (float)Utils.GetTeamColor(player).R;
                    smokeProjectile.SmokeColor.Y = (float)Utils.GetTeamColor(player).G;
                    if(LastThrownSmoke.ContainsKey(((int)projectile.Index)))
                    {
                        LastThrownSmoke[(int)projectile.Index] = DateTime.Now;
                    }
                    else
                    {
                        LastThrownSmoke.Add((int)projectile.Index, DateTime.Now);
                    }                    
                });
            }           
        }

        public HookResult OnSmokeDetonate(EventSmokegrenadeDetonate @event, GameEventInfo info)
        {
            if(LastThrownSmoke.TryGetValue(@event.Entityid, out var result)) 
            {
                
               CSmokeGrenadeProjectile projectile =  Utilities.GetEntityFromIndex<CSmokeGrenadeProjectile>(@event.Entityid);
                if(projectile != null)
                {
                    Utils.ServerMessage($"{ChatColors.Blue}{@event.Userid.PlayerName}{ChatColors.White}'s smoke Airtime: {ChatColors.Green}{(DateTime.Now - result).TotalSeconds.ToString("0.00")}{ChatColors.White}s Bounces: {ChatColors.Green}{projectile.Bounces}{ChatColors.White}");
                }
                else
                {
                    Utils.ServerMessage($"{@event.Userid.PlayerName}'s smoke Airtime: {(DateTime.Now - result).TotalSeconds.ToString("0.00")}s");
                }
                
            }
            return HookResult.Continue;
        }


        public void SaveLastGrenade(CCSPlayerController playerController, string name)
        {
            ProjectileSnapshot? snapshot = getLatestProjectileSnapshot(playerController.SteamID);
            if(snapshot == null)
            {
                return;
            }
            snapshot.Title = name;
            CurrentProjectileStorage.Add(snapshot);
            playerController.PrintToCenter($"Successfully added grenade {name}");           
        }

        /// <summary>
        /// Rethrowing last grenade
        /// Smokes are currently not detonating, that why they are disabled for now.
        /// </summary>
        /// <param name="player">player who issued the command</param>
        public void ReThrow(CCSPlayerController player, string tag = "")
        {
            List<KeyValuePair<int, ProjectileSnapshot>> nades = getCurrentPlayerNades(player);

            tag = tag.Trim().ToLower();
            if (string.IsNullOrEmpty(tag))
            {
                ProjectileSnapshot? grenade = getLatestProjectileSnapshot(player.SteamID);
                if (grenade == null)
                {
                    player.PrintToCenter("Could not get last thrown nade");
                    return;
                }
                if (grenade == null)
                {
                    player.PrintToCenter("Could not get last thrown nade");
                    return;
                }
                if (!ThrowGrenadePojectile(grenade, player))
                {
                    Utils.ClientChatMessage("Encountered error while throwing your last grenade.", player);
                    return;
                }
                Utils.ClientChatMessage("Rethrowing your last grenade.", player);
                return;
            }
            else if (tag.StartsWith("id:"))
            {
                tag = tag[3..];
                if (!int.TryParse(tag, out int id))
                {
                    player.ChatMessage("Could not parse id");
                    return;
                }

                var foundNade = nades.FirstOrDefault(x => x.Key == id).Value;
                if (foundNade == null)
                {
                    player.ChatMessage($"Could not find grenade with id {ChatColors.Red}{id}");
                    return;
                }

                CSPraccPlugin.Instance!.AddTimer(foundNade.Delay, () => ThrowGrenadePojectile(foundNade, player));
                player.ChatMessage($"Threw your grenade {ChatColors.Green}{foundNade.Title}");

                return;
            }
            else if (int.TryParse(tag, out int id))
            {
                var foundNade = nades.FirstOrDefault(x => x.Key == id).Value;
                if (foundNade != null)
                {
                    CSPraccPlugin.Instance!.AddTimer(foundNade.Delay, () => ThrowGrenadePojectile(foundNade, player));
                    player.ChatMessage($"Threw your grenade {ChatColors.Green}{foundNade.Title}");

                    return;
                }
            }

            Utils.ClientChatMessage($"Throwing all grenades containing tag: {ChatColors.Green}{tag}", player);

            foreach (var kvp in nades)
            {
                if (snapshotContainTag(kvp.Value, tag))
                {
                    CSPraccPlugin.Instance!.AddTimer(kvp.Value.Delay, () => ThrowGrenadePojectile(kvp.Value, player));
                }
            }
        }
        public bool ThrowGrenadePojectile(ProjectileSnapshot projectile, CCSPlayerController player)
        {
            CBaseCSGrenadeProjectile? cGrenade = null;
            switch (projectile.GrenadeType_T)
            {
                case GrenadeType_t.GRENADE_TYPE_EXPLOSIVE:
                    {
                        cGrenade = Utilities.CreateEntityByName<CHEGrenadeProjectile>(DesignerNames.ProjectileHE);
                        break;
                    }
                case GrenadeType_t.GRENADE_TYPE_FLASH:
                    {
                        cGrenade = Utilities.CreateEntityByName<CFlashbangProjectile>(DesignerNames.ProjectileFlashbang);
                        break;
                    }
                case GrenadeType_t.GRENADE_TYPE_SMOKE:
                    {
                        cGrenade = Utilities.CreateEntityByName<CSmokeGrenadeProjectile>(DesignerNames.ProjectileSmoke);
                        cGrenade!.IsSmokeGrenade = true;
                        if(OperatingSystem.IsLinux())
                        {
                            CSmokeGrenadeProjectile_CreateFuncLinux.Invoke(
                                projectile.ProjectilePosition.ToCSVector().Handle,
                                projectile.ProjectilePosition.ToCSVector().Handle,
                                projectile.Velocity.ToCSVector().Handle,
                                projectile.Velocity.ToCSVector().Handle,
                                player.Pawn.Value.Handle,
                                45,
                                player.TeamNum
                            );

                        }
                        else if(OperatingSystem.IsWindows())
                        {
                            CSmokeGrenadeProjectile_CreateFuncWindows.Invoke(
                                projectile.ProjectilePosition.ToCSVector().Handle,
                                projectile.ProjectilePosition.ToCSVector().Handle,
                                projectile.Velocity.ToCSVector().Handle,
                                projectile.Velocity.ToCSVector().Handle,
                                player.Pawn.Value.Handle,
                                45,
                                player.TeamNum
                            );
                        }
                        else
                        {
                            Utils.ServerMessage($"{ChatColors.Red}Unknown operating system");
                            return false;
                        }          
                        return true;
                    }
                case GrenadeType_t.GRENADE_TYPE_FIRE:
                    {
                        cGrenade = Utilities.CreateEntityByName<CMolotovProjectile>(DesignerNames.ProjectileMolotov);
                        if(cGrenade != null)
                        cGrenade.SetModel("weapons/models/grenade/incendiary/weapon_incendiarygrenade.vmdl");
                        break;
                    }
                case GrenadeType_t.GRENADE_TYPE_DECOY:
                    {
                        cGrenade = Utilities.CreateEntityByName<CDecoyProjectile>(DesignerNames.ProjectileDecoy);
                        break;
                    }
                default:
                    {
                        cGrenade = Utilities.CreateEntityByName<CSmokeGrenadeProjectile>(DesignerNames.ProjectileSmoke);
                        break;
                    }
            }
            if (cGrenade == null)
            {
                CSPraccPlugin.Instance!.Logger.LogError("grenade entity is  null");
                return false;
            }
            cGrenade.Elasticity = 0.33f;
            cGrenade.IsLive = false;
            cGrenade.DmgRadius = 350.0f;
            cGrenade.Damage = 99.0f;
            cGrenade.InitialPosition.X = projectile.ProjectilePosition.X;
            cGrenade.InitialPosition.Y = projectile.ProjectilePosition.Y;
            cGrenade.InitialPosition.Z = projectile.ProjectilePosition.Z;
            cGrenade.InitialVelocity.X = projectile.Velocity.X;
            cGrenade.InitialVelocity.Y = projectile.Velocity.Y;
            cGrenade.InitialVelocity.Z = projectile.Velocity.Z;
            cGrenade.Teleport(projectile.ProjectilePosition.ToCSVector(), projectile.PlayerAngle.ToCSQAngle(), projectile.Velocity.ToCSVector());

            cGrenade.DispatchSpawn();
            cGrenade.Globalname = "custom";
            cGrenade.AcceptInput("FireUser1", player, player, "");
            cGrenade.AcceptInput("InitializeSpawnFromWorld", null, null, "");
            cGrenade.TeamNum = player.TeamNum;
            cGrenade.Thrower.Raw = player.PlayerPawn.Raw;
            cGrenade.OriginalThrower.Raw = player.PlayerPawn.Raw;
            cGrenade.OwnerEntity.Raw = player.PlayerPawn.Raw;
            SelfThrownGrenade.Add(cGrenade);
            return true;
        }

        /// <summary>
        /// Save current pos, restore it when flash thrown.
        /// </summary>
        /// <param name="player">player who issued the command</param>
        public void Flash(CCSPlayerController? player)
        {
            if (player == null || !player.IsValid)
            {
                return;
            }

            if (!FlashPosition.ContainsKey(player.SteamID))
            {
                FlashPosition.Add(player.SteamID,new Position(player.PlayerPawn.Value.CBodyComponent.SceneNode.AbsOrigin.Copy(),player.PlayerPawn.Value.EyeAngles.Copy()));
            }
            else
            {
                FlashPosition[player.SteamID] = new Position(player.PlayerPawn.Value.CBodyComponent.SceneNode.AbsOrigin.Copy(), player.PlayerPawn.Value.EyeAngles.Copy());
            }
            player.PrintToCenter("In flashing mode. Use .stop to disable flashing mode.");
        }

        public void Stop(CCSPlayerController? player)
        {
            if (player == null || !player.IsValid)
            {
                return;
            }
            if (FlashPosition.ContainsKey(player.SteamID)) 
            { 
                FlashPosition.Remove(player.SteamID);
                player.PrintToCenter("Stopped flashing mode.");
            }
        }

        private void TpToFlashPos(CCSPlayerController? player)
        {
            if (player == null || !player.IsValid)
            {
                return;
            }

            if(!FlashPosition.TryGetValue(player.SteamID,out Position? pos))
            {
                return;
            }
            if (pos == null) return;

            player.PlayerPawn.Value!.Teleport(pos.PlayerPosition, pos.PlayerAngle, new Vector(0,0,0));
        }


        /// <summary>
        /// Disabling flash effect
        /// </summary>
        /// <param name="player">player who issued the command</param>
        public void NoFlash(CCSPlayerController? player)
        {
            if (player == null || !player.IsValid)
            {
                return;
            }
            if(!NoFlashList.Contains(player.SteamID))
            {
                NoFlashList.Add(player.SteamID);
                Server.NextFrame(() => player.PlayerPawn.Value.FlashMaxAlpha = 0.5f);
                player.HtmlMessage($"No flash: <font color='#008000'>enabled</font>", 2);               
            }
            else
            {
                NoFlashList.Remove(player.SteamID);
                player.HtmlMessage("No flash: <font color='#008000'>disabled</font>", 2);
            }
        }

        /// <summary>
        /// Adds description to your last saved nade
        /// </summary>
        /// <param name="steamId">player who issued the command</param>
        /// <param name="description">description</param>
        public void AddDescription(ulong steamId, string description)
        {
            KeyValuePair<int,ProjectileSnapshot> lastSnapshot = getLastAddedProjectileSnapshot(steamId);
            if(lastSnapshot.Key != 0)
            {             
                if(lastSnapshot.Value != null)
                {
                    lastSnapshot.Value.Description = description;
                    CurrentProjectileStorage.SetOrAdd(lastSnapshot.Key, lastSnapshot.Value);
                    Utils.ClientChatMessage($"Updating grenade description to {description}", steamId);
                }               
            }
        }

        /// <summary>
        /// Adds description to your last saved nade
        /// </summary>
        /// <param name="steamId">player who issued the command</param>
        /// <param name="description">description</param>
        public void SetDelay(ulong steamId, string delay)
        {
            KeyValuePair<int, ProjectileSnapshot> lastSnapshot = getLastAddedProjectileSnapshot(steamId);
            if (lastSnapshot.Key != 0)
            {
                if (lastSnapshot.Value != null)
                {
                    if(!float.TryParse(delay, out float delayInSeconds))
                    {
                        Utils.ClientChatMessage($"Could not parse delay.", steamId);
                    }
                    lastSnapshot.Value.Delay = delayInSeconds;
                    CurrentProjectileStorage.SetOrAdd(lastSnapshot.Key, lastSnapshot.Value);
                    Utils.ClientChatMessage($"Updating grenade delay to {delayInSeconds}", steamId);
                }
            }
        }

        /// <summary>
        /// Adds description to your last saved nade
        /// </summary>
        /// <param name="steamId">player who issued the command</param>
        /// <param name="title">description</param>
        public void RenameLastSnapshot(ulong steamId, string title)
        {
            KeyValuePair<int, ProjectileSnapshot> lastSnapshot = getLastAddedProjectileSnapshot(steamId);
            if (lastSnapshot.Key != 0)
            {
                if (lastSnapshot.Value != null)
                {
                    lastSnapshot.Value.Title = title;                   
                    CurrentProjectileStorage.SetOrAdd(lastSnapshot.Key, lastSnapshot.Value); 
                    Utils.ClientChatMessage($"Updating grenade name to {title}", steamId);

                }
            }
        }

        /// <summary>
        /// Adds description to your last saved nade
        /// </summary>
        /// <param name="steamId">player who issued the command</param>
        /// <param name="title">description</param>
        public void UpdatePosition(CCSPlayerController playerController)
        {           
            KeyValuePair<int, ProjectileSnapshot> lastSnapshot = getLastAddedProjectileSnapshot(playerController.SteamID);
            if (lastSnapshot.Key != 0)
            {
                if (lastSnapshot.Value != null)
                {
                    lastSnapshot.Value.PlayerPosition = playerController.GetCurrentPosition()!.PlayerPosition.ToVector3();
                    CurrentProjectileStorage.SetOrAdd(lastSnapshot.Key, lastSnapshot.Value);
                    Utils.ClientChatMessage($"Updating player position for your current nade.", playerController);

                }
            }
        }

        public void AddTagToLastGrenade(CCSPlayerController player, string tag)
        {
            if (int.TryParse(tag, out int _)) 
            {
                player.ChatMessage("Cannot use a number as tag");
                return;
            }
            KeyValuePair<int, ProjectileSnapshot> lastSnapshot = getLastAddedProjectileSnapshot(player.SteamID);
            if (lastSnapshot.Key != 0)
            {
                if (lastSnapshot.Value != null)
                {
                    if(snapshotContainTag(lastSnapshot.Value,tag))
                    {
                        Utils.ClientChatMessage($"Grenade already contains tag {tag}", player.SteamID);
                        return;
                    }
                    lastSnapshot.Value.Tags.Add(tag);
                    CurrentProjectileStorage.SetOrAdd(lastSnapshot.Key, lastSnapshot.Value);
                    Utils.ClientChatMessage($"Added tag {tag}  to {lastSnapshot.Value.Title}", player.SteamID);
                }
            }
        }

        public void RemoveTagFromLastGrenade(ulong steamid, string tag)
        {
            KeyValuePair<int, ProjectileSnapshot> lastSnapshot = getLastAddedProjectileSnapshot(steamid);
            if (lastSnapshot.Key != 0)
            {
                if (lastSnapshot.Value != null)
                {
                    bool foundTag = false;
                    foreach(string tagToDelete in lastSnapshot.Value.Tags)
                    {
                        if(tagToDelete.ToLower() == tag.ToLower())
                        {
                            foundTag = true;
                            lastSnapshot.Value.Tags.Remove(tagToDelete);
                            break;
                        }
                    }
                   if(foundTag)
                    {
                        CurrentProjectileStorage.SetOrAdd(lastSnapshot.Key, lastSnapshot.Value);
                        Utils.ClientChatMessage($"Removed tag {tag} from {lastSnapshot.Value.Title}", steamid);
                    }
                   
                }
            }
        }

        public void ClearTagsFromLastGrenade(ulong steamid)
        {
            KeyValuePair<int, ProjectileSnapshot> lastSnapshot = getLastAddedProjectileSnapshot(steamid);
            if (lastSnapshot.Key != 0)
            {
                if (lastSnapshot.Value != null)
                {
                    lastSnapshot.Value.Tags.Clear();
                    CurrentProjectileStorage.SetOrAdd(lastSnapshot.Key, lastSnapshot.Value);
                    Utils.ClientChatMessage($"Removed alls tags from {lastSnapshot.Value.Title}", steamid);
                }
            }
        }

        public void DeleteTagFromAllNades(ulong steamid, string tag)
        {
            List<KeyValuePair<int, ProjectileSnapshot>> playerSnapshots = getAllNadesFromPlayer(steamid);
            foreach(KeyValuePair<int, ProjectileSnapshot> kvp in playerSnapshots)
            {
                kvp.Value.Tags.Remove(tag);
                CurrentProjectileStorage.SetOrAdd(kvp.Key, kvp.Value);
            }
            Utils.ClientChatMessage($"Removed tag {tag} from all your grenades", steamid);
        }
        
        public void AddRoleToLastGrenade(CCSPlayerController player, string role)
        {
            KeyValuePair<int, ProjectileSnapshot> lastSnapshot = getLastAddedProjectileSnapshot(player.SteamID);
            if (lastSnapshot.Key != 0)
            {
                if (lastSnapshot.Value != null)
                {
                    if(snapshotContainRole(lastSnapshot.Value, role))
                    {
                        Utils.ClientChatMessage($"Grenade already contains role {role}", player.SteamID);
                        return;
                    }
                    lastSnapshot.Value.Roles.Add(role);
                    CurrentProjectileStorage.SetOrAdd(lastSnapshot.Key, lastSnapshot.Value);
                    Utils.ClientChatMessage($"Added role {role} to {lastSnapshot.Value.Title}", player.SteamID);
                }
            }
        }
        
        public void AddStratToLastGrenade(CCSPlayerController player, string strat)
        {
            KeyValuePair<int, ProjectileSnapshot> lastSnapshot = getLastAddedProjectileSnapshot(player.SteamID);
            if (lastSnapshot.Key != 0)
            {
                if (lastSnapshot.Value != null)
                {
                    if(snapshotContainStrat(lastSnapshot.Value, strat))
                    {
                        Utils.ClientChatMessage($"Grenade already contains strat {strat}", player.SteamID);
                        return;
                    }
                    lastSnapshot.Value.Strats.Add(strat);
                    CurrentProjectileStorage.SetOrAdd(lastSnapshot.Key, lastSnapshot.Value);
                    Utils.ClientChatMessage($"Added strat {strat} to {lastSnapshot.Value.Title}", player.SteamID);
                }
            }
        }
        
        public void SetTeamToLastGrenade(CCSPlayerController player, CsTeam team)
        {
            KeyValuePair<int, ProjectileSnapshot> lastSnapshot = getLastAddedProjectileSnapshot(player.SteamID);
            if (lastSnapshot.Key != 0)
            {
                if (lastSnapshot.Value != null)
                {
                    lastSnapshot.Value.Team = team;
                    CurrentProjectileStorage.SetOrAdd(lastSnapshot.Key, lastSnapshot.Value);
                    Utils.ClientChatMessage($"Set team {team.ToString()} to {lastSnapshot.Value.Title}", player.SteamID);
                }
            }
        }

        public void RemoveRoleFromLastGrenade(CCSPlayerController player, string role)
        {
            KeyValuePair<int, ProjectileSnapshot> lastSnapshot = getLastAddedProjectileSnapshot(player.SteamID);
            if (lastSnapshot.Key != 0)
            {
                if (lastSnapshot.Value != null)
                {
                    bool foundRole = false;
                    foreach(string roleToDelete in lastSnapshot.Value.Roles)
                    {
                        if(roleToDelete.ToLower() == role.ToLower())
                        {
                            foundRole = true;
                            lastSnapshot.Value.Roles.Remove(roleToDelete);
                            break;
                        }
                    }
                    if(foundRole)
                    {
                        CurrentProjectileStorage.SetOrAdd(lastSnapshot.Key, lastSnapshot.Value);
                        Utils.ClientChatMessage($"Removed role {role} from {lastSnapshot.Value.Title}", player.SteamID);
                    }
                   
                }
            }
        }
        
        public void RemoveStratFromLastGrenade(CCSPlayerController player, string strat)
        {
            KeyValuePair<int, ProjectileSnapshot> lastSnapshot = getLastAddedProjectileSnapshot(player.SteamID);
            if (lastSnapshot.Key != 0)
            {
                if (lastSnapshot.Value != null)
                {
                    bool foundStrat = false;
                    foreach(string stratToDelete in lastSnapshot.Value.Strats)
                    {
                        if(stratToDelete.ToLower() == strat.ToLower())
                        {
                            foundStrat = true;
                            lastSnapshot.Value.Strats.Remove(stratToDelete);
                            break;
                        }
                    }
                    if(foundStrat)
                    {
                        CurrentProjectileStorage.SetOrAdd(lastSnapshot.Key, lastSnapshot.Value);
                        Utils.ClientChatMessage($"Removed strat {strat} from {lastSnapshot.Value.Title}", player.SteamID);
                    }
                   
                }
            }
        }

        public void ClearNades(CCSPlayerController player, bool all = false)
        {
            if (player == null || !player.IsValid) return;
            var smokes = Utilities.FindAllEntitiesByDesignerName<CSmokeGrenadeProjectile>("smokegrenade_projectile");
            foreach (var entity in smokes)
            {
                if (entity != null)
                {                  
                    if (entity.Thrower.Value!.Handle == 0)
                    {
                        List<ProjectileSnapshot>? projectileSnapshots = new List<ProjectileSnapshot>();
                        if (LastThrownGrenade.ContainsKey((player.SteamID)) && LastThrownGrenade.TryGetValue(player.SteamID, out projectileSnapshots))
                        {
                            ProjectileSnapshot? projectileSnapshot = projectileSnapshots.FirstOrDefault();
                            if (projectileSnapshot != null)
                            {
                                if (projectileSnapshot.ProjectilePosition != entity.InitialPosition.ToVector3())
                                {
                                    entity.Remove();
                                    continue;
                                }
                            }
                            continue;
                        }
                        continue;
                    }
                    CCSPlayerController? thrower = new CCSPlayerController(entity.Thrower!.Value!.Controller!.Value!.Handle);
                    if (thrower.Handle == player.Handle || all)
                    {
                        entity.Remove();
                    }
                }
            }
                var mollys = Utilities.FindAllEntitiesByDesignerName<CSmokeGrenadeProjectile>("molotov_projectile");
                foreach (var entity in mollys)
                {
                    if (entity != null)
                    {
                        CCSPlayerController thrower = new CCSPlayerController(entity.Thrower.Value.Controller.Value.Handle);
                        if (thrower.Handle == player.Handle || all)
                        {
                            entity.Remove();
                        }
                    }
                }
                var inferno = Utilities.FindAllEntitiesByDesignerName<CSmokeGrenadeProjectile>("inferno");
                foreach (var entity in inferno)
                {
                    CCSPlayerController? thrower = new CCSPlayerController(entity.Thrower.Value.Controller.Value.Handle);
                    if (entity != null)
                    {
                        if (thrower.Handle == player.Handle || all)
                        {
                            entity.Remove();
                        }
                    }
                }
            }
        }
    }
