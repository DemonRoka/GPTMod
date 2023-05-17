using RimWorld;
using System.Collections.Generic;
using System.Xml;
using Verse;
using System.IO;
using System;
using System.Linq;
using UnityEngine;
using System.Xml.Serialization;
using System.Text;
using RimWorld.Planet;

namespace Phototrophs_Class
{
    public class SymbolDefinition : Def
    {
        public string symbol;
        public ThingDef thingDef;
        public TerrainDef terrainDef;
        public ThingDef stuffDef;
    }
    public class PlanGenBuild : Def
    {
        public List<string> buildingMap;
        public List<string> roofMap;
        public List<string> stuffMap;
        public List<string> terrainMap;
    }
    public static class BuildingMapUtility
    {
        public static void CreateBuildingMap(IntVec3 position, Map map, List<string> buildingMap, List<string> roofMap, List<string> stuffMap, List<string> terrainMap)
        {
            for (int z = 0; z < buildingMap.Count; z++)
            {
                string[] row = buildingMap[z].Split(',');
                string[] roofRow = roofMap[z].Split(',');
                string[] stuffRow = stuffMap[z].Split(',');
                string[] terrainRow = terrainMap[z].Split(',');

                for (int x = 0; x < row.Length; x++)
                {
                    IntVec3 cellPosition = position + new IntVec3(x, 0, z);
                    string cell = row[x];
                    string stuff = stuffRow[x];
                    string terrain = terrainRow[x];

                    SymbolDefinition symbolDef = DefDatabase<SymbolDefinition>.AllDefsListForReading.Find(s => s.defName == cell);
                    SymbolDefinition stuffDef = DefDatabase<SymbolDefinition>.AllDefsListForReading.Find(s => s.defName == stuff);
                    SymbolDefinition terrainDef = DefDatabase<SymbolDefinition>.AllDefsListForReading.Find(s => s.defName == terrain);

                    // Обработка территории
                    if (terrainDef != null && terrainDef.terrainDef != null)
                    {
                        map.terrainGrid.SetTerrain(cellPosition, terrainDef.terrainDef);
                    }

                    if (symbolDef != null)
                    {
                        if (symbolDef.thingDef != null)
                        {
                            Thing thing = ThingMaker.MakeThing(symbolDef.thingDef, stuffDef?.stuffDef); // Использовать stuffDef здесь
                            GenSpawn.Spawn(thing, cellPosition, map);
                        }
                    }
                    else
                    {
                        Log.Error($"No SymbolDefinition found for '{cell}'");
                    }

                    // Обработка крыши
                    if (roofRow[x] == "1")
                    {
                        map.roofGrid.SetRoof(cellPosition, RoofDefOf.RoofConstructed);
                    }
                }
            }
        }
    }


    public class Designator_CreateXML : Designator
    {
        private Dictionary<string, string> symbolLibrary;
        private HashSet<Thing> seenThings = new HashSet<Thing>();

        private List<List<string>> selectedBuildingSymbols = new List<List<string>>();
        private List<List<string>> selectedTerrainSymbols = new List<List<string>>();
        private List<List<string>> selectedRoofSymbols = new List<List<string>>();
        private List<List<string>> selectedStuffSymbols = new List<List<string>>();

        public override int DraggableDimensions => 2;

        public Designator_CreateXML()
        {
            defaultLabel = "DesignatorCreateXML".Translate();
            defaultDesc = "DesignatorCreateXMLDesc".Translate();
            useMouseIcon = true;
            tutorTag = "CreateXML";

            // Load symbol library
            symbolLibrary = LoadSymbolsFromXml();
        }
        private Dictionary<string, string> LoadSymbolsFromXml()
        {
            Dictionary<string, string> symbolDict = new Dictionary<string, string>();

            // Доступ к списку всех определений SymbolDefinition
            List<Phototrophs_Class.SymbolDefinition> symbolDefinitions = DefDatabase<Phototrophs_Class.SymbolDefinition>.AllDefsListForReading;

            foreach (Phototrophs_Class.SymbolDefinition symbolDef in symbolDefinitions)
            {
                string idSymbol = symbolDef.defName;

                if (symbolDef.thingDef != null)
                {
                    symbolDict[symbolDef.thingDef.defName] = idSymbol;
                }

                if (symbolDef.terrainDef != null)
                {
                    symbolDict[symbolDef.terrainDef.defName] = idSymbol;
                }
            }

            return symbolDict;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            return c.InBounds(base.Map);
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            ProcessCell(c, 0, 0);
        }

        public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
        {
            base.DesignateMultiCell(cells);

            // Очистим списки перед началом новой операции
            selectedBuildingSymbols.Clear();
            selectedTerrainSymbols.Clear();
            selectedRoofSymbols.Clear();

            // Создаем список из ячеек для удобства обработки
            List<IntVec3> cellList = cells.ToList();

            // Получаем координаты верхнего левого и нижнего правого углов выделенной области
            int minX = cellList.Min(c => c.x);
            int maxX = cellList.Max(c => c.x);
            int minZ = cellList.Min(c => c.z);
            int maxZ = cellList.Max(c => c.z);

            // Проходим через каждую ячейку в выделенной области
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    // Обрабатываем ячейку
                    IntVec3 currentCell = new IntVec3(x, 0, z);
                    ProcessCell(currentCell, z - minZ, x - minX);
                }
            }

            // Записываем данные в XML
            WriteToXml();
        }

        public override void RenderHighlight(List<IntVec3> dragCells)
        {
            seenThings.Clear();
            foreach (IntVec3 dragCell in dragCells)
            {
                List<Thing> thingList = dragCell.GetThingList(base.Map);
                for (int i = 0; i < thingList.Count; i++)
                {
                    Thing thing = thingList[i];
                    if (!seenThings.Contains(thing))
                    {
                        Vector3 drawPos = thing.DrawPos;
                        drawPos.y = AltitudeLayer.MetaOverlays.AltitudeFor();
                        Graphics.DrawMesh(MeshPool.plane10, drawPos, Quaternion.identity, DesignatorUtility.DragHighlightThingMat, 0);
                        seenThings.Add(thing);
                    }
                }
            }
            seenThings.Clear();
        }

        private void ProcessCell(IntVec3 c, int row, int col)
        {
            // Ensure the row exists in the map.
            while (selectedRoofSymbols.Count <= row)
            {
                selectedRoofSymbols.Add(new List<string>());
            }

            // Ensure the column exists in the map.
            while (selectedRoofSymbols[row].Count <= col)
            {
                selectedRoofSymbols[row].Add("0");
            }

            if (base.Map.roofGrid.Roofed(c))
            {
                selectedRoofSymbols[row][col] = "1";
            }

            // Ensure the row exists in the map.
            while (selectedBuildingSymbols.Count <= row)
            {
                selectedBuildingSymbols.Add(new List<string>());
                selectedTerrainSymbols.Add(new List<string>());
            }

            // Ensure the column exists in the map.
            while (selectedBuildingSymbols[row].Count <= col)
            {
                selectedBuildingSymbols[row].Add("Em");
                selectedTerrainSymbols[row].Add("Em");
            }

            // Ensure the row exists in the map.
            while (selectedStuffSymbols.Count <= row)
            {
                selectedStuffSymbols.Add(new List<string>());
            }

            // Ensure the column exists in the map.
            while (selectedStuffSymbols[row].Count <= col)
            {
                selectedStuffSymbols[row].Add("Em");
            }

            List<Thing> thingList = c.GetThingList(base.Map);
            foreach (Thing thing in thingList)
            {
                string defName = thing.def.defName;
                if (symbolLibrary.ContainsKey(defName))
                {
                    selectedBuildingSymbols[row][col] = symbolLibrary[defName]; // Используем символ из библиотеки, если он есть
                }
                else
                {
                    selectedBuildingSymbols[row][col] = "Em"; // Используем "Em" как пустой символ, если в библиотеке нет символа
                }
            }

            foreach (Thing thing in thingList)
            {
                if (thing.Stuff != null) // Если у вещи есть материал
                {
                    string stuffDefName = thing.Stuff.defName;
                    if (symbolLibrary.ContainsKey(stuffDefName))
                    {
                        selectedStuffSymbols[row][col] = symbolLibrary[stuffDefName]; // Используем символ из библиотеки, если он есть
                    }
                    else
                    {
                        selectedStuffSymbols[row][col] = "Em"; // Используем "Em" как пустой символ, если в библиотеке нет символа
                    }
                }
            }

            TerrainDef terrainDef = base.Map.terrainGrid.TerrainAt(c);
            if (terrainDef != null)
            {
                string terrainDefName = terrainDef.defName;
                if (symbolLibrary.ContainsKey(terrainDefName))
                {
                    selectedTerrainSymbols[row][col] = symbolLibrary[terrainDefName]; // Используем символ из библиотеки, если он есть
                }
                else
                {
                    selectedTerrainSymbols[row][col] = "Em"; // Используем "Em" как пустой символ, если в библиотеке нет символа
                }
            }


        }

        private void WriteToXml()
        {
            string modFolderPath = GenFilePaths.ModsFolderPath;
            string modName = ModLister.GetActiveModWithIdentifier("GPTMood")?.Name ?? "GPTMood";
            string xmlFilePath = Path.Combine(modFolderPath, modName, "GeneratedPlan.xml");

            using (XmlTextWriter writer = new XmlTextWriter(xmlFilePath, System.Text.Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 4;

                writer.WriteStartElement("Phototrophs_Class.PlanGenBuild");

                // записываем карту зданий
                writer.WriteStartElement("buildingMap");
                foreach (List<string> row in selectedBuildingSymbols)
                {
                    writer.WriteElementString("li", string.Join(",", row));
                }
                writer.WriteEndElement(); // Закрывает тег "buildingMap"

                // записываем карту террейна
                writer.WriteStartElement("terrainMap");
                foreach (List<string> row in selectedTerrainSymbols)
                {
                    writer.WriteElementString("li", string.Join(",", row));
                }
                writer.WriteEndElement(); // Закрывает тег "terrainMap"

                // записываем карту крыши
                writer.WriteStartElement("roofMap");
                foreach (List<string> row in selectedRoofSymbols)
                {
                    writer.WriteElementString("li", string.Join(",", row));
                }
                writer.WriteEndElement(); // Закрывает тег "roofMap"

                writer.WriteStartElement("stuffMap");
                foreach (List<string> row in selectedStuffSymbols)
                {
                    writer.WriteElementString("li", string.Join(",", row));
                }
                writer.WriteEndElement(); // Закрывает тег "stuffMap"

                writer.WriteEndElement(); // Закрывает тег "Phototrophs_Class.PlanGenBuild"

            }
        }
    }


    public class CompProperties_AbilitySpawnBuilding : CompProperties_AbilityEffect
    {
        public PlanGenBuild planDef;

        public CompProperties_AbilitySpawnBuilding()
        {
            compClass = typeof(CompAbilitySpawnBuilding);
        }
    }
    public class CompAbilitySpawnBuilding : CompAbilityEffect
    {
        public new CompProperties_AbilitySpawnBuilding Props => (CompProperties_AbilitySpawnBuilding)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Map map = Find.CurrentMap;
            if (map != null)
            {
                if (Props.planDef?.buildingMap != null && Props.planDef?.roofMap != null)
                {
                    int zOffset = Props.planDef.buildingMap.Count >> 1, xOffset = Props.planDef.buildingMap[zOffset - 1].Split(',').Length >> 1;

                    BuildingMapUtility.CreateBuildingMap(target.Cell - new IntVec3(xOffset, 0, zOffset), map, Props.planDef.buildingMap, Props.planDef.roofMap, Props.planDef.stuffMap, Props.planDef.terrainMap);
                }
                else
                {
                    Log.Error($"No building map or roof map provided in CompProperties_AbilitySpawnBuilding");
                }
            }
        }

    }


    public class CompProperties_AbilityLogBuilding : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityLogBuilding()
        {
            this.compClass = typeof(AbilityBuildingLog);
        }
    }
    public class AbilityBuildingLog : CompAbilityEffect
    {
        private int currentId = 0;
        private const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        // Генерация уникального IDsymbol из двух букв
        private string GenerateIdSymbol()
        {
            string idSymbol = alphabet[currentId / alphabet.Length].ToString() + alphabet[currentId % alphabet.Length];
            currentId++;
            return idSymbol;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            // Получаем список всех ThingDef в игре
            IEnumerable<ThingDef> allThingDefs = DefDatabase<ThingDef>.AllDefs;

            // Фильтруем список, оставляя только здания, растения, одежду и расходные материалы
            IEnumerable<ThingDef> allBuildingDefs = allThingDefs.Where(def => def.category == ThingCategory.Building || def.category == ThingCategory.Plant || def.IsApparel || def.IsStuff);

            // Получаем список всех TerrainDef в игре
            IEnumerable<TerrainDef> allTerrainDefs = DefDatabase<TerrainDef>.AllDefs;

            // Записываем defName каждого здания/растения и каждого TerrainDef в XML-файл
            string modFolderPath = GenFilePaths.ModsFolderPath;
            string modName = ModLister.GetActiveModWithIdentifier("GPTMood")?.Name ?? "GPTMood";
            string xmlFilePath = Path.Combine(modFolderPath, modName, "Defs.xml");

            using (XmlTextWriter writer = new XmlTextWriter(xmlFilePath, System.Text.Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 4;

                writer.WriteStartElement("Defs");

                foreach (ThingDef buildingDef in allBuildingDefs)
                {
                    writer.WriteStartElement("Phototrophs_Class.SymbolDefinition");

                    writer.WriteStartElement("defName");
                    writer.WriteString(GenerateIdSymbol());
                    writer.WriteEndElement();

                    writer.WriteStartElement("thingDef");
                    writer.WriteString(buildingDef.defName);
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                }

                foreach (TerrainDef terrainDef in allTerrainDefs)
                {
                    writer.WriteStartElement("Phototrophs_Class.SymbolDefinition");

                    writer.WriteStartElement("defName");
                    writer.WriteString(GenerateIdSymbol());
                    writer.WriteEndElement();

                    writer.WriteStartElement("terrainDef");
                    writer.WriteString(terrainDef.defName);
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.Close();
            }
        }
    }




}

