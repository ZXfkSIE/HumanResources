﻿using HarmonyLib;
using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace HumanResources
{
    //Checks if pawn knows a weapon before equiping it, 1/3
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob), new Type[] { typeof(Job), typeof(JobTag) })]
    public static class Pawn_JobTracker_TryTakeOrderedJob
    {
        public static bool Prefix(Job job, Pawn ___pawn)
        {
            if (___pawn.RaceProps.Humanlike && ___pawn.Faction.IsPlayer && ___pawn.TryGetComp<CompKnowledge>() != null && job.def == JobDefOf.Equip) return HarmonyPatches.CheckKnownWeapons(___pawn, job.targetA.Thing);
            else return true;
        }
    }
}
