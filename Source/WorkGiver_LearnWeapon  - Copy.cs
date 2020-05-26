﻿using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace HumanResources
{
    internal class WorkGiver_LearnWeapon : WorkGiver_Knowledge
    {
        //protected string RecipeName = "TrainWeapon";

        private List<ThingCount> chosenIngThings = new List<ThingCount>();
        //private string RangedSuffix = "Shooting";
        //private string MeleeSuffix = "Melee";

        private bool ValidateChosenWeapons(Bill bill, Pawn pawn)
        {
            IEnumerable<ThingDef> knownWeapons = pawn.TryGetComp<CompKnowledge>().knownWeapons;
            var studyWeapons = bill.ingredientFilter.AllowedThingDefs.Except(knownWeapons);
            if (studyWeapons.Any())
            {
                Log.Message("Validating chosen weapons for " + pawn + ": " + studyWeapons.ToStringSafeEnumerable());
            }
            else Log.Message("Validating chosen weapons for " + pawn + ": no weapons available");
            return studyWeapons.Any();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Log.Message(pawn + " is looking for a training job...");
            Building_WorkTable Target = t as Building_WorkTable;
            if (Target != null)
            {
                if (!CheckJobOnThing(pawn, t, forced)/* && RelevantBills(t).Any()*/)
                {
                    //Log.Message("...no job on target.");
                    return false;
                }
                IEnumerable<ThingDef> knownWeapons = pawn.TryGetComp<CompKnowledge>().knownWeapons;
                foreach (Bill bill in RelevantBills(Target))
                {
                    return ValidateChosenWeapons(bill, pawn);
                }
                JobFailReason.Is("NoWeaponToLearn".Translate(pawn), null);
                return false;
            }
            //Log.Message("case 4");
            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            Log.Message(pawn + " looking for a job at " + thing);
            IBillGiver billGiver = thing as IBillGiver;
            if (billGiver != null && ThingIsUsableBillGiver(thing) && billGiver.BillStack.AnyShouldDoNow && billGiver.UsableForBillsAfterFueling())
            {
                LocalTargetInfo target = thing;
                if (pawn.CanReserve(target, 1, -1, null, forced) && !thing.IsBurning() && !thing.IsForbidden(pawn))
                {
                    Log.Message("HasJobOnThing from JobOnThing: " + HasJobOnThing(pawn, thing));
                    billGiver.BillStack.RemoveIncompletableBills();
                    foreach (Bill bill in RelevantBills(thing))
                    {
                        return StartBillJob(pawn, billGiver);
                    }
                }
            }
            return null;
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            ////IEnumerable<ThingDef> studyMaterial = available.Except(knownWeapons);
            //IEnumerable<ThingDef> chosen = chosenIngThings.AsEnumerable();
            //IEnumerable<ThingDef> studyMaterial = chosen.Where()
            //bool flag = studyMaterial.Count() > 0;
            ////if (!flag) Log.Message(pawn + " skipped training. Available: " + available.ToList().Count() + ", studyMaterial: " + studyMaterial.Count());
            //return !flag;

            IEnumerable<ThingDef> knownWeapons = pawn.TryGetComp<CompKnowledge>().knownWeapons;
            IEnumerable<ThingDef> chosen = chosenIngThings.Cast<ThingDef>();
            IEnumerable<ThingDef> available = ModBaseHumanResources.unlocked.weapons;
            IEnumerable<ThingDef> studyMaterial = chosen.Intersect(available).Except(knownWeapons);
            return !studyMaterial.Any();
        }

        private Job StartBillJob(Pawn pawn, IBillGiver giver)
        {
            Log.Warning(pawn + " is trying to start a training job...");
            for (int i = 0; i < giver.BillStack.Count; i++)
            {
                Bill bill = giver.BillStack[i];
                if (bill.recipe.requiredGiverWorkType == null || bill.recipe.requiredGiverWorkType == def.workType)
                {
                    //reflection info
                    FieldInfo rangeInfo = AccessTools.Field(typeof(WorkGiver_DoBill), "ReCheckFailedBillTicksRange");
                    IntRange range = (IntRange)rangeInfo.GetValue(this);
                    //
                    if (Find.TickManager.TicksGame >= bill.lastIngredientSearchFailTicks + range.RandomInRange || FloatMenuMakerMap.makingFor == pawn)
                    {
                        bill.lastIngredientSearchFailTicks = 0;
                        if (bill.ShouldDoNow() && bill.PawnAllowedToStartAnew(pawn))
                        {
                            //reflection info
                            MethodInfo BestIngredientsInfo = AccessTools.Method(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients");
                            FieldInfo MissingMaterialsTranslatedInfo = AccessTools.Field(typeof(WorkGiver_DoBill), "MissingMaterialsTranslated");
                            //
                            chosenIngThings.RemoveAll(x => pawn.TryGetComp<CompKnowledge>().knownWeapons.Contains(x.Thing.def));
                            if ((bool)BestIngredientsInfo.Invoke(this, new object[] { bill, pawn, giver, chosenIngThings }))
                            {
                                if (chosenIngThings.Any())
                                {
                                    Log.Message("...weapon found, chosen ingredients: " + chosenIngThings.Select(x => x.Thing).ToStringSafeEnumerable());
                                    Job result = TryStartNewDoBillJob(pawn, bill, giver);
                                    chosenIngThings.Clear();
                                    return result;
                                }
                            }
                            if (FloatMenuMakerMap.makingFor != pawn)
                            {
                                //Log.Message("...float menu maker case");
                                bill.lastIngredientSearchFailTicks = Find.TickManager.TicksGame;
                            }
                            else
                            {
                                //Log.Message("...missing materials");
                                JobFailReason.Is((string)MissingMaterialsTranslatedInfo.GetValue(this), bill.Label);
                            }
                            chosenIngThings.Clear();
                        }
                    }
                }
            }
            Log.Message("...job failed.");
            chosenIngThings.Clear();
            return null;
        }

        private Job TryStartNewDoBillJob(Pawn pawn, Bill bill, IBillGiver giver)
        {
            Job job = WorkGiverUtility.HaulStuffOffBillGiverJob(pawn, giver, null);
            if (job != null)
            {
                return job;
            }
            Job job2 = new Job(DefDatabase<JobDef>.GetNamed("TrainWeapon"), (Thing)giver);
            IEnumerable<ThingDef> knownWeapons = pawn.TryGetComp<CompKnowledge>().knownWeapons;
            chosenIngThings.RemoveAll(x => knownWeapons.Contains(x.Thing.def));
            job2.targetQueueB = new List<LocalTargetInfo>(chosenIngThings.Count);
            job2.countQueue = new List<int>(chosenIngThings.Count);
            for (int i = 0; i < chosenIngThings.Count; i++)
            {
                job2.targetQueueB.Add(chosenIngThings[i].Thing);
                job2.countQueue.Add(chosenIngThings[i].Count);
            }
            job2.haulMode = HaulMode.ToCellNonStorage;
            job2.bill = bill;
            return job2;
        }
    }
}