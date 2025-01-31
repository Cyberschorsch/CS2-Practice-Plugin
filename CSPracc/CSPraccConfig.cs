﻿using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CSPracc.DataModules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static CSPracc.DataModules.Enums;

namespace CSPracc
{
    public class CSPraccConfig : BasePluginConfig
    {
        [JsonPropertyName("EnableLogging")] public bool Logging { get; set; } = false;
        [JsonPropertyName("ChatPrefix")] public string ChatPrefix { get; set; } = $" {ChatColors.Green}[{ChatColors.Red}CSPRACC{ChatColors.Green}]{ChatColors.White} ";
        [JsonPropertyName("RconPassword")] public string RconPassword { get; set; } = "secret";
        [JsonPropertyName("DemoSettings")] public DemoManagerSettings DemoManagerSettings { get; set; } = new DemoManagerSettings();
        [JsonPropertyName("StandardMode")] public PluginMode ModeToLoad { get; set; } = PluginMode.Base;
        [JsonPropertyName("AdminRequirement")] public bool AdminRequirement { get; set; } = true;

        [JsonPropertyName("MapChangeDelay")] public int DelayMapChange { get; set; } = 3;

        [JsonPropertyName("UsePersonalNadeMenu")] public bool UsePersonalNadeMenu { get; set; } = true;
        
        [JsonPropertyName("UseNadeWizard")] public bool UseNadeWizard { get; set; } = true;
        
        [JsonPropertyName("FilterByTeam")] public bool FilterByTeam { get; set; } = true;
        [JsonPropertyName("AlwaysShowNadesWithoutTeam")] public bool AlwaysShowNadesWithoutTeam { get; set; } = true;
    }
}
