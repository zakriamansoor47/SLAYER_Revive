using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using System.Text.Json.Serialization;
using System.Drawing;
using Microsoft.Extensions.Logging;

namespace SLAYER_Revive;
// Used these to remove compile warnings
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8619
public class SLAYER_ReviveConfig : BasePluginConfig
{
    [JsonPropertyName("PluginEnabled")] public bool PluginEnabled { get; set; } = true;
    [JsonPropertyName("revive_DrawBeacon")] public bool revive_DrawBeacon { get; set; } = true;
    [JsonPropertyName("revive_DrawReviveSign")] public bool revive_DrawReviveSign { get; set; } = true;
    [JsonPropertyName("revive_CountDeath")] public bool revive_CountDeath { get; set; } = true;
    [JsonPropertyName("revive_ReviveLimit")] public int revive_ReviveLimit { get; set; } = 2;
    [JsonPropertyName("revive_ReviveFrag")] public int revive_ReviveFrag { get; set; } = 0;
    [JsonPropertyName("revive_cost_mode")] public int revive_cost_mode { get; set; } = 0;
    [JsonPropertyName("revive_cost_health")] public int revive_cost_health { get; set; } = 10;
    [JsonPropertyName("revive_cost_money")] public int revive_cost_money { get; set; } = 1000;
    [JsonPropertyName("revive_RevivedHealth")] public int revive_RevivedHealth { get; set; } = 100;
    [JsonPropertyName("revive_timer_delay")] public float revive_timer_delay { get; set; } = 5.0f;
    [JsonPropertyName("revive_delay")] public float revive_delay { get; set; } = 15.0f;
    [JsonPropertyName("revive_distance")] public int revive_distance { get; set; } = 150;
    [JsonPropertyName("revive_AdminFlag")] public string revive_AdminFlag { get; set; } = "";
}
public class SLAYER_Revive : BasePlugin, IPluginConfig<SLAYER_ReviveConfig>
{
    public override string ModuleName => "SLAYER_Revive";
    public override string ModuleVersion => "1.3.2";
    public override string ModuleAuthor => "SLAYER";
    public override string ModuleDescription => "Revive teammates with 'E' (+use button)";
    public required SLAYER_ReviveConfig Config {get; set;}
    public void OnConfigParsed(SLAYER_ReviveConfig config)
    {
        Config = config;
    }
    Dictionary<CCSPlayerController, (int Team, Vector Position, bool IsReviving)> sDiedPlayers = new Dictionary<CCSPlayerController, (int, Vector, bool)>();
    Dictionary<CCSPlayerController, CBeam> DiedPlayersRevivePartical = new Dictionary<CCSPlayerController, CBeam>();
    Dictionary<CCSPlayerController, CBeam[]> BeaconOfRevivingPlayer = new Dictionary<CCSPlayerController, CBeam[]>();
    Dictionary<CCSPlayerController, int> IsPlayerReviving = new Dictionary<CCSPlayerController, int>();
    Dictionary<CCSPlayerController, int> PlayersRevive = new Dictionary<CCSPlayerController, int>();
    Dictionary<CCSPlayerController, float> RevivingTime = new Dictionary<CCSPlayerController, float>();
    Dictionary<CCSPlayerController, float> ReviveDelay = new Dictionary<CCSPlayerController, float>();
    Dictionary<CCSPlayerController, float> DelayMessage = new Dictionary<CCSPlayerController, float>();
    bool isRoundEnd = false;
    // timer
    Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Timers.Timer?> t_Reviving =  new Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Timers.Timer>();
    Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Timers.Timer?> t_ReviveDelay =  new Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Timers.Timer>();
    Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Timers.Timer?> t_DelayMessage =  new Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Timers.Timer>();
    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnTick>(() =>
        {
            if(!Config.PluginEnabled)return;
            
            foreach (var player in Utilities.GetPlayers().Where(player => player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && player.TeamNum > 1 && player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE&& !player.IsHLTV && !player.IsBot))
            {
                // Add null checks and ensure the player exists in the dictionary
                if (!IsPlayerReviving.ContainsKey(player))
                {
                    IsPlayerReviving[player] = -1;
                }

                var buttons = player.Buttons;
                if((buttons & PlayerButtons.Use) != 0) // Check, is player Pressed +use button on tick
                {
                    if(IsPlayerReviving[player] == -1 && !isRoundEnd) // if he not reviving anyone rn and round is not end yet
                    {
                        var DeadTeammate = FindNearestDeadTeammate(player);
                        if(DeadTeammate != null) // We found nearest reviveable teammate
                        {
                            StartReviving(player, DeadTeammate); // Start reviving Dead Teammate
                        }
                    }
                    else // Already Reviving Someone
                    {
                        // Add null check for RevivingTime dictionary
                        if (!RevivingTime.ContainsKey(player))
                        {
                            RevivingTime[player] = 0.0f;
                        }

                        player.PrintToCenterHtml
                        (
                            $"{Localizer["CenterHtml.Reviving", Utilities.GetPlayerFromSlot(IsPlayerReviving[player]).PlayerName]}" +
                            $"{GenerateLoadingText(RevivingTime[player], Config.revive_timer_delay)}"
                        );
                    }
                }
                else
                {
                    if(IsPlayerReviving[player] != -1 && !isRoundEnd) // Was Reviving
                    {
                        if(IsPlayerReviving[player] != 0)AbortReviving(player, Utilities.GetPlayerFromSlot(IsPlayerReviving[player]));
                    }
                }
            }
        });
        RegisterEventHandler<EventRoundStart>((@event, info) =>
        {
            sDiedPlayers?.Clear();
            isRoundEnd = false;
            return HookResult.Continue;
        });
        RegisterEventHandler<EventRoundEnd>((@event, info) =>
        {
            isRoundEnd = true;
            sDiedPlayers?.Clear(); // Clear DiedPlayers Dictionary on round end
            
            foreach (var circle in DiedPlayersRevivePartical?.Where(ent => ent.Value != null && ent.Value.IsValid))
            {
                circle.Value.Remove(); // Remove all circles from Died Players Body
            }
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            if (!Config.PluginEnabled || @event.Userid == null || !@event.Userid.IsValid)
                return HookResult.Continue;

            var player = @event.Userid;

            // Remove player from all dictionaries
            PlayersRevive.Remove(player);
            IsPlayerReviving.Remove(player);
            RevivingTime.Remove(player);
            ReviveDelay.Remove(player);
            DelayMessage.Remove(player);

            // Kill any timers associated with the player
            t_Reviving?[player]?.Kill();
            t_ReviveDelay?[player]?.Kill();
            t_DelayMessage?[player]?.Kill();

            RemoveSquareFromPlayer(player);
            RemoveBeaconCircleFromPlayer(player);

            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
        {
            if(!Config.PluginEnabled || @event.Userid == null || !@event.Userid.IsValid)
                return HookResult.Continue;

            var player = @event.Userid;

            // Initialize dictionaries with default values
            PlayersRevive[player] = 0;
            IsPlayerReviving[player] = -1;
            RevivingTime[player] = 0.0f;
            ReviveDelay[player] = 0.0f;
            DelayMessage[player] = 0.0f;

            t_Reviving[player] = null;
            t_ReviveDelay[player] = null;
            t_DelayMessage[player] = null;

            // Clear any existing timers
            if(t_Reviving?[player] != null) t_Reviving?[player]?.Kill();
            if(t_ReviveDelay?[player] != null) t_ReviveDelay?[player]?.Kill();
            if(t_DelayMessage?[player] != null) t_DelayMessage?[player]?.Kill();

            RemoveSquareFromPlayer(player);
            RemoveBeaconCircleFromPlayer(player);

            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDeath>((@event, info) =>
        {
            if (!Config.PluginEnabled || @event.Userid == null || !@event.Userid.IsValid) return HookResult.Continue;

            var player = @event.Userid;
            var DiedPosition = player.PlayerPawn.Value.AbsOrigin; // Get Death Position of the player
            sDiedPlayers[player] = (player.TeamNum, DiedPosition, false); // Store player team, death position and revive status
            
            // Make a Red/Blue Circle on the above Death Position of the Player to let their teammates know their revive position (This circle will helpful if player dead body is removed by any other plugin)
            if(Config.revive_DrawReviveSign)DiedPlayersRevivePartical[player] = DrawLaserBetween(new Vector(DiedPosition.X + 20, DiedPosition.Y + 20, DiedPosition.Z + 20), new Vector(DiedPosition.X + 2.1f, DiedPosition.Y + 2.1f, DiedPosition.Z + 21.1f), player.TeamNum == 2 ? Color.Red : Color.Blue, -1, 5.0f);
            
            if(IsPlayerReviving != null && IsPlayerReviving?.ContainsKey(player) == true && IsPlayerReviving?[player] != -1) // Player died during reviving his teammate
            {
                AbortReviving(player, Utilities.GetPlayerFromSlot(IsPlayerReviving[player])); // Abort reviving
            }
            return HookResult.Continue;
        });
    }
    private void StartReviving(CCSPlayerController? player, CCSPlayerController? teammate)
    {
        if(Config.PluginEnabled && player == null || !player.IsValid || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)return;
        
        if(DelayMessage?[player] != 0) return;
        var flags = Config.revive_AdminFlag.Split(',');
        var hasPermission = false;
        foreach (var flag in flags) // Check if the player has the required permission
        {
            if (AdminManager.PlayerHasPermissions(player, flag))
            {
                hasPermission = true;
            }
        }
        if (!hasPermission && Config.revive_AdminFlag != "")
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.NoPermission"]}");
            SetDelayMessage(player);
            return;
        }
        if (PlayersRevive?[player] >= Config.revive_ReviveLimit)
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.NotEnoughRevive"]}");
                SetDelayMessage(player);
                return;
            }
        if(ReviveDelay?[player] != 0)
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.ReviveDelay", ReviveDelay[player]]}");
            SetDelayMessage(player);
            return;
        }
        if(Config.revive_cost_mode > 0)
        {
            if((Config.revive_cost_mode == 1 || Config.revive_cost_mode == 3) && Config.revive_cost_health > player.PlayerPawn.Value!.Health)
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.NotEnoughHealth", Config.revive_cost_health]}");
                SetDelayMessage(player);
                return;
            }
            if((Config.revive_cost_mode == 2 || Config.revive_cost_mode == 3) && Config.revive_cost_money > player.InGameMoneyServices!.Account)
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.NotEnoughMoney", Config.revive_cost_money]}");
                SetDelayMessage(player);
                return;
            }
        }
        if(!IsPlayerInDiedPlayers(teammate))return; // Player Should be in Died players list
        teammate.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.GettingRevived", player.PlayerName]}"); // Send message to who is currently getting revived by his teammate
        IsPlayerReviving[player] = teammate.Slot; // Player started reviving
        UpdateReviveStatus(teammate, true); // update dead player status that he is now getting revive
        BeaconOfRevivingPlayer[player] = DrawBeaconCircleOnPlayer(player); // Draw a Circle to let other now that this player is currently reviving his teammate
        RevivingTime[player] = 0.0f;
        t_Reviving[player] = AddTimer(0.1f, () => 
        {
            if (RevivingTime?[player] >= Config.revive_timer_delay) // Delete Timer if player stop reviving
            {
                if(t_Reviving?[player] != null)t_Reviving?[player]?.Kill();
                Revivied(player, teammate);
                return;
            }
            if(CalculateDistanceBetween(player.PlayerPawn.Value.AbsOrigin, sDiedPlayers?[teammate].Position) > Config.revive_distance)
            {
                if(t_Reviving?[player] != null)t_Reviving?[player]?.Kill();
                AbortReviving(player, teammate);
                return;
            }
            RevivingTime[player] += 0.1f;
        }, TimerFlags.REPEAT);
    }
    private void AbortReviving(CCSPlayerController? player, CCSPlayerController? teammate)
    {
        if(player == null || !player.IsValid || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE || player.Connected != PlayerConnectedState.PlayerConnected)return;
        if(teammate == null || !teammate.IsValid || teammate.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE || teammate.Connected != PlayerConnectedState.PlayerConnected)return; // Validate the one who is revived
        
        IsPlayerReviving[player] = -1; // Player abort reviving
        if(IsPlayerInDiedPlayers(teammate))UpdateReviveStatus(teammate, false);
        RemoveBeaconCircleFromPlayer(player); // Remove Beacon from player
        if(t_Reviving?[player] != null)t_Reviving?[player]?.Kill(); // kill timer
        RevivingTime[player] = 0.0f;
    }
    private void Revivied(CCSPlayerController? player, CCSPlayerController? teammate)
    {
        // Validate Reviver and the one who is revived
        if(player == null || !player.IsValid || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE || player.Connected != PlayerConnectedState.PlayerConnected)return;
        if(teammate == null || !teammate.IsValid || teammate.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE || teammate.Connected != PlayerConnectedState.PlayerConnected)return;
        IsPlayerReviving[player] = -1; // set Player is no longer reviving
        PlayersRevive[player]++; // Increase players revive
        RemoveBeaconCircleFromPlayer(player); // Remove Beacon from player
        if(t_Reviving?[player] != null)t_Reviving?[player]?.Kill(); // Kill timer
        RemoveSquareFromPlayer(teammate); // Remove square from Player dead Body
        if(Config.revive_cost_mode > 0)
        {
            if((Config.revive_cost_mode == 1 || Config.revive_cost_mode == 3) && Config.revive_cost_health <= player.PlayerPawn.Value!.Health)
            {
                player.PlayerPawn.Value!.Health -= Config.revive_cost_health;
            }
            if((Config.revive_cost_mode == 2 || Config.revive_cost_mode == 3) && Config.revive_cost_money <= player.InGameMoneyServices!.Account)
            {
                player.InGameMoneyServices!.Account -= Config.revive_cost_money;
                Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");
            }
        }
        // Creating New Vector cause otherwise it create a bug
        Vector position = new Vector(sDiedPlayers?[teammate].Position.X, sDiedPlayers?[teammate].Position.Y, sDiedPlayers?[teammate].Position.Z);
        AddTimer(0.1f,() =>     // a little delay for spawning
        {
            teammate.Respawn();    // Respawn him after he get revivied
        }, TimerFlags.STOP_ON_MAPCHANGE);
        AddTimer(0.25f,() => // Delay timer which will execute after player spawn
        {
            if(teammate != null && teammate.IsValid && teammate.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
            {
                teammate.PlayerPawn.Value!.Health = Config.revive_RevivedHealth; // give specific Health after getting revived
                teammate.PlayerPawn.Value.Teleport(position, teammate.PlayerPawn.Value.AbsRotation, Vector.Zero);
                if(Config.revive_ReviveFrag > 0)player!.ActionTrackingServices!.MatchStats.Kills += Config.revive_ReviveFrag; // Give Frag to the player who revived his teammate
                if(Config.revive_CountDeath == false)teammate!.ActionTrackingServices!.MatchStats.Deaths -= 1; // Decrease Death of the player who got revived
                sDiedPlayers?.Remove(teammate); // Remove Revived Teammate from Died players dictionary
            }
        
        }, TimerFlags.STOP_ON_MAPCHANGE);
        ReviveDelay[player] = Config.revive_delay;
        if(Config.revive_delay > 0)t_ReviveDelay[player] = AddTimer(1.0f, ()=> // Revive delay timer
        {
            if(ReviveDelay?[player] <= 0)
            {
                ReviveDelay[player] = 0.0f;
                if(t_ReviveDelay?[player] != null)t_ReviveDelay?[player]?.Kill(); // Kill timer
                return;
            }
            ReviveDelay[player] -= 1.0f;
        }, TimerFlags.REPEAT);
        
    }
    private void SetDelayMessage(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)return;

        DelayMessage[player] = 1.0f;
        if(Config.revive_delay > 0)t_DelayMessage[player] = AddTimer(0.1f, ()=>
        {
            if(DelayMessage?[player] <= 0)
            {
                DelayMessage[player] = 0.0f;
                if(t_DelayMessage?[player] != null)t_DelayMessage?[player]?.Kill(); // Kill timer
                return;
            }
            DelayMessage[player] -= 0.1f;
        }, TimerFlags.REPEAT);
    }
    private CCSPlayerController FindNearestDeadTeammate(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return null;

        // Get the current player's position
        var currentPosition = player.PlayerPawn.Value.AbsOrigin;

        // Filter the dead players based on the criteria
        var nearestDeadTeammate = sDiedPlayers
            .Where(entry => 
                entry.Key != null &&
                entry.Key.IsValid &&
                entry.Key.Connected == PlayerConnectedState.PlayerConnected &&
                !entry.Key.IsHLTV && // Ensure not HLTV
                entry.Value.Team == player.TeamNum && // Match team
                !entry.Value.IsReviving) // Not currently reviving
            .Select(entry => new 
            {
                Player = entry.Key,
                Distance = CalculateDistanceBetween(currentPosition, entry.Value.Position) // No null check needed for ValueTuple
            })
            .Where(x => x.Distance <= Config.revive_distance) // Distance check
            .OrderBy(x => x.Distance) // Order by distance
            .FirstOrDefault(); // Get the closest

        // Validate that the dead player is still in the game
        if (nearestDeadTeammate?.Player == null || !nearestDeadTeammate.Player.IsValid || 
            nearestDeadTeammate.Player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE || 
            nearestDeadTeammate.Player.Connected != PlayerConnectedState.PlayerConnected)
        {
            return null;
        }
        return nearestDeadTeammate.Player; // Return the closest player, or null if none found
    }

    private void UpdateReviveStatus(CCSPlayerController? player, bool isReviving)
    {
        if (sDiedPlayers?.TryGetValue(player, out var playerInfo) == true)
        {
            // Update the revive status
            playerInfo.IsReviving = isReviving;

            // Set the updated info back in the dictionary
            sDiedPlayers[player] = playerInfo;
        }
    }
    private bool IsPlayerInDiedPlayers(CCSPlayerController? player)
    {
        if(!Config.PluginEnabled || player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected)
        {
            if(sDiedPlayers?.ContainsKey(player) == true)sDiedPlayers?.Remove(player); // Remove him from the died players list
            return false;
        }
        return sDiedPlayers?.ContainsKey(player) == true;
    }
    private void RemoveBeaconCircleFromPlayer(CCSPlayerController? player)
    {
        if(!Config.PluginEnabled || !Config.revive_DrawBeacon || player == null || !player.IsValid)return;

        // Check if the slot is within bounds and if the beacon array is initialized
        if (BeaconOfRevivingPlayer.ContainsKey(player) && BeaconOfRevivingPlayer?[player] != null)
        {
            foreach (var beam in BeaconOfRevivingPlayer?[player].Where(ent => ent != null && ent.IsValid))
            {
                beam.Remove();
            }
        }
    }
    private void RemoveSquareFromPlayer(CCSPlayerController? player)
    {
        if(!Config.PluginEnabled || !Config.revive_DrawReviveSign || player == null || !player.IsValid)return;

        if(DiedPlayersRevivePartical.ContainsKey(player) && DiedPlayersRevivePartical[player].IsValid == true)
        {
            DiedPlayersRevivePartical[player].Remove(); // Remove square from Dead Player Body
        }
    }
    private CBeam[] DrawBeaconCircleOnPlayer(CCSPlayerController? player)
    {
        if(Config.PluginEnabled && Config.revive_DrawBeacon && player == null || !player.IsValid || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)return null;
        
        Vector mid =  new Vector(player?.PlayerPawn.Value.AbsOrigin.X,player?.PlayerPawn.Value.AbsOrigin.Y,player?.PlayerPawn.Value.AbsOrigin.Z);

        int lines = 20;
        CBeam[] beam_ent = new CBeam[lines];

        // draw piecewise approx by stepping angle
        // and joining points with a dot to dot
        float step = (float)(2.0f * Math.PI) / (float)lines;
        float radius = Config.revive_distance;

        float angle_old = 0.0f;
        float angle_cur = step;

        for(int i = 0; i < lines; i++) // Drawing Beacon Circle
        {
            Vector start = angle_on_circle(angle_old, radius, mid);
            Vector end = angle_on_circle(angle_cur, radius, mid);

            beam_ent[i] = DrawLaserBetween(start, end, player.TeamNum == 2 ? Color.Red : Color.Blue, -1, 2.0f);

            angle_old = angle_cur;
            angle_cur += step;
        }
        return beam_ent;
    }
    public CBeam DrawLaserBetween(Vector startPos, Vector endPos, Color color, float life, float width)
    {
        if (startPos == null || endPos == null)
            return null;

        CBeam beam = Utilities.CreateEntityByName<CBeam>("beam");

        if (beam == null)
        {
            Logger.LogError($"Failed to create beam...");
            return null;
        }

        beam.Render = color;
        beam.Width = width;

        beam.Teleport(startPos, QAngle.Zero, Vector.Zero);
        beam.EndPos.X = endPos.X;
        beam.EndPos.Y = endPos.Y;
        beam.EndPos.Z = endPos.Z;
        beam.DispatchSpawn();

        if(life != -1) AddTimer(life, () => {if(beam != null && beam.IsValid) beam.Remove(); }); // destroy beam after specific time

        return beam;
    }
    private Vector angle_on_circle(float angle, float radius, Vector mid)
    {
        // {r * cos(x),r * sin(x)} + mid
        // NOTE: we offset Z so it doesn't clip into the ground
        return new Vector((float)(mid.X + (radius * Math.Cos(angle))),(float)(mid.Y + (radius * Math.Sin(angle))), mid.Z + 6.0f);
    }
    private float CalculateDistanceBetween(Vector point1, Vector point2)
    {
        float dx = point2.X - point1.X;
        float dy = point2.Y - point1.Y;
        float dz = point2.Z - point1.Z;

        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
    private string GenerateLoadingText(float reviveTimer, float maxReviveTime)
    {
        // Define the total length of the loading bar
        const int totalLength = 15; // Total characters in the loading bar
        const char filledChar = '█';
        const char emptyChar = '░';

        // Calculate the number of filled characters based on the revive timer
        int filledLength = (int)((reviveTimer / maxReviveTime) * totalLength);
        filledLength = Math.Clamp(filledLength, 0, totalLength); // Ensure within bounds

        // Create the loading text
        string loadingText = "<font class='fontSize-l' color='red'>⟪ </font><font class='fontSize-l' color='green'>" + new string(filledChar, filledLength) + new string(emptyChar, totalLength - filledLength) + "</font><font class='fontSize-l' color='red'> ⟫</font>";

        return loadingText;
    }
}
