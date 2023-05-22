
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace S_Euglena
{
    //Набор DefOf
    [DefOf]
    public static class EuglenaDefOf
    {
        public static ThingDef BiomaterialFlask;
        public static IncidentDef FallResearchSatellite;

        public static ThingDef AncientATM;
        public static ThingDef SatelliteSolarPanel;
    }

    public class IncidentWorker_FallResearchSatellite : IncidentWorker
    {
        private const float Radius = 3.3f;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            return TryFindCell(out _, map);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!TryFindCell(out var center, map))
            {
                return false;
            }

            foreach (IntVec3 c in GenRadial.RadialCellsAround(center, Radius, false))
            {
                if (c.InBounds(map) && c.DistanceTo(center) < Radius)
                {
                    List<Thing> thingList = c.GetThingList(map);
                    for (int i = thingList.Count - 1; i >= 0; i--)
                    {
                        Thing thing = thingList[i];
                        if (!thing.Destroyed) thing.Destroy();
                    }
                }
            }

            // Обломки
            int numCells = GenRadial.NumCellsInRadius(12);

            for (int i = 0; i < numCells; i++)
            {
                IntVec3 cell = center + GenRadial.RadialPattern[i];

                if (cell.InBounds(map) && cell.Standable(map) && Rand.Chance(0.2f))
                {
                    ThingDef chunkDef = ThingDefOf.ChunkSlagSteel;
                    Thing chunk = ThingMaker.MakeThing(chunkDef);
                    GenSpawn.Spawn(chunk, cell, map);
                }
            }

            // Пол
            TerrainDef newTerrain = TerrainDefOf.MetalTile;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(center, Radius, false))
            {
                if (c.InBounds(map) && c.DistanceTo(center) < Radius)
                {
                    map.terrainGrid.SetTerrain(c, newTerrain);
                }
                if (center.InBounds(map))
                {
                    map.terrainGrid.SetTerrain(center, newTerrain);
                }
            }


            // Создаем круг из стен вокруг точки удара метеорита
            foreach (IntVec3 c in GenRadial.RadialCellsAround(center, Radius, false))
            {
                if (c.InBounds(map))
                {
                    if (c.DistanceTo(center) < Radius && c.DistanceTo(center) > Radius - 1f)
                    {
                        ThingDef wallDef = ThingDefOf.Wall;
                        Rot4 rot = Rot4.North;
                        Thing wall = ThingMaker.MakeThing(wallDef, ThingDefOf.Steel);
                        GenSpawn.Spawn(wall, c, map, rot, WipeMode.Vanish, false);
                    }
                }
            }

            // Спавним Ancient вдоль стен, только в доль внутренней части стен относительно круга
            foreach (IntVec3 c in GenRadial.RadialCellsAround(center, Radius, false))
            {
                if (c.InBounds(map))
                {
                    if (c.DistanceTo(center) + 1f < Radius && c.DistanceTo(center) > Radius - 2f)
                    {
                        // Находим стену наиболее близкую к центру круга
                        IntVec3 wallCell = c;
                        foreach (IntVec3 adjCell in GenAdj.CardinalDirections)
                        {
                            IntVec3 curCell = c + adjCell;
                            if (curCell.InBounds(map) && curCell.DistanceTo(center) < wallCell.DistanceTo(center) && curCell.GetFirstBuilding(map) is Building && curCell.GetFirstBuilding(map).def == ThingDefOf.Wall)
                            {
                                wallCell = curCell;
                            }
                        }
                        // Находим свободную клетку вдоль стены
                        if (wallCell.DistanceTo(center) < Radius - 1f && wallCell.DistanceTo(center) > Radius - 2f)
                        {
                            IntVec3 spawnCell = wallCell;
                            if (spawnCell.InBounds(map) && spawnCell.Standable(map))
                            {
                                float spawnChance = Rand.Value;
                                if (spawnChance < 0.2f)
                                {
                                    ThingDef ancientBarrelDef = EuglenaDefOf.AncientATM;
                                    Rot4 ancientBarrelRot = Rot4.Random;
                                    Thing ancientBarrel = ThingMaker.MakeThing(ancientBarrelDef);
                                    GenSpawn.Spawn(ancientBarrel, spawnCell, map, ancientBarrelRot);
                                }
                                else if (spawnChance < 0.3f)
                                {
                                    ThingDef ancientCrateDef = ThingDefOf.AncientCrate;
                                    Rot4 ancientCrateRot = Rot4.Random;
                                    Thing ancientCrate = ThingMaker.MakeThing(ancientCrateDef);
                                    GenSpawn.Spawn(ancientCrate, spawnCell, map, ancientCrateRot);
                                }
                                else if (spawnChance < 0.4f)
                                {
                                    ThingDef ancientTerminalDef = ThingDefOf.AncientTerminal;
                                    Rot4 ancientTerminalRot = Rot4.Random;
                                    Thing ancientTerminal = ThingMaker.MakeThing(ancientTerminalDef);
                                    GenSpawn.Spawn(ancientTerminal, spawnCell, map, ancientTerminalRot);
                                }
                            }
                        }
                    }
                }
            }

            // Спавним Firefoam pop
            IntVec3 FirefoamspawnCell = CellFinder.RandomClosewalkCellNear(center, map, (int)(Radius - 1f));
            if (FirefoamspawnCell.IsValid && !FirefoamspawnCell.Fogged(map) && FirefoamspawnCell.GetFirstBuilding(map) == null)
            {
                ThingDef firefoamPopperDef = ThingDefOf.FirefoamPopper;
                Thing firefoamPopper = ThingMaker.MakeThing(firefoamPopperDef);
                GenSpawn.Spawn(firefoamPopper, FirefoamspawnCell, map);
            }

            // Спавним колбу в центре круга
            IntVec3 spawnFlaskCell = center;
            ThingDef BiomaterialFlaskDef = EuglenaDefOf.BiomaterialFlask;
            Rot4 BiomaterialFlaskRot = Rot4.North;


            Thing BiomaterialFlask = ThingMaker.MakeThing(BiomaterialFlaskDef);
            GenSpawn.Spawn(BiomaterialFlask, spawnFlaskCell, map, BiomaterialFlaskRot);

            // Строим крышу над кругом
            foreach (IntVec3 c in GenRadial.RadialCellsAround(center, Radius, true))
            {
                if (c.InBounds(map) && c.DistanceTo(center) < Radius)
                {
                    if (!c.Roofed(map))
                    {
                        RoofDef roofDef = RoofDefOf.RoofConstructed;
                        map.roofGrid.SetRoof(c, roofDef);
                        MoteMaker.PlaceTempRoof(c, map);
                    }
                }


            }

            IntVec3 atmOrigin = new IntVec3(center.x, center.y, center.z - Mathf.RoundToInt(Radius / 2f) - 3);

            // Спавним две постройки по сторонам круга
            int halfRadius = Mathf.RoundToInt(Radius / 2f);
            IntVec3 leftBuildingCell = new IntVec3(center.x - halfRadius - 4, center.y, center.z);
            IntVec3 rightBuildingCell = new IntVec3(center.x + halfRadius + 4, center.y, center.z);

            ThingDef buildingDef = EuglenaDefOf.SatelliteSolarPanel;
            Rot4 leftBuildingRot = Rot4.South;
            Rot4 rightBuildingRot = Rot4.North;

            Thing leftBuilding = ThingMaker.MakeThing(buildingDef);
            GenSpawn.Spawn(leftBuilding, leftBuildingCell, map, leftBuildingRot);

            Thing rightBuilding = ThingMaker.MakeThing(buildingDef);
            GenSpawn.Spawn(rightBuilding, rightBuildingCell, map, rightBuildingRot);
            //Падающий спутник

            GenExplosion.DoExplosion(leftBuildingCell, map, 20f, DamageDefOf.Flame, leftBuilding);
            GenExplosion.DoExplosion(rightBuildingCell, map, 20f, DamageDefOf.Flame, rightBuilding);

            string text = "A fallen research satellite has been found on the map!";
            SendStandardLetter("Satellite fell", text, LetterDefOf.PositiveEvent, parms, new TargetInfo(center, map)); // отправка уведомления о падении спутника

            return true;
        }

        private bool TryFindCell(out IntVec3 cell, Map map)
        {
            int maxMineables = ThingSetMaker_Meteorite.MineablesCountRange.max;
            return CellFinderLoose.TryFindSkyfallerCell(ThingDefOf.MeteoriteIncoming, map, out cell, 10, default(IntVec3), -1, allowRoofedCells: true, allowCellsWithItems: false, allowCellsWithBuildings: false, colonyReachable: false, avoidColonistsIfExplosive: true, alwaysAvoidColonists: true, delegate (IntVec3 x)
            {
                int num = Mathf.CeilToInt(Mathf.Sqrt(maxMineables)) + 2;
                CellRect cellRect = CellRect.CenteredOn(x, num, num);
                int num2 = 0;
                foreach (IntVec3 item in cellRect)
                {
                    if (item.InBounds(map) && item.Standable(map))
                    {
                        num2++;
                    }
                }
                return num2 >= maxMineables;
            });
        }
    }

    public class CompUseEffect_IncidentStart : CompUseEffect
    {
        public override void DoEffect(Pawn user)
        {
            base.DoEffect(user);
            IncidentParms parms = new IncidentParms
            {
                target = parent.Map,
                spawnCenter = parent.PositionHeld
            };
            EuglenaDefOf.FallResearchSatellite.Worker.TryExecute(parms);
        }
    }

    public class CompProperties_AssignFaction : CompProperties
    {
        public CompProperties_AssignFaction()
        {
            this.compClass = typeof(CompAssignFaction);
        }
    }
    public class CompAssignFaction : ThingComp
    {
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            Pawn pawn = parent as Pawn;
            if (pawn != null && pawn.Faction != Faction.OfPlayer)
            {
                pawn.SetFaction(Faction.OfPlayer);
            }
        }
    }

    public class CompProperties_InstantHatcher : CompProperties
    {
        public CompProperties_InstantHatcher()
        {
            this.compClass = typeof(CompInstantHatcher);
        }
    }
    public class CompInstantHatcher : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (DebugSettings.ShowDevGizmos)
            {
                foreach (var g in base.CompGetGizmosExtra()) yield return g;

                yield return new Command_Action
                {
                    defaultLabel = "Instant Hatch",
                    defaultDesc = "Instantly hatches this egg.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower"),
                    action = InstantHatch
                };
            }

        }

        private void InstantHatch()
        {
            var hatcher = this.parent.GetComp<CompHatcher>();
            if (hatcher != null)
            {
                var fieldInfo = typeof(CompHatcher).GetField("gestateProgress", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(hatcher, 1f);
                }
            }
        }

    }

    public class ResearchToItemGroup
    {
        public ResearchProjectDef research;
        public List<ItemWithCost> items;
    }
    public class ItemWithCost
    {
        public ThingDef item;
        public int cost;
    }

    public class CompProperties_CorpseBar : CompProperties
    {
        public int totalPower = 300;

        public int cooldownTicksLef = 120000;


        public List<ResearchToItemGroup> researchToItemGroups = new List<ResearchToItemGroup>();

        public string LaberButtom = "List Items";
        public string LaberButtomDescription = "Toggle item list";
        public string texIconPath;

        public string LaberGizmo = "Laber";
        public string LaberGizmoDescription = "GizmoDescription";

        public string iconPath;
        public float drawSize;
        public Color color;

        public int minPowerAtBaby = 10;
        public int maxPowerAtAdult = 100;

        public CompProperties_CorpseBar()
        {
            compClass = typeof(CompCorpseBar);
        }
    }
    public class CompCorpseBar : ThingComp
    {
        private int powerTicksLeft;

        private int cooldownTicksLeft;

        private CorpseGizmo gizmo;

        private bool showItemList = false;

        private int ticksSinceAbsorbed = 0;

        public CompProperties_CorpseBar Props => (CompProperties_CorpseBar)props;



        public float TotalPower
        {
            get
            {
                if (parent is Pawn pawn)
                {
                    float lifeStagePercentage = pawn.ageTracker.CurLifeStageIndex / (float)pawn.RaceProps.lifeStageAges.Count;
                    return Mathf.RoundToInt(lifeStagePercentage * Props.totalPower);
                }
                return Props.totalPower;
            }
        }

        public float PercentFull => powerTicksLeft / (float)TotalPower;

        public int PowerTicksLeft => powerTicksLeft;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
        }

        private int ingestDelayTicks = 0;

        public override void CompTick()
        {
            base.CompTick();

            if (parent is Pawn pawn)
            {
                if (powerTicksLeft > TotalPower)
                {
                    powerTicksLeft = (int)TotalPower;
                }

                if (ticksSinceAbsorbed < 250)
                {
                    ticksSinceAbsorbed++;
                    return;
                }

                if (pawn.CurJob != null && pawn.CurJob.targetA.Thing != null)
                {
                    if (pawn.CurJob.def == JobDefOf.Ingest && pawn.CurJob.targetA.Thing is Corpse corpse)
                    {
                        // Check if the pawn is close enough to the corpse
                        if (pawn.Position.AdjacentTo8WayOrInside(corpse.Position))
                        {
                            // Check if 30 ticks passed since the pawn started eating
                            if (ingestDelayTicks++ >= 200)
                            {
                                float corpseWeight = corpse.GetStatValue(StatDefOf.Mass);
                                float remainingPower = TotalPower - powerTicksLeft;
                                int powerToAdd = Mathf.RoundToInt(corpseWeight);

                                float decayPct = corpse.InnerPawn.health.hediffSet.GetCoverageOfNotMissingNaturalParts(corpse.InnerPawn.RaceProps.body.corePart) / corpse.InnerPawn.RaceProps.body.corePart.def.GetMaxHealth(corpse.InnerPawn);

                                powerToAdd = Mathf.RoundToInt(powerToAdd * (1 - decayPct));

                                if (powerToAdd > remainingPower)
                                {
                                    powerToAdd = Mathf.RoundToInt(remainingPower);
                                }

                                powerTicksLeft += powerToAdd;
                                corpse.Destroy();
                                ticksSinceAbsorbed = 0;

                                pawn.needs.food.CurLevelPercentage = 1f;

                                // Reset delay ticks
                                ingestDelayTicks = 0;
                            }
                        }
                        else
                        {
                            // Reset delay ticks if pawn is not adjacent to the corpse
                            ingestDelayTicks = 0;
                        }
                    }
                }

                if (powerTicksLeft >= TotalPower)
                {
                    powerTicksLeft = (int)TotalPower;
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo item in base.CompGetGizmosExtra())
            {
                yield return item;
            }

            if (parent is Pawn pawn && pawn.ageTracker.CurLifeStageIndex != 0)
            {
                if (Find.Selector.SingleSelectedThing == parent && parent.Faction == Faction.OfPlayer)
                {
                    var availableOptions = GetAvailableOptions();

                    if (availableOptions.Any())
                    {
                        if (gizmo == null)
                        {
                            gizmo = new CorpseGizmo(this);
                        }

                        yield return gizmo;
                    }
                    if (availableOptions.Count > 0)
                    {
                        string recoveringStatus = $"The shell is recovering. About {Math.Round(cooldownTicksLeft / 2500.0)} hours to go.";

                        yield return new Command_Action
                        {
                            defaultLabel = Props.LaberButtom,
                            defaultDesc = Props.LaberButtomDescription,
                            icon = ContentFinder<Texture2D>.Get(Props.iconPath),
                            iconDrawScale = Props.drawSize,
                            defaultIconColor = cooldownTicksLeft > 0 ? Color.grey : Props.color,
                            action = delegate ()
                            {
                                List<FloatMenuOption> options = new List<FloatMenuOption>();
                                ResearchProjectDef lastResearch = null;

                                foreach (var researchToItemGroup in Props.researchToItemGroups)
                                {
                                    ResearchProjectDef research = researchToItemGroup.research;
                                    List<ItemWithCost> items = researchToItemGroup.items;
                                    List<FloatMenuOption> subOptions = new List<FloatMenuOption>();

                                    foreach (ItemWithCost itemWithCost in items)
                                    {
                                        ThingDef item = itemWithCost.item;
                                        int cost = itemWithCost.cost;

                                        bool researchCompleted = research == null || research.IsFinished;

                                        if (researchCompleted)
                                        {
                                            subOptions.Add(new FloatMenuOption("(" + cost + ") " + item.LabelCap, delegate ()
                                            {
                                                SpawnResource(item, cost);
                                            },
                                            MenuOptionPriority.Default, null, null, 0f, null, null)
                                            {
                                                Disabled = (PowerTicksLeft < cost),
                                                tooltip = item.description
                                            });
                                        }
                                    }

                                    if (research.IsFinished && subOptions.Count > 0)
                                    {
                                        options.Add(new FloatMenuOption(research.LabelCap, delegate ()
                                        {
                                            FloatMenu floatMenu = new FloatMenu(subOptions);
                                            floatMenu.windowRect.y -= 200;
                                            Find.WindowStack.Add(floatMenu);
                                        },
                                        MenuOptionPriority.Default, null, null, 0f, null, null));
                                    }

                                    lastResearch = research;
                                }

                                FloatMenu primaryFloatMenu = new FloatMenu(options);
                                primaryFloatMenu.windowRect.y -= 200;
                                Find.WindowStack.Add(primaryFloatMenu);

                            },
                            disabled = cooldownTicksLeft > 0,
                            disabledReason = cooldownTicksLeft > 0 ? recoveringStatus : "Cooldown active"
                        };

                    };


                    if (DebugSettings.ShowDevGizmos)
                    {
                        Command_Action command_Action = new Command_Action();
                        command_Action.defaultLabel = "Set 0";
                        command_Action.action = delegate
                        {
                            powerTicksLeft = 0;
                        };
                        yield return command_Action;

                        Command_Action command_Action2 = new Command_Action();
                        command_Action2.defaultLabel = "Add +10";
                        command_Action2.action = delegate
                        {
                            powerTicksLeft += 10;
                        };
                        yield return command_Action2;

                        Command_Action command_Action3 = new Command_Action();
                        command_Action3.defaultLabel = "Add -10";
                        command_Action3.action = delegate
                        {
                            powerTicksLeft -= 10;
                        };
                        yield return command_Action3;

                        Command_Action command_Action4 = new Command_Action();
                        command_Action4.defaultLabel = "Fill";
                        command_Action4.action = delegate
                        {
                            powerTicksLeft = (int)TotalPower;
                        };
                        yield return command_Action4;
                    }

                }

            }
        }

        private List<FloatMenuOption> GetAvailableOptions()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (var researchToItemGroup in Props.researchToItemGroups)
            {
                ResearchProjectDef research = researchToItemGroup.research;
                List<ItemWithCost> items = researchToItemGroup.items;

                if (research == null || research.IsFinished)
                {
                    foreach (ItemWithCost itemWithCost in items)
                    {
                        ThingDef item = itemWithCost.item;
                        options.Add(new FloatMenuOption(item.LabelCap, null));
                    }
                }
            }

            return options;
        }

        public void SpawnResource(ThingDef def, int cost)
        {
            if (powerTicksLeft >= cost && cooldownTicksLeft == 0) // check if enough power
            {

                IntVec3 position = parent.Position;
                Map map = parent.Map;

                Thing thing = ThingMaker.MakeThing(def);
                GenPlace.TryPlaceThing(thing, position, map, ThingPlaceMode.Near);

                powerTicksLeft -= cost; // reduce power

                cooldownTicksLeft = Props.cooldownTicksLef; // set the cooldown
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref powerTicksLeft, "powerTicksLeft", 0);
            Scribe_Values.Look(ref cooldownTicksLeft, "cooldownTicksLeft", 0);
        }

    }

    [StaticConstructorOnStartup]
    public class CorpseGizmo : Gizmo
    {
        private CompCorpseBar CorpseAbsorb;

        private static readonly Texture2D BarTex = SolidColorMaterials.NewSolidColorTexture(new Color32(12, 45, 45, byte.MaxValue));

        private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(GenUI.FillableBar_Empty);

        public CorpseGizmo(CompCorpseBar carrier)
        {
            CorpseAbsorb = carrier;
        }

        public override float GetWidth(float maxWidth)
        {
            return 160f;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            if (CorpseAbsorb.parent is Pawn pawn && pawn.ageTracker.CurLifeStageIndex == 0) // Проверка текущей стадии роста
            {
                return new GizmoResult(GizmoState.Clear); // Возвращаем пустой результат, чтобы ничего не отображалось
            }
            else
            {
                Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
                Rect rect2 = rect.ContractedBy(10f);
                Widgets.DrawWindowBackground(rect);
                TaggedString taggedString = CorpseAbsorb.Props.LaberGizmo;
                Rect rect3 = new Rect(rect2.x, rect2.y, rect2.width, Text.CalcHeight(taggedString, rect2.width) + 8f);
                Text.Font = GameFont.Small;
                Widgets.Label(rect3, taggedString);
                Rect barRect = new Rect(rect2.x, rect3.yMax, rect2.width, rect2.height - rect3.height);
                float powerTicksLeft = CorpseAbsorb.PowerTicksLeft;
                float totalPower = CorpseAbsorb.TotalPower;
                float fillPercent = powerTicksLeft / totalPower;
                Widgets.FillableBar(barRect, fillPercent, BarTex, EmptyBarTex, doBorder: true);
                for (int i = 2500; i < CorpseAbsorb.TotalPower; i += 2500)
                {
                    DoBarThreshold((float)i / (float)CorpseAbsorb.TotalPower);
                }
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(barRect, Mathf.CeilToInt((float)CorpseAbsorb.PowerTicksLeft) + (string)"");
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(rect2, () => CorpseAbsorb.Props.LaberGizmoDescription, Gen.HashCombineInt(CorpseAbsorb.GetHashCode(), 34242369));
                return new GizmoResult(GizmoState.Clear);
                void DoBarThreshold(float percent)
                {
                    Rect position = default(Rect);
                    position.x = barRect.x + 3f + (barRect.width - 8f) * percent;
                    position.y = barRect.y + barRect.height - 9f;
                    position.width = 2f;
                    position.height = 6f;
                    GUI.DrawTexture(position, BaseContent.BlackTex);
                }
            }
        }
    }


    public class CompAbilityEffectProperties_Stun : CompProperties_AbilityEffect
    {
        public int stunDuration = 1200;

        public CompAbilityEffectProperties_Stun()
        {
            compClass = typeof(CompAbilityEffect_Stun);
        }
    }
    public class CompAbilityEffect_Stun : CompAbilityEffect
    {
        private CompAbilityEffectProperties_Stun Props => (CompAbilityEffectProperties_Stun)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null || !pawn.health.capacities.CanBeAwake || pawn.Downed || pawn.Dead || pawn.RaceProps.IsMechanoid)
            {
                return;
            }

            pawn.jobs.StartJob(new Job(JobDefOf.LayDown, pawn.Position));
            pawn.stances.stunner.StunFor(Props.stunDuration, null);
        }
    }

    public class CompProperties_CalmDown : CompProperties_AbilityEffect
    {
        public CompProperties_CalmDown()
        {
            compClass = typeof(CompAbilityEffect_CalmDown);
        }
    }
    public class CompAbilityEffect_CalmDown : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.HasThing && target.Thing is Pawn pawn)
            {
                if (pawn.mindState.mentalStateHandler.InMentalState)
                {
                    pawn.mindState.mentalStateHandler.CurState.RecoverFromState(); // forcing pawn to recover from the mental state
                }
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return target.HasThing && target.Thing is Pawn pawn && pawn.mindState.mentalStateHandler.InMentalState;
        }
    }

    public class HediffCompProperties_HiveMind : HediffCompProperties
    {
        public ThoughtDef aloneThought;
        public ThoughtDef oneOtherThought;
        public ThoughtDef fewOthersThought;
        public ThoughtDef manyOthersThought;

        public float radius = 5.0f;

        public HediffCompProperties_HiveMind()
        {
            this.compClass = typeof(HediffComp_HiveMind);
        }
    }
    public class HediffComp_HiveMind : HediffComp
    {
        private HediffCompProperties_HiveMind Props => (HediffCompProperties_HiveMind)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = this.Pawn;

            if (pawn.Spawned)
            {
                int count = GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, Props.radius, true)
                            .Count(thing => thing is Pawn otherPawn && otherPawn.health.hediffSet.HasHediff(this.Def));

                ThoughtDef thoughtDef = null;

                if (count == 1)
                {
                    thoughtDef = Props.aloneThought;
                }
                else if (count == 2)
                {
                    thoughtDef = Props.oneOtherThought;
                }
                else if (count <= 4)
                {
                    thoughtDef = Props.fewOthersThought;
                }
                else
                {
                    thoughtDef = Props.manyOthersThought;
                }

                // Remove previous memories
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(Props.aloneThought);
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(Props.oneOtherThought);
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(Props.fewOthersThought);
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(Props.manyOthersThought);

                // Gain the new memory
                pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
            }
        }
    }

    public class CompAbilityEffectProperties_ApplyHediff : CompProperties_AbilityEffect
    {
        public HediffDef hediffDef;

        public CompAbilityEffectProperties_ApplyHediff()
        {
            compClass = typeof(CompAbilityEffect_ApplyHediff);
        }
    }
    public class CompAbilityEffect_ApplyHediff : CompAbilityEffect
    {
        public CompAbilityEffectProperties_ApplyHediff Props => (CompAbilityEffectProperties_ApplyHediff)this.props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (target.HasThing && target.Thing is Pawn pawn)
            {
                Hediff existingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);

                if (existingHediff != null)
                {
                    pawn.health.RemoveHediff(existingHediff);
                }
                Hediff hediff = HediffMaker.MakeHediff(Props.hediffDef, pawn);
                pawn.health.AddHediff(hediff);
            }
        }
    }
}