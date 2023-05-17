
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;


namespace TestClass
{
    public class HediffCompProperties_Rebirth : HediffCompProperties
    {
        public HediffCompProperties_Rebirth()
        {
            compClass = typeof(Hediff_Rebirth);
        }
    }
    public class Hediff_Rebirth : HediffComp
    {
        public override void Notify_PawnDied()
        {
            base.Notify_PawnDied();

            if (Pawn.Dead)
            {
                RebirthPawn();
            }
        }

        private void RebirthPawn()
        {
            // Удаляем текущий Hediff, чтобы избежать повторного возрождения
            Pawn.health.RemoveHediff(parent);

            // Создаем новую пешку с теми же характеристиками, что и умершая пешка
            PawnGenerationRequest request = new PawnGenerationRequest(Pawn.kindDef, Pawn.Faction, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: false, allowDead: false, allowDowned: true, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, forceNoIdeo: false, forceNoBackstory: false, forbidAnyTitle: false, forceDead: false, null, null, null, null, null, 0f, DevelopmentalStage.Newborn);

            Pawn newPawn = PawnGenerator.GeneratePawn(request);
            newPawn.Name = Pawn.Name;

            // Помещаем новую пешку на карту
            GenSpawn.Spawn(newPawn, Pawn.Corpse.Position, Pawn.Corpse.Map, WipeMode.Vanish);

            // Удаляем труп старой пешки
            Pawn.Corpse?.Destroy();
        }
    }


    public class HediffCompProperties_AverageSkills : HediffCompProperties
    {
        public HediffCompProperties_AverageSkills()
        {
            compClass = typeof(HediffComp_AverageSkills);
        }
    }
    public class HediffComp_AverageSkills : HediffComp
    {
        private int ticksSinceLastSkillAverage = 0;
        private int skillAverageInterval = 60000; // Каждую игровую неделю (60,000 тиков)

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (Pawn.Map == null) return;

            ticksSinceLastSkillAverage++;

            // Если время для смешивания навыков еще не пришло, ничего не делаем
            if (ticksSinceLastSkillAverage < skillAverageInterval) return;

            // Если время для смешивания навыков пришло и еще не прошло 1000 тиков, останавливаем пешку
            if (ticksSinceLastSkillAverage < skillAverageInterval)
            {
                Pawn.jobs.StopAll();
                return;
            }

            // Сбрасываем счетчик тиков и продолжаем смешивание навыков
            ticksSinceLastSkillAverage = 0;

            // Получаем список пешек с таким же Hediff на карте
            List<Pawn> pawnsWithSameHediff = new List<Pawn>();
            foreach (Pawn p in Pawn.Map.mapPawns.AllPawns)
            {
                if (p != Pawn && p.health.hediffSet.HasHediff(parent.def))
                {
                    pawnsWithSameHediff.Add(p);
                }
            }

            // Если нет других пешек с данным заболеванием, не продолжаем
            if (!pawnsWithSameHediff.Any()) return;

            // Усредняем уровень навыков между пешками
            foreach (SkillRecord skill in Pawn.skills.skills)
            {
                if (skill.TotallyDisabled) continue; // Игнорируем недоступные навыки

                int totalSkill = skill.Level;
                int pawnCount = 1;

                foreach (Pawn otherPawn in pawnsWithSameHediff)
                {
                    SkillRecord otherSkill = otherPawn.skills.GetSkill(skill.def);
                    if (otherSkill.TotallyDisabled) continue; // Игнорируем недоступные навыки

                    totalSkill += otherSkill.Level;
                    pawnCount++;
                }

                int averageSkill = totalSkill / pawnCount;

                // Устанавливаем усредненный уровень навыков для всех пешек
                skill.Level = averageSkill;
                foreach (Pawn otherPawn in pawnsWithSameHediff)
                {
                    SkillRecord otherSkill = otherPawn.skills.GetSkill(skill.def);
                    if (otherSkill.TotallyDisabled) continue; // Игнорируем недоступные навыки

                    otherSkill.Level = averageSkill;
                }
            }
        }
    }







}



