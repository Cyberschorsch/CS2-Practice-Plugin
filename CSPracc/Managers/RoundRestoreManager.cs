﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CSPracc.DataModules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CSPracc.Managers
{
    public static class RoundRestoreManager
    {
        public static void CleanupOldFiles()
        {
            foreach (FileInfo file in GetBackupFiles())
            {
                file.Delete();
            }
        }
        private static List<FileInfo> GetBackupFiles()
        {
            return CSPraccPlugin.Cs2Dir.GetFiles("backup_round*").ToList();
        }

        public static void OpenBackupMenu(CCSPlayerController player)
        {
            if (player == null) return;
            if (!player.PlayerPawn.IsValid) return;
            if (!player.IsAdmin())
            {
                player.PrintToCenter("Only admins can execute this command!");
                return;
            }
            var backupMenu = new ChatMenu("Backup Menu");
            var handleGive = (CCSPlayerController player, ChatMenuOption option) => LoadSelectedBackup(option.Text);

            List<FileInfo> Backupfiles = GetBackupFiles();

            foreach (var file in Backupfiles)
            {
                string round = file.Name.Substring(file.Name.Length - 6, 2);
                backupMenu.AddMenuOption(round, handleGive);
            }
            ChatMenus.OpenMenu(player, backupMenu);
        }

        private static void LoadSelectedBackup(string Backup)
        {
            List<FileInfo> backups = GetBackupFiles();
            foreach (var backup in backups) 
            { 
                if(backup.Name.Contains(Backup))
                {
                    Server.ExecuteCommand($"mp_backup_restore_load_file {backup.Name}");
                }
            }
            CSPracc.DataModules.consts.Methods.MsgToServer("Restored round, to continue match both teams need to use .unpause");
        }


    }
}