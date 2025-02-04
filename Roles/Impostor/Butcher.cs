﻿using TOHE.Modules;
using TOHE.Roles.Core;
using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.Impostor;

internal class Butcher : RoleBase
{
    //===========================SETUP================================\\
    private const int Id = 24300;
    private static readonly HashSet<byte> PlayerIds = [];
    public static bool HasEnabled => PlayerIds.Any();
    
    public override CustomRoles ThisRoleBase => CustomRoles.Impostor;
    public override Custom_RoleType ThisRoleType => Custom_RoleType.ImpostorKilling;
    //==================================================================\\

    private static Dictionary<byte, (int, int, Vector2)> MurderTargetLateTask = [];
    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Butcher);
    }
    public override void Init()
    {
        MurderTargetLateTask = [];
        PlayerIds.Clear();
    }
    public override void Add(byte playerId)
    {
        PlayerIds.Add(playerId);

        if (AmongUsClient.Instance.AmHost)
        {
            CustomRoleManager.OnFixedUpdateOthers.Add(OnFixedUpdateOthers);
        }
    }

    public override void SetAbilityButtonText(HudManager hud, byte playerId) => hud.KillButton.OverrideText(Translator.GetString("ButcherButtonText"));

    public override void OnMurderPlayerAsKiller(PlayerControl killer, PlayerControl target, bool inMeeting, bool isSuicide)
    {
        if (inMeeting || isSuicide) return;
        if (target == null) return;

        target.SetRealKiller(killer);
        Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Dismembered;
        Main.PlayerStates[target.PlayerId].SetDead();

        Main.OverDeadPlayerList.Add(target.PlayerId);
        //var ops = target.GetCustomPosition();
        var rd = IRandom.Instance;

        if (target.Is(CustomRoles.Avanger))
        {
            CustomSoundsManager.RPCPlayCustomSoundAll("Congrats");
            var pcList = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId); //No need to do extra check cause nobody is winning
            pcList.Do(x =>
            {
                Main.PlayerStates[x.PlayerId].deathReason = PlayerState.DeathReason.Revenge;
                target.RpcSpecificMurderPlayer(x, x);
                x.SetRealKiller(target);
                Main.PlayerStates[x.PlayerId].SetDead();
            });

            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
            return;
        }

        _ = new LateTask(() =>
        {
            for (int i = 0; i <= 19; i++)
            {
                if (GameStates.IsMeeting) break;
                if (!target.AmOwner)
                {
                    target.MurderPlayer(target, ExtendedPlayerControl.ResultFlags);
                }
                Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId && !x.AmOwner)
                .Do(x => target.RpcSpecificMurderPlayer(target, x));
            }
        }, 0.2f, "Butcher Show Bodies"); //25 exactly takes over the whole screen

        _ = new LateTask(() =>
        {
            if (!MurderTargetLateTask.ContainsKey(target.PlayerId))
                MurderTargetLateTask.Add(target.PlayerId, (0, 0, target.GetCustomPosition()));
        }, 0.6f, "Butcher Late Kill");
    }

    public override void AfterMeetingTasks() => MurderTargetLateTask = [];
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => MurderTargetLateTask.Clear();

    public static void OnFixedUpdateOthers(PlayerControl target)
    {
        if (!MurderTargetLateTask.ContainsKey(target.PlayerId)) return;
        if (target == null || !target.Data.IsDead) return;
        var ops = MurderTargetLateTask[target.PlayerId].Item3;

        if (MurderTargetLateTask[target.PlayerId].Item1 > 19) //on fix update updates 30 times pre second
        {
            if (MurderTargetLateTask[target.PlayerId].Item2 < 5)
            {
                var rd = IRandom.Instance;

                Vector2 location = new(ops.x + ((float)(rd.Next(1, 200) - 100) / 100), ops.y + ((float)(rd.Next(1, 200) - 100) / 100));
                target.RpcTeleport(location);
                target.RpcMurderPlayer(target);
                target.SetRealKiller(Utils.GetPlayerById(PlayerIds.First()), true);
                MurderTargetLateTask[target.PlayerId] = (0, MurderTargetLateTask[target.PlayerId].Item2 + 1, ops);
            }
            else MurderTargetLateTask.Remove(target.PlayerId);
        }
        else
            MurderTargetLateTask[target.PlayerId] = (MurderTargetLateTask[target.PlayerId].Item1 + 1, MurderTargetLateTask[target.PlayerId].Item2, ops);
    }

}