using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RFF_Code {

	[StaticConstructorOnStartup]
	internal static class RFF_Initializer {
		static RFF_Initializer() {
			HarmonyInstance harmony = HarmonyInstance.Create("net.rainbeau.rimworld.mod.fertilefields");
			harmony.PatchAll( Assembly.GetExecutingAssembly() );
		}
	}

	// ==================================== //
	// ========== Config Options ========== //
	// ==================================== //

	public class Controller : Mod {
		public static Settings Settings;
		public override string SettingsCategory() { return "RFF.FertileFields".Translate(); }
		public override void DoSettingsWindowContents(Rect canvas) { Settings.DoWindowContents(canvas); }
		public Controller(ModContentPack content) : base(content) {
			Settings = GetSettings<Settings>();
		}
	}

	public class Settings : ModSettings {
		public float degradeChancePlowed = 100.5f;
		public float degradeChanceRich = 50.5f;
		public bool autoRefertilize = true;
		public bool autoReplow = true;
		public bool autoRecompost = true;
		public bool playHardMode = false;
		public float plantScrapsPercent = 100.5f;
		//public bool compressedMenu = true;
		public void DoWindowContents(Rect canvas) {
			Listing_Standard list = new Listing_Standard();
			list.ColumnWidth = canvas.width;
			list.Begin(canvas);
			list.Gap(36);
			Text.Font = GameFont.Medium;
			list.Label("RFF.FarmingOptions".Translate());
			Text.Font = GameFont.Small;
			list.Gap(24);
			list.Label("RFF.DegradeChancePlowed".Translate()+"  "+(int)degradeChancePlowed+"%");
			degradeChancePlowed = list.Slider(degradeChancePlowed, 0f, 100.99f);
			list.Gap();
			list.Label("RFF.DegradeChanceRich".Translate()+"  "+(int)degradeChanceRich+"%");
			degradeChanceRich = list.Slider(degradeChanceRich, 0f, 100.99f);
			list.Gap();
			list.CheckboxLabeled( "RFF.AutoRefertilize".Translate(), ref autoRefertilize, "RFF.AutoRefertilizeTip".Translate() );
			list.Gap();
			list.CheckboxLabeled( "RFF.AutoReplow".Translate(), ref autoReplow, "RFF.AutoReplowTip".Translate() );
			list.Gap();
			list.CheckboxLabeled( "RFF.AutoRecompost".Translate(), ref autoRecompost, "RFF.AutoRecompostTip".Translate() );
			list.Gap(36);
			Text.Font = GameFont.Medium;
			list.Label("RFF.TerraformingOptions".Translate());
			Text.Font = GameFont.Small;
			list.Gap(24);
			list.CheckboxLabeled( "RFF.PlayHardMode".Translate(), ref playHardMode, "RFF.PlayHardModeTip".Translate() );
			list.Gap(24);
			list.Label("RFF.PlantScraps".Translate()+"  "+(int)plantScrapsPercent+"%");
			plantScrapsPercent = list.Slider(plantScrapsPercent, 0f, 100.99f);
			//list.Gap(24);
			//list.CheckboxLabeled( "RFF.CompressedMenu".Translate(), ref compressedMenu, "RFF.CompressedMenuTip".Translate() );
			list.Gap();
			list.End();
		}
		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look(ref degradeChancePlowed, "degradeChancePlowed", 100.5f);
			Scribe_Values.Look(ref degradeChanceRich, "degradeChanceRich", 50.5f);
			Scribe_Values.Look(ref autoRefertilize, "autoRefertilize", true);
			Scribe_Values.Look(ref autoReplow, "autoReplow", true);
			Scribe_Values.Look(ref autoRecompost, "autoRecompost", true);
			Scribe_Values.Look(ref playHardMode, "playHardMode", false);
			Scribe_Values.Look(ref plantScrapsPercent, "plantScrapsPercent", 100.5f);
			//Scribe_Values.Look(ref compressedMenu, "compressedMenu", true);
		}
	}	

	// ===================================== //
	// ========== Harmony Patches ========== //
	// ===================================== //

	[HarmonyPatch (typeof (CompRottable), "CompTickRare")]
	public static class CompRottable_CompTickRare {
		static void Prefix (CompRottable __instance, ref State __state) {
			__state = new State (__instance);
		}
		static void Postfix(CompRottable __instance, ref State __state) {
			if (__instance.parent.Destroyed) {
				if (__instance.parent.def.thingCategories == null) { return; }
				if (__instance.parent.def.defName.Contains("__Corpse")) { return; }
				if (__instance.parent.def.defName.Contains("Mizu_")) { return; }
				if (__state.map == null) { return; }
				ThingDef thingDef;
				thingDef = ThingDef.Named("RottedMush");
				var rotItem = ThingMaker.MakeThing (thingDef, null);
				rotItem.stackCount = __state.stackCount;
				if (!__state.map.areaManager.Home[__instance.parent.Position]) {
					rotItem.SetForbidden(true, false);
				}
				GenPlace.TryPlaceThing (rotItem, __instance.parent.Position, __state.map, ThingPlaceMode.Near, null);
			}
		}
		struct State {
			public Map map;
			public int stackCount;
			public State(CompRottable instance) {
				map = instance.parent.Map;
				stackCount = instance.parent.stackCount;
			}
		}
	}

	[HarmonyPatch(typeof(GenConstruct), "BlocksConstruction", null)]
	public static class GenConstruct_BlocksConstruction {
		public static bool Prefix(Thing constructible, Thing t, ref bool __result) {
			ThingDef thingDef;
			if (constructible == null || t == null) {
				return true;
			}
			if (!(constructible is Blueprint)) {
				thingDef = (!(constructible is Frame) ? constructible.def.blueprintDef : constructible.def.entityDefToBuild.blueprintDef);
			}
			else {
				thingDef = constructible.def;
			}
			ThingDef thingDef1 = thingDef.entityDefToBuild as ThingDef;
			if (thingDef1 != null && thingDef1.building != null && thingDef1.building.canPlaceOverWall) {
				if (t.def.defName.Equals("BrickWall")) {
					__result = false;
					return false;
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(GenConstruct), "CanPlaceBlueprintOver", null)]
	public static class GenConstruct_CanPlaceBlueprintOver {
		public static bool Prefix(BuildableDef newDef, ThingDef oldDef, ref bool __result) {
			ThingDef thingDef = newDef as ThingDef;
			ThingDef thingDef1 = oldDef;
			if (thingDef == null || thingDef1 == null) {
				return true;
			}
			BuildableDef buildableDef = GenConstruct.BuiltDefOf(oldDef);
			ThingDef thingDef2 = buildableDef as ThingDef;
			if (thingDef.building != null && thingDef.building.canPlaceOverWall && thingDef2 != null) {
				if (thingDef2.defName.Equals("BrickWall")) {
					__result = true;
					return false;
				}
			}
			return true;
		}
	}
	
	[HarmonyPatch(typeof(GenLeaving), "DoLeavingsFor", new Type[] { typeof(TerrainDef), typeof(IntVec3), typeof(Map) })]
	public static class GenLeaving_DoLeavingsFor {
		public static bool Prefix(TerrainDef terrain, IntVec3 cell, Map map) {
			if (terrain == TerrainDef.Named("Topsoil") || terrain == TerrainDef.Named("DirtFert")) {
				Thing leaving = ThingMaker.MakeThing(ThingDef.Named("PileofDirt"), null);
				leaving.stackCount = 2;
				GenPlace.TryPlaceThing(leaving, cell, map, ThingPlaceMode.Near, null);
				return false;
			}
			if (terrain == TerrainDef.Named("SoilTilled")) {
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(GenSpawn), "SpawningWipes", null)]
	public static class GenSpawn_SpawningWipes {
		public static bool Prefix(BuildableDef newEntDef, BuildableDef oldEntDef, ref bool __result) {
			ThingDef thingDef = newEntDef as ThingDef;
			ThingDef thingDef1 = oldEntDef as ThingDef;
			if (thingDef == null || thingDef1 == null) {
				return true;
			}
			ThingDef thingDef2 = thingDef.entityDefToBuild as ThingDef;
			if (thingDef1.IsBlueprint) {
				if (thingDef.IsBlueprint) {
					if (thingDef2 != null && thingDef2.building != null && thingDef2.building.canPlaceOverWall) {
						if (thingDef1.entityDefToBuild is ThingDef) {
							if (thingDef1.entityDefToBuild.defName.Equals("BrickWall")) {
								__result = true;
								return false;
							}
						}
					}
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(JobDriver_PlantCut), "PlantWorkDoneToil", null)]
	internal static class JobDriver_PlantCut_PlantWorkDoneToil {
		private static bool Prefix(ref Toil __result) {
			Toil toil = new Toil();
			toil.initAction = delegate {
				Pawn actor = toil.actor;
				Plant cutPlant = (Plant)actor.jobs.curJob.GetTarget(TargetIndex.A).Thing;
				ThingDef leavings = ThingDef.Named("PlantScraps");
				var plantScraps = ThingMaker.MakeThing (leavings, null);
				float stackSize = cutPlant.def.plant.harvestYield;
				if (cutPlant.def.defName == "Plant_Grass" || cutPlant.def.defName == "Plant_TallGrass"
				  || cutPlant.def.defName == "Plant_Dandelion" || cutPlant.def.defName == "Plant_Astragalus"
				  || cutPlant.def.defName == "Plant_Moss" || cutPlant.def.defName == "Plant_Daylily") {
					stackSize = 3; 
				}
				else if (stackSize < 9) {
					stackSize = 9;
				}
				if (Rand.Value < 0.33f) {
					stackSize = stackSize * Rand.Value;
				}
				stackSize = stackSize * cutPlant.Growth * (Controller.Settings.plantScrapsPercent/100);
				plantScraps.stackCount = (int)stackSize;
				if (plantScraps.stackCount > 0) {
					GenPlace.TryPlaceThing (plantScraps, actor.Position, actor.Map, ThingPlaceMode.Near, null);
				}
				if (!cutPlant.Destroyed) {
					cutPlant.Destroy(DestroyMode.Vanish);
				}
				IntVec3 cell = toil.actor.jobs.curJob.GetTarget(TargetIndex.A).Cell;
				if (actor.Map.terrainGrid.TerrainAt(cell).defName == "DirtFert" && (Rand.Value < (Controller.Settings.degradeChanceRich/100))) {
					actor.Map.terrainGrid.SetTerrain(cell,TerrainDef.Named("Topsoil"));
					if (Controller.Settings.autoRefertilize.Equals(true) && actor.Map.zoneManager.ZoneAt(cell) is Zone_Growing) {
						GenConstruct.PlaceBlueprintForBuild(ThingDef.Named("Terraform_Topsoil-DirtFert"), cell, actor.Map, Rot4.North, Faction.OfPlayer, null);
					}
				}
				if (actor.Map.terrainGrid.TerrainAt(cell).defName == "SoilRich" && (Rand.Value < (Controller.Settings.degradeChanceRich/100))) {
					actor.Map.terrainGrid.SetTerrain(cell,TerrainDef.Named("Soil"));
					if (Controller.Settings.autoRefertilize.Equals(true) && actor.Map.zoneManager.ZoneAt(cell) is Zone_Growing) {
						GenConstruct.PlaceBlueprintForBuild(ThingDef.Named("Terraform_SoilF-SoilRich"), cell, actor.Map, Rot4.North, Faction.OfPlayer, null);
					}
				}
				if (actor.Map.terrainGrid.TerrainAt(cell).defName == "SoilTilled" && (Rand.Value < (Controller.Settings.degradeChancePlowed/100))) {
					actor.Map.terrainGrid.SetTerrain(cell,TerrainDef.Named("SoilRich"));
					if (Controller.Settings.autoReplow.Equals(true) && actor.Map.zoneManager.ZoneAt(cell) is Zone_Growing) {
						GenConstruct.PlaceBlueprintForBuild(ThingDef.Named("Terraform_SoilRich-SoilTilled"), cell, actor.Map, Rot4.North, Faction.OfPlayer, null);
					}
				}
			};
			__result = toil;
			return false;
		}
	}

	[HarmonyPatch(typeof(JobDriver_PlantCut_Designated), "PlantWorkDoneToil", null)]
	internal static class JobDriver_PlantCut_Designated_PlantWorkDoneToil {
		private static bool Prefix(ref Toil __result) {
			Toil toil = new Toil();
			toil.initAction = delegate {
				Pawn actor = toil.actor;
				Plant cutPlant = (Plant)actor.jobs.curJob.GetTarget(TargetIndex.A).Thing;
				ThingDef leavings = ThingDef.Named("PlantScraps");
				var plantScraps = ThingMaker.MakeThing (leavings, null);
				float stackSize = cutPlant.def.plant.harvestYield;
				if (cutPlant.def.defName == "Plant_Grass" || cutPlant.def.defName == "Plant_TallGrass"
				  || cutPlant.def.defName == "Plant_Dandelion" || cutPlant.def.defName == "Plant_Astragalus"
				  || cutPlant.def.defName == "Plant_Moss" || cutPlant.def.defName == "Plant_Daylily") {
					stackSize = 3; 
				}
				else if (stackSize < 9) {
					stackSize = 9;
				}
				if (Rand.Value < 0.33f) {
					stackSize = stackSize * Rand.Value;
				}
				stackSize = stackSize * cutPlant.Growth * (Controller.Settings.plantScrapsPercent/100);
				plantScraps.stackCount = (int)stackSize;
				if (plantScraps.stackCount > 0) {
					GenPlace.TryPlaceThing (plantScraps, actor.Position, actor.Map, ThingPlaceMode.Near, null);
				}
				if (!cutPlant.Destroyed) {
					cutPlant.Destroy(DestroyMode.Vanish);
				}
				IntVec3 cell = toil.actor.jobs.curJob.GetTarget(TargetIndex.A).Cell;
				if (actor.Map.terrainGrid.TerrainAt(cell).defName == "DirtFert" && (Rand.Value < (Controller.Settings.degradeChanceRich/100))) {
					actor.Map.terrainGrid.SetTerrain(cell,TerrainDef.Named("Topsoil"));
					if (Controller.Settings.autoRefertilize.Equals(true) && actor.Map.zoneManager.ZoneAt(cell) is Zone_Growing) {
						GenConstruct.PlaceBlueprintForBuild(ThingDef.Named("Terraform_Topsoil-DirtFert"), cell, actor.Map, Rot4.North, Faction.OfPlayer, null);
					}
				}
				if (actor.Map.terrainGrid.TerrainAt(cell).defName == "SoilRich" && (Rand.Value < (Controller.Settings.degradeChanceRich/100))) {
					actor.Map.terrainGrid.SetTerrain(cell,TerrainDef.Named("Soil"));
					if (Controller.Settings.autoRefertilize.Equals(true) && actor.Map.zoneManager.ZoneAt(cell) is Zone_Growing) {
						GenConstruct.PlaceBlueprintForBuild(ThingDef.Named("Terraform_SoilF-SoilRich"), cell, actor.Map, Rot4.North, Faction.OfPlayer, null);
					}
				}
				if (actor.Map.terrainGrid.TerrainAt(cell).defName == "SoilTilled" && (Rand.Value < (Controller.Settings.degradeChancePlowed/100))) {
					actor.Map.terrainGrid.SetTerrain(cell,TerrainDef.Named("SoilRich"));
					if (Controller.Settings.autoReplow.Equals(true) && actor.Map.zoneManager.ZoneAt(cell) is Zone_Growing) {
						GenConstruct.PlaceBlueprintForBuild(ThingDef.Named("Terraform_SoilRich-SoilTilled"), cell, actor.Map, Rot4.North, Faction.OfPlayer, null);
					}
				}
			};
			__result = toil;
			return false;
		}
	}
	
	[HarmonyPatch(typeof(JobDriver_PlantHarvest), "PlantWorkDoneToil", null)]
	internal static class JobDriver_PlantHarvest_PlantWorkDoneToil {
		private static void Postfix(ref Toil __result, ref JobDriver_PlantHarvest __instance) {
			Toil _Result = __result;
			Map map = __instance.pawn.Map;
			_Result.initAction = (Action)Delegate.Combine(_Result.initAction, new Action(delegate {
				Pawn actor = _Result.actor;
				Plant cutPlant = (Plant)actor.jobs.curJob.GetTarget(TargetIndex.A).Thing;
				ThingDef leavings = ThingDef.Named("PlantScraps");
				var plantScraps = ThingMaker.MakeThing (leavings, null);
				float stackSize = cutPlant.def.plant.harvestYield;
				if (cutPlant.def.defName == "Plant_Grass" || cutPlant.def.defName == "Plant_TallGrass"
				  || cutPlant.def.defName == "Plant_Dandelion" || cutPlant.def.defName == "Plant_Astragalus"
				  || cutPlant.def.defName == "Plant_Moss" || cutPlant.def.defName == "Plant_Daylily") {
					stackSize = 3; 
				}
				else if (stackSize < 9) {
					stackSize = 9;
				}
				if (Rand.Value < 0.33f) {
					stackSize = stackSize * Rand.Value;
				}
				stackSize = stackSize * Rand.Value;
				stackSize = stackSize * cutPlant.Growth * (Controller.Settings.plantScrapsPercent/100);
				plantScraps.stackCount = (int)stackSize;
				if (plantScraps.stackCount > 0) {
					GenPlace.TryPlaceThing (plantScraps, actor.Position, actor.Map, ThingPlaceMode.Near, null);
				}
				IntVec3 cell = _Result.actor.jobs.curJob.GetTarget(TargetIndex.A).Cell;
				if (actor.Map.terrainGrid.TerrainAt(cell).defName == "DirtFert" && (Rand.Value < (Controller.Settings.degradeChanceRich/100))) {
					actor.Map.terrainGrid.SetTerrain(cell,TerrainDef.Named("Topsoil"));
					if (Controller.Settings.autoRefertilize.Equals(true) && map.zoneManager.ZoneAt(cell) is Zone_Growing) {
						GenConstruct.PlaceBlueprintForBuild(ThingDef.Named("Terraform_Topsoil-DirtFert"), cell, map, Rot4.North, Faction.OfPlayer, null);
					}
				}
				if (actor.Map.terrainGrid.TerrainAt(cell).defName == "SoilRich" && (Rand.Value < (Controller.Settings.degradeChanceRich/100))) {
					actor.Map.terrainGrid.SetTerrain(cell,TerrainDef.Named("Soil"));
					if (Controller.Settings.autoRefertilize.Equals(true) && map.zoneManager.ZoneAt(cell) is Zone_Growing) {
						GenConstruct.PlaceBlueprintForBuild(ThingDef.Named("Terraform_SoilF-SoilRich"), cell, map, Rot4.North, Faction.OfPlayer, null);
					}
				}
				if (actor.Map.terrainGrid.TerrainAt(cell).defName == "SoilTilled" && (Rand.Value < (Controller.Settings.degradeChancePlowed/100))) {
					actor.Map.terrainGrid.SetTerrain(cell,TerrainDef.Named("SoilRich"));
					if (Controller.Settings.autoReplow.Equals(true) && map.zoneManager.ZoneAt(cell) is Zone_Growing) {
						GenConstruct.PlaceBlueprintForBuild(ThingDef.Named("Terraform_SoilRich-SoilTilled"), cell, map, Rot4.North, Faction.OfPlayer, null);
					}
				}
			}));
		}
	}

	[HarmonyPatch(typeof(JobDriver_PlantHarvest_Designated), "PlantWorkDoneToil", null)]
	internal static class JobDriver_PlantHarvest_Designated_PlantWorkDoneToil {
		private static void Postfix(ref Toil __result, ref JobDriver_PlantHarvest __instance) {
			Toil _Result = __result;
			Map map = __instance.pawn.Map;
			_Result.initAction = (Action)Delegate.Combine(_Result.initAction, new Action(delegate {
				Pawn actor = _Result.actor;
				Plant cutPlant = (Plant)actor.jobs.curJob.GetTarget(TargetIndex.A).Thing;
				ThingDef leavings = ThingDef.Named("PlantScraps");
				var plantScraps = ThingMaker.MakeThing (leavings, null);
				float stackSize = cutPlant.def.plant.harvestYield;
				if (cutPlant.def.defName == "Plant_Grass" || cutPlant.def.defName == "Plant_TallGrass"
				  || cutPlant.def.defName == "Plant_Dandelion" || cutPlant.def.defName == "Plant_Astragalus"
				  || cutPlant.def.defName == "Plant_Moss" || cutPlant.def.defName == "Plant_Daylily") {
					stackSize = 3; 
				}
				else if (stackSize < 9) {
					stackSize = 9;
				}
				if (Rand.Value < 0.33f) {
					stackSize = stackSize * Rand.Value;
				}
				stackSize = stackSize * Rand.Value;
				stackSize = stackSize * cutPlant.Growth * (Controller.Settings.plantScrapsPercent/100);
				plantScraps.stackCount = (int)stackSize;
				if (plantScraps.stackCount > 0) {
					GenPlace.TryPlaceThing (plantScraps, actor.Position, actor.Map, ThingPlaceMode.Near, null);
				}
				IntVec3 cell = _Result.actor.jobs.curJob.GetTarget(TargetIndex.A).Cell;
				if (actor.Map.terrainGrid.TerrainAt(cell).defName == "DirtFert" && (Rand.Value < (Controller.Settings.degradeChanceRich/100))) {
					actor.Map.terrainGrid.SetTerrain(cell,TerrainDef.Named("Topsoil"));
					if (Controller.Settings.autoRefertilize.Equals(true) && map.zoneManager.ZoneAt(cell) is Zone_Growing) {
						GenConstruct.PlaceBlueprintForBuild(ThingDef.Named("Terraform_Topsoil-DirtFert"), cell, map, Rot4.North, Faction.OfPlayer, null);
					}
				}
				if (actor.Map.terrainGrid.TerrainAt(cell).defName == "SoilRich" && (Rand.Value < (Controller.Settings.degradeChanceRich/100))) {
					actor.Map.terrainGrid.SetTerrain(cell,TerrainDef.Named("Soil"));
					if (Controller.Settings.autoRefertilize.Equals(true) && map.zoneManager.ZoneAt(cell) is Zone_Growing) {
						GenConstruct.PlaceBlueprintForBuild(ThingDef.Named("Terraform_SoilF-SoilRich"), cell, map, Rot4.North, Faction.OfPlayer, null);
					}
				}
				if (actor.Map.terrainGrid.TerrainAt(cell).defName == "SoilTilled" && (Rand.Value < (Controller.Settings.degradeChancePlowed/100))) {
					actor.Map.terrainGrid.SetTerrain(cell,TerrainDef.Named("SoilRich"));
					if (Controller.Settings.autoReplow.Equals(true) && map.zoneManager.ZoneAt(cell) is Zone_Growing) {
						GenConstruct.PlaceBlueprintForBuild(ThingDef.Named("Terraform_SoilRich-SoilTilled"), cell, map, Rot4.North, Faction.OfPlayer, null);
					}
				}
			}));
		}
	}
	
	[HarmonyPatch(typeof(WorkGiver_ConstructDeliverResourcesToBlueprints), "JobOnThing")]
	public static class WorkGiver_ConstructDeliverResourcesToBlueprints_JobOnThing {
		static bool Prefix(Pawn pawn, Thing t, ref Job __result, bool forced = false) {
			if (t.Faction != pawn.Faction) {
				__result = null;
				return false;
			}
			Blueprint blueprint = t as Blueprint;
			if (blueprint == null) {
				__result = null;
				return false;
			}
			Thing check = (Thing)blueprint;
			if (check.def.defName.Contains("Stone-Topsoil") || check.def.defName.Contains("Topsoil-DirtFert") || check.def.defName.Contains("SoilF-SoilRich") || check.def.defName.Contains("SoilRich-SoilTilled")) {
				__result = null;
				return false;
			}
			return true;
		}
	}
	
	[HarmonyPatch(typeof(WorkGiver_ConstructDeliverResourcesToBlueprints), "NoCostFrameMakeJobFor")]
	public static class WorkGiver_ConstructDeliverResourcesToBlueprints_NoCostFrameMakeJobFor {
		static bool Prefix(Pawn pawn, IConstructible c, ref Job __result) {
			if (c is Blueprint && c.MaterialsNeeded().Count == 0) {
				Thing check = (Thing)c;
				if (check.def.defName.Contains("Stone-Topsoil") || check.def.defName.Contains("Topsoil-DirtFert") || check.def.defName.Contains("SoilF-SoilRich") || check.def.defName.Contains("SoilRich-SoilTilled")) {
					__result = null;
					return false;
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(WorkGiver_ConstructFinishFrames), "JobOnThing")]
	public static class WorkGiver_ConstructFinishFrames_JobOnThing {
		static bool Prefix(Pawn pawn, Thing t, ref Job __result, bool forced = false) {
			if (t.Faction != pawn.Faction) {
				__result = null;
				return false;
			}
			Frame frame = t as Frame;
			if (frame == null) {
				__result = null;
				return false;
			}
			if (frame.MaterialsNeeded().Count > 0) {
				__result = null;
				return false;
			}
			if (GenConstruct.FirstBlockingThing(frame, pawn) != null) {
				__result = GenConstruct.HandleBlockingThingJob(frame, pawn, forced);
				return false;
			}
			if (!GenConstruct.CanConstruct(frame, pawn, forced)) {
				__result = null;
				return false;
			}
			if (frame.def.defName.Contains("Terraform_")) {
				if (frame.def.defName.Contains("Stone-Topsoil") || frame.def.defName.Contains("Topsoil-DirtFert") || frame.def.defName.Contains("SoilF-SoilRich") || frame.def.defName.Contains("SoilRich-SoilTilled")) {
					__result = null;
					return false;
				}
				__result = new Job(JobDefOf.FinishFrameConstruction, frame);
				return false;
			}
			__result = new Job(JobDefOf.FinishFrame, frame);
			return false;
		}
	}

	// ========================== //
	// ========== Defs ========== //
	// ========================== //

	public class Terrain : DefModExtension {
		public List<TerrainDef> above;
		public List<TerrainDef> near;
		public TerrainDef result;
		public List<ThingDefCountClass> products;
	}

	[DefOf]
	public static class ThingDefOf {
		public static ThingDef CompostBarrel;
		public static ThingDef Fertilizer;
		public static ThingDef RawCompost;		
	}
	
	[DefOf]
	public static class JobDefOf {
		public static JobDef EmptyCompostBarrel;
		public static JobDef FillCompostBarrel;
		public static JobDef FinishFrame;
		public static JobDef FinishFrameGrowing;
		public static JobDef FinishFrameConstruction;
		public static JobDef PlaceNoCostFrame;
	}

	[DefOf]
	public static class TerrainDefOf {
		public static TerrainDef RockySoil;
		public static TerrainDef Gravel;
		public static TerrainDef Sand;
		public static TerrainDef WaterShallow;
		public static TerrainDef WaterDeep;
	}

	// ================================== //
	// ========== PlaceWorkers ========== //
	// ================================== //

	public class PlaceWorker_Dynamic : PlaceWorker {
		public override AcceptanceReport AllowsPlacing (BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null) {
			var config = checkingDef.GetModExtension<Terrain> ();
			var terrainDef = map.terrainGrid.TerrainAt (loc);
			bool above = false, aboveSkip = true;
			if (config.above != null) {
				if (config.above.Contains(TerrainDef.Named("Granite_Rough"))) {
					Thing thing = loc.GetFirstMineable(map);
					if (thing == null) {
						if (terrainDef.defName.Contains("_Smooth") || terrainDef.defName.Contains("_Rough")) {
							above = true;
						}
					}
				}
				else if (config.above.Contains(TerrainDef.Named("Granite_Smooth"))) {
					if (terrainDef.defName.Contains("_Smooth")) {
						above = true;
					}
					if (terrainDef.defName.Contains("Ice")) {
						above = true;
					}
				}
				else if (config.above.Contains(TerrainDef.Named("Ice"))) {
					if (Controller.Settings.playHardMode.Equals(true) && (map.Biome.defName == "IceSheet" || map.Biome.defName == "SeaIce")) {
						return new AcceptanceReport ("RFF.HardMode".Translate ());
					}
					above = true;
				}
				else if (config.above.Contains(TerrainDef.Named("WaterDeep"))) {
					if (Controller.Settings.playHardMode.Equals(true) && terrainDef.defName.Contains("Ocean")) {
						return new AcceptanceReport ("RFF.HardMode".Translate ());
					}
					above = true;
				}
				else {
					above = config.above.Contains(terrainDef);
				}
				aboveSkip = false;
			}
			bool near = false, nearSkip = true;
			if (config.near != null) {
				near = FindInAdjacentCellsAround (map, loc, config.near.Contains);
				nearSkip = false;
			}
			if ((aboveSkip || above) && (nearSkip || near)) {
				return true;
			}
			else {
				return new AcceptanceReport ("RFF.Unplaceable".Translate (terrainDef.label));
			}
		}
		static bool FindInAdjacentCellsAround(Map map, IntVec3 loc, Predicate<TerrainDef> p) {
			for (int i = -1; i < 2; i++) {
				for (int j = -1; j < 2; j++) {
					int x = loc.x + i;
					int z = loc.z + j;
					IntVec3 newSpot = new IntVec3(x, 0, z);
					var terrainAroundDef = map.terrainGrid.TerrainAt (newSpot);
					if (p.Invoke(terrainAroundDef)) { return true; }
				}
			}
			for (int i = -2; i < 3; i++) {
				for (int j = -2; j < 3; j++) {
					int x = loc.x + i;
					int z = loc.z + j;
					IntVec3 newSpot = new IntVec3(x, 0, z);
					var terrainAroundDef = map.terrainGrid.TerrainAt (newSpot);
					if (p.Invoke(terrainAroundDef)) { return true; }
				}
			}
			for (int i = -3; i < 4; i++) {
				for (int j = -3; j < 4; j++) {
					int x = loc.x + i;
					int z = loc.z + j;
					IntVec3 newSpot = new IntVec3(x, 0, z);
					var terrainAroundDef = map.terrainGrid.TerrainAt (newSpot);
					if (p.Invoke(terrainAroundDef)) { return true; }
				}
			}
			return false;
		}
	}

	// ================================== //
	// ========== Terraforming ========== //
	// ================================== //
	
	public class Building_Terraform : Building {
		public override void SpawnSetup(Map map, bool respawningAfterLoad) {
			base.SpawnSetup(map, respawningAfterLoad);
			PlaceProduct();
			Destroy (DestroyMode.Vanish);
		}
		void PlaceProduct() {
			var terrain = def.GetModExtension<Terrain> ();
			if (terrain == null) {
				Log.Error ("Fertile Fields :: " + this + " lacks " + typeof(Terrain) + " ModExtension"); 
				return;
			}
			if (terrain.products != null) {
				Thing thing;
				foreach(var product in terrain.products) {
					if (product.thingDef.defName == "ChunkGranite") {
						System.Random rnd = new System.Random();
						int ChunkChance = rnd.Next(4);
						if (ChunkChance == 0) {
							TerrainDef terrainDef = base.Map.terrainGrid.TerrainAt(base.Position);
							string chunkType = "Granite";
							if (terrainDef.defName.Contains("_Smooth")) {
								chunkType = terrainDef.defName.Substring(0,terrainDef.defName.Length-7);
							}
							else if (terrainDef.defName.Contains("_RoughHewn")) {
								chunkType = terrainDef.defName.Substring(0,terrainDef.defName.Length-10);
							}
							else if (terrainDef.defName.Contains("_Rough")) {
								chunkType = terrainDef.defName.Substring(0,terrainDef.defName.Length-6);
							}
							chunkType = "Chunk"+chunkType;
							GenPlace.TryPlaceThing(ThingMaker.MakeThing(ThingDef.Named(chunkType), null), base.Position, base.Map, ThingPlaceMode.Near, null);
						}
					}
					else {
						thing = ThingMaker.MakeThing (product.thingDef, null);
						thing.stackCount = product.count;
						GenPlace.TryPlaceThing (thing, Position, Map, ThingPlaceMode.Near, null);
					}
				}
			} 
			if (terrain.result != null) {
				if (terrain.result == TerrainDef.Named("Granite_Rough")) {
					TerrainDef rock = RockAt(Map, Position);
					Map.terrainGrid.SetTerrain(Position, rock);
				}
				else if (terrain.result == TerrainDef.Named("WaterShallow")) {
					if (NearOcean()) {
						Map.snowGrid.SetDepth(base.Position, 0f);
						Map.terrainGrid.SetTerrain (Position, TerrainDef.Named("WaterOceanShallow"));
					}
					else if (NearRiver()) {
						Map.snowGrid.SetDepth(base.Position, 0f);
						Map.terrainGrid.SetTerrain (Position, TerrainDef.Named("WaterMovingShallow"));
					}
					else {
						if (!Map.terrainGrid.TerrainAt(Position).defName.Contains("Ice")) {
							Map.snowGrid.SetDepth(base.Position, 0f);
							Map.terrainGrid.SetTerrain (Position, TerrainDef.Named("WaterShallow"));
						}
						else if (Map.Biome.defName == "SeaIce") {
							Map.snowGrid.SetDepth(base.Position, 0f);
							Map.terrainGrid.SetTerrain (Position, TerrainDef.Named("WaterShallow"));
						}
						else {
							bool nearGravel = false;
							for (int i = -1; i < 2; i++) {
								for (int j = -1; j < 2; j++) {
									int x = Position.x + i;
									int z = Position.z + j;
									IntVec3 newSpot = new IntVec3(x, 0, z);
									var terrainAroundDef = Map.terrainGrid.TerrainAt (newSpot);
									if (!terrainAroundDef.defName.Contains("Water")) { nearGravel = true; break; }
								}
							}
							if (nearGravel.Equals(false)) {
								for (int i = -2; i < 3; i++) {
									for (int j = -2; j < 3; j++) {
										int x = Position.x + i;
										int z = Position.z + j;
										IntVec3 newSpot = new IntVec3(x, 0, z);
										var terrainAroundDef = Map.terrainGrid.TerrainAt (newSpot);
										if (!terrainAroundDef.defName.Contains("Water")) { nearGravel = true; break; }
									}
								}
							}
							if (nearGravel.Equals(false)) {
								for (int i = -3; i < 4; i++) {
									for (int j = -3; j < 4; j++) {
										int x = Position.x + i;
										int z = Position.z + j;
										IntVec3 newSpot = new IntVec3(x, 0, z);
										var terrainAroundDef = Map.terrainGrid.TerrainAt (newSpot);
										if (!terrainAroundDef.defName.Contains("Water")) { nearGravel = true; break; }
									}
								}
							}
							if (NearLake()) {
								if (nearGravel.Equals(true) && Rand.Value < 0.5f) {
									Map.terrainGrid.SetTerrain (Position, TerrainDef.Named("Gravel"));
								}
								else {
									if (Rand.Value < 0.25f) {
										Map.terrainGrid.SetTerrain (Position, TerrainDef.Named("Gravel"));
									}
									else {
										Map.snowGrid.SetDepth(base.Position, 0f);
										Map.terrainGrid.SetTerrain (Position, TerrainDef.Named("WaterShallow"));
									}
								}
							}
							else if (nearGravel.Equals(true) && Rand.Value < 0.95f) {
								Map.terrainGrid.SetTerrain (Position, TerrainDef.Named("Gravel"));
							}
							else {
								if (Rand.Value < 0.75f) {
									Map.terrainGrid.SetTerrain (Position, TerrainDef.Named("Gravel"));
								}
								else {
									Map.snowGrid.SetDepth(base.Position, 0f);
									Map.terrainGrid.SetTerrain (Position, TerrainDef.Named("WaterShallow"));
								}
							}
						}
					}
				}
				else if (terrain.result == TerrainDef.Named("WaterDeep")) {
					Map.snowGrid.SetDepth(base.Position, 0f);
					if (NearOcean()) {
						Map.terrainGrid.SetTerrain (Position, TerrainDef.Named("WaterOceanDeep"));
					}
					else if (NearRiver()) {
						Map.terrainGrid.SetTerrain (Position, TerrainDef.Named("WaterMovingChestDeep"));
					}
					else {
						Map.terrainGrid.SetTerrain (Position, TerrainDef.Named("WaterDeep"));
					}
				}
				else {
					Map.terrainGrid.SetTerrain (Position, terrain.result);
				}
			}
		}
		public bool NearOcean () {
			for (int i = -1; i < 2; i++) {
				for (int j = -1; j < 2; j++) {
					int x = base.Position.x + i;
					int z = base.Position.z + j;
					IntVec3 newSpot = new IntVec3(x, 0, z);
					string terrainCheck = base.Map.terrainGrid.TerrainAt(newSpot).defName;
					if (terrainCheck.Contains("Ocean")) {
						return true;
					}
				}
			}
			return false;
		}
		public bool NearRiver () {
			if (base.Map.waterInfo.riverOffsetMap == null) {
				return false;
			}
			if (base.Map.waterInfo.riverFlowMap == null) {
				return false;
			}
			IntVec3 intVec3 = new IntVec3(Mathf.FloorToInt(base.Position.x), 0, Mathf.FloorToInt(base.Position.z));
			IntVec3 intVec31 = new IntVec3(Mathf.FloorToInt(base.Position.x) + 1, 0, Mathf.FloorToInt(base.Position.z) + 1);
			if (!base.Map.waterInfo.riverFlowMapBounds.Contains(intVec3) || !base.Map.waterInfo.riverFlowMapBounds.Contains(intVec31)) {
				return false;
			}
            int num = base.Map.waterInfo.riverFlowMapBounds.IndexOf(intVec3);
            int num1 = num + 1;
            int width = num + base.Map.waterInfo.riverFlowMapBounds.Width;
            int num2 = width + 1;
            Vector3 vector3 = Vector3.Lerp(new Vector3(base.Map.waterInfo.riverFlowMap[num * 2], 0f, base.Map.waterInfo.riverFlowMap[num * 2 + 1]), new Vector3(base.Map.waterInfo.riverFlowMap[num1 * 2], 0f, base.Map.waterInfo.riverFlowMap[num1 * 2 + 1]), base.Position.x - Mathf.Floor(base.Position.x));
            Vector3 vector31 = Vector3.Lerp(new Vector3(base.Map.waterInfo.riverFlowMap[width * 2], 0f, base.Map.waterInfo.riverFlowMap[width * 2 + 1]), new Vector3(base.Map.waterInfo.riverFlowMap[num2 * 2], 0f, base.Map.waterInfo.riverFlowMap[num2 * 2 + 1]), base.Position.x - Mathf.Floor(base.Position.x));
            if (Vector3.Lerp(vector3, vector31, base.Position.z - (float)Mathf.FloorToInt(base.Position.z)) == Vector3.zero) {
            	return false;
            }
            return true;
			//for (int i = -1; i < 2; i++) {
			//	for (int j = -1; j < 2; j++) {
			//		int x = base.Position.x + i;
			//		int z = base.Position.z + j;
			//		IntVec3 newSpot = new IntVec3(x, 0, z);
			//		string terrainCheck = base.Map.terrainGrid.TerrainAt(newSpot).defName;
			//		if (terrainCheck.Contains("Moving")) {
			//			return true;
			//		}
			//	}
			//}
			//return false;
		}
		public bool NearLake () {
			for (int i = -1; i < 2; i++) {
				for (int j = -1; j < 2; j++) {
					int x = base.Position.x + i;
					int z = base.Position.z + j;
					IntVec3 newSpot = new IntVec3(x, 0, z);
					string terrainCheck = base.Map.terrainGrid.TerrainAt(newSpot).defName;
					if (terrainCheck.Contains("Water")) {
						return true;
					}
				}
			}
			return false;
		}
		static TerrainDef RockAt(Map map, IntVec3 pos) {
			string rockType = "nada";
			for (int i = -1; i < 2; i++) {
				for (int j = -1; j < 2; j++) {
					int x = pos.x + i;
					int z = pos.z + j;
					IntVec3 newSpot = new IntVec3(x, 0, z);
					string terrainCheck = map.terrainGrid.TerrainAt(newSpot).defName;
					if (terrainCheck.Contains("_Smooth")) {
						rockType = terrainCheck.Substring(0,terrainCheck.Length-7);
						if (Rand.Value < 0.5f) { break; }
					}
					else if (terrainCheck.Contains("_RoughHewn")) {
						rockType = terrainCheck.Substring(0,terrainCheck.Length-10);
						if (Rand.Value < 0.5f) { break; }
					}
					else if (terrainCheck.Contains("_Rough")) {
						rockType = terrainCheck.Substring(0,terrainCheck.Length-6);
						if (Rand.Value < 0.5f) { break; }
					}
				}
			}
			if (rockType == "nada") {
				for (int i = -2; i < 3; i++) {
					for (int j = -2; j < 3; j++) {
						int x = pos.x + i;
						int z = pos.z + j;
						IntVec3 newSpot = new IntVec3(x, 0, z);
						string terrainCheck = map.terrainGrid.TerrainAt(newSpot).defName;
						if (terrainCheck.Contains("_Smooth")) {
							rockType = terrainCheck.Substring(0,terrainCheck.Length-7);
							if (Rand.Value < 0.5f) { break; }
						}
						else if (terrainCheck.Contains("_RoughHewn")) {
							rockType = terrainCheck.Substring(0,terrainCheck.Length-10);
							if (Rand.Value < 0.5f) { break; }
						}
						else if (terrainCheck.Contains("_Rough")) {
							rockType = terrainCheck.Substring(0,terrainCheck.Length-6);
							if (Rand.Value < 0.5f) { break; }
						}
					}
				}
			}
			if (rockType == "nada" && Rand.Value < 0.75f) {
				for (int i = -3; i < 4; i++) {
					for (int j = -3; j < 4; j++) {
						int x = pos.x + i;
						int z = pos.z + j;
						IntVec3 newSpot = new IntVec3(x, 0, z);
						string terrainCheck = map.terrainGrid.TerrainAt(newSpot).defName;
						if (terrainCheck.Contains("_Smooth")) {
							rockType = terrainCheck.Substring(0,terrainCheck.Length-7);
							if (Rand.Value < 0.5f) { break; }
						}
						else if (terrainCheck.Contains("_RoughHewn")) {
							rockType = terrainCheck.Substring(0,terrainCheck.Length-10);
							if (Rand.Value < 0.5f) { break; }
						}
						else if (terrainCheck.Contains("_Rough")) {
							rockType = terrainCheck.Substring(0,terrainCheck.Length-6);
							if (Rand.Value < 0.5f) { break; }
						}
					}
				}
			}
			if (rockType == "nada") {
				ThingDef thingDef = Find.World.NaturalRockTypesIn(map.Tile).RandomElement<ThingDef>();
				rockType = thingDef.defName;
			}
			string newRock = rockType+"_Rough";
			return TerrainDef.Named(newRock);
		}
	}

	public class JobDriver_ConstructFinishFrameGrowing : JobDriver {
		private const int JobEndInterval = 5000;
		private Frame Frame {
			get { return (Frame)this.job.GetTarget(TargetIndex.A).Thing; }
		}
		public override bool TryMakePreToilReservations(bool errorOnFailed) {
			return this.pawn.Reserve(this.job.targetA, this.job, 1, -1, null, errorOnFailed);
		}
		[DebuggerHidden]
		protected override IEnumerable<Toil> MakeNewToils() {
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
			Toil build = new Toil();
			build.initAction = delegate {
				GenClamor.DoClamor(build.actor, 15f, ClamorDefOf.Construction);
			};
			build.tickAction = delegate {
				Pawn actor = build.actor;
				Frame frame = this.Frame;
				actor.skills.Learn(SkillDefOf.Plants, 0.06f, false);
				float statValue = actor.GetStatValue(StatDefOf.PlantWorkSpeed, true);
				float workToMake = frame.WorkToBuild;
				if (actor.Faction == Faction.OfPlayer) {
					float successChance = 0.95f-((20-actor.skills.GetSkill(SkillDefOf.Plants).Level)/100);
					if (Rand.Value < 1f - Mathf.Pow(successChance, statValue / workToMake)) {
						frame.FailConstruction(actor);
						this.ReadyForNextToil();
						return;
					}
				}
				if (frame.def.entityDefToBuild is TerrainDef) {
					this.Map.snowGrid.SetDepth(frame.Position, 0f);
				}
				frame.workDone += statValue;
				if (frame.workDone >= workToMake) {
					frame.CompleteConstruction(actor);
					this.ReadyForNextToil();
					return;
				}
			};
			build.WithEffect(() => ((Frame)build.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing).ConstructionEffect, TargetIndex.A);
			build.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			build.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
			build.FailOn(() => !GenConstruct.CanConstruct(this.Frame, this.pawn, false));
			build.defaultCompleteMode = ToilCompleteMode.Delay;
			build.defaultDuration = 5000;
			yield return build;
		}
	}

	public class JobDriver_ConstructFinishFrameConstruction : JobDriver {
		private const int JobEndInterval = 5000;
		private Frame Frame {
			get { return (Frame)this.job.GetTarget(TargetIndex.A).Thing; }
		}
		public override bool TryMakePreToilReservations(bool errorOnFailed) {
			return this.pawn.Reserve(this.job.targetA, this.job, 1, -1, null, errorOnFailed);
		}
		[DebuggerHidden]
		protected override IEnumerable<Toil> MakeNewToils() {
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
			Toil build = new Toil();
			build.initAction = delegate {
				GenClamor.DoClamor(build.actor, 15f, ClamorDefOf.Construction);
			};
			build.tickAction = delegate {
				Pawn actor = build.actor;
				Frame frame = this.Frame;
				actor.skills.Learn(SkillDefOf.Construction, 0.06f, false);
				float statValue = actor.GetStatValue(StatDefOf.ConstructionSpeed, true);
				float workToMake = frame.WorkToBuild;
				if (actor.Faction == Faction.OfPlayer) {
					float statValue2 = actor.GetStatValue(StatDefOf.ConstructSuccessChance, true);
					if (Rand.Value < 1f - Mathf.Pow(statValue2, statValue / workToMake)) {
						frame.FailConstruction(actor);
						this.ReadyForNextToil();
						return;
					}
				}
				if (frame.def.entityDefToBuild is TerrainDef) {
					this.Map.snowGrid.SetDepth(frame.Position, 0f);
				}
				frame.workDone += statValue;
				if (frame.workDone >= workToMake) {
					frame.CompleteConstruction(actor);
					this.ReadyForNextToil();
					return;
				}
			};
			build.WithEffect(() => ((Frame)build.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing).ConstructionEffect, TargetIndex.A);
			build.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			build.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
			build.FailOn(() => !GenConstruct.CanConstruct(this.Frame, this.pawn, false));
			build.defaultCompleteMode = ToilCompleteMode.Delay;
			build.defaultDuration = 5000;
			yield return build;
		}
	}

	public class WorkGiver_ConstructFinishFramesGrowing : WorkGiver_Scanner {
		public override PathEndMode PathEndMode {
			get { return PathEndMode.Touch; }
		}
		public override ThingRequest PotentialWorkThingRequest {
			get { return ThingRequest.ForGroup(ThingRequestGroup.BuildingFrame); }
		}
		public override Danger MaxPathDanger(Pawn pawn) {
			return Danger.Deadly;
		}
		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) {
			if (t.Faction != pawn.Faction) {
				return null;
			}
			Frame frame = t as Frame;
			if (frame == null) {
				return null;
			}
			if (frame.MaterialsNeeded().Count > 0) {
				return null;
			}
			if (GenConstruct.FirstBlockingThing(frame, pawn) != null) {
				return GenConstruct.HandleBlockingThingJob(frame, pawn, forced);
			}
			if (!GenConstruct.CanConstruct(frame, pawn, forced)) {
				return null;
			}
			if (frame.def.defName.Contains("Stone-Topsoil") || frame.def.defName.Contains("Topsoil-DirtFert") || frame.def.defName.Contains("SoilF-SoilRich") || frame.def.defName.Contains("SoilRich-SoilTilled")) {
				return new Job(JobDefOf.FinishFrameGrowing, frame);
			}
			return null;
		}
	}

	public class WorkGiver_ConstructDeliverResourcesToBlueprintsGrowing : WorkGiver_ConstructDeliverResources {
		public override ThingRequest PotentialWorkThingRequest {
			get { return ThingRequest.ForGroup(ThingRequestGroup.Blueprint); }
		}
		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) {
			if (t.Faction != pawn.Faction) {
				return null;
			}
			Blueprint blueprint = t as Blueprint;
			if (blueprint == null) {
				return null;
			}
			Thing check = (Thing)blueprint;
			if (!check.def.defName.Contains("Stone-Topsoil") && !check.def.defName.Contains("Topsoil-DirtFert") && !check.def.defName.Contains("SoilF-SoilRich") && !check.def.defName.Contains("SoilRich-SoilTilled")) {
				return null;
			}
			if (GenConstruct.FirstBlockingThing(blueprint, pawn) != null) {
				return GenConstruct.HandleBlockingThingJob(blueprint, pawn, forced);
			}
			if (!GenConstruct.CanConstruct(blueprint, pawn, forced)) {
				return null;
			}
			Job job = base.RemoveExistingFloorJob(pawn, blueprint);
			if (job != null) {
				return job;
			}
			Job job2 = base.ResourceDeliverJobFor(pawn, blueprint, true);
			if (job2 != null) {
				return job2;
			}
			Job job3 = this.NoCostFrameMakeJobFor(pawn, blueprint);
			if (job3 != null) {
				return job3;
			}
			return null;
		}
		private Job NoCostFrameMakeJobFor(Pawn pawn, IConstructible c) {
			if (c is Blueprint_Install) {
				return null;
			}
			if (c is Blueprint && c.MaterialsNeeded().Count == 0) {
				Thing check = (Thing)c;
				if (check.def.defName.Contains("Stone-Topsoil") || check.def.defName.Contains("Topsoil-DirtFert") || check.def.defName.Contains("SoilF-SoilRich") || check.def.defName.Contains("SoilRich-SoilTilled")) {
					return new Job(JobDefOf.PlaceNoCostFrame) {
						targetA = (Thing)c
					};
				}
			}
			return null;
		}
	}

	// =============================== //
	// ========== Cremating ========== //
	// =============================== //

	public class IngredientValueGetter_Mass : IngredientValueGetter {
		public IngredientValueGetter_Mass() { }
		public override string BillRequirementsDescription(RecipeDef r, IngredientCount ing) {
			return string.Concat("RFF.BillRequires".Translate(new object[] { ing.GetBaseCount() }), " (", ing.filter.Summary, ")");
		}
		public override float ValuePerUnitOf(ThingDef t) {
			return t.GetStatValueAbstract(StatDefOf.Mass, null);
		}
	}
	
	// ================================= //
	// ========== Compost Bin ========== //
	// ================================= //

	public class Building_CompostBin : Building {
		private CompCompostBin compostBinComp;
		private int fermentTicks;
		private int TargetTicks {
			get {
				return this.compostBinComp.Props.fermentTicks;
			}
		}
		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look<int>(ref this.fermentTicks, "fermentTicks", 0, false);
		}
		public override void SpawnSetup(Map currentGame, bool respawningAfterLoad) {
			base.SpawnSetup(currentGame, respawningAfterLoad);
			this.compostBinComp = base.GetComp<CompCompostBin>();
		}
		public override void TickRare() {
			base.TickRare();
			if (this.fermentTicks < this.TargetTicks) {
				this.fermentTicks++;
			}
			if (this.fermentTicks >= this.TargetTicks) {
				this.PlaceProduct();
			}
		}
		private void PlaceProduct() {
			IntVec3 position = base.Position;
			Map map = base.Map;
			Thing product = ThingMaker.MakeThing(ThingDef.Named(this.compostBinComp.Props.product), null);
			product.stackCount = 3;
			GenPlace.TryPlaceThing(product, position, map, ThingPlaceMode.Near, null);
			System.Random rnd = new System.Random();
			int Chance1 = rnd.Next(3);
			int Chance2 = rnd.Next(3); 
			if (Chance1 < 2) {
				GenPlace.TryPlaceThing(ThingMaker.MakeThing(ThingDef.Named("WoodLog"), null), position, map, ThingPlaceMode.Near, null);
			}
			if (Chance2 < 1) {
				GenPlace.TryPlaceThing(ThingMaker.MakeThing(ThingDef.Named("WoodLog"), null), position, map, ThingPlaceMode.Near, null);
			}
			this.Destroy(DestroyMode.Vanish);
			if (Controller.Settings.autoRecompost.Equals(true)) {
				GenConstruct.PlaceBlueprintForBuild(ThingDef.Named("CompostBin"), position, map, Rot4.North, Faction.OfPlayer, null);
			}
		}
		public override string GetInspectString() {
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(base.GetInspectString());
			if (stringBuilder.Length != 0) {
				stringBuilder.AppendLine();
			}
			stringBuilder.AppendLine("RFF.Progress".Translate() + " " + ((float)this.fermentTicks / (float)this.TargetTicks * 100f).ToString("#0.00") + "%");
			return stringBuilder.ToString().TrimEndNewlines();
		}
	}

	public class CompCompostBin : ThingComp {
		public CompProperties_CompostBin Props {
			get {
				return (CompProperties_CompostBin)this.props;
			}
		}
	}

	public class CompProperties_CompostBin : CompProperties {
		public string product;
		public int fermentTicks;
		public CompProperties_CompostBin() {
			this.compClass = typeof(CompCompostBin);
		}
	}

	// ==================================== //
	// ========== Compost Barrel ========== //
	// ==================================== //

	[StaticConstructorOnStartup]
	public class Building_CompostBarrel : Building {
		private int compostcount;
		private float progressInt;
		private Material barFilledCachedMat;
		private static readonly Vector2 BarSize = new Vector2(0.55f, 0.1f);
		private static readonly Color BarZeroProgressColor = new Color(0.4f, 0.27f, 0.22f);
		private static readonly Color BarFermentedColor = new Color(0.9f, 0.85f, 0.2f);
		private static readonly Material BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.3f, 0.3f));
		public CompPowerTrader compPowerTrader;
		public override void SpawnSetup(Map map, bool respawningAfterLoad) {
			base.SpawnSetup(map, respawningAfterLoad);
			this.compPowerTrader = base.GetComp<CompPowerTrader>();
		}
		public float Progress {
			get {
				return this.progressInt;
			}
			set {
				if (value == this.progressInt) {
					return;
				}
				this.progressInt = value;
				this.barFilledCachedMat = null;
			}
		}
		private Material BarFilledMat {
			get {
				if (this.barFilledCachedMat == null) {
					this.barFilledCachedMat = SolidColorMaterials.SimpleSolidColorMaterial(Color.Lerp(Building_CompostBarrel.BarZeroProgressColor, Building_CompostBarrel.BarFermentedColor, this.Progress));
				}
				return this.barFilledCachedMat;
			}
		}
		private float Temperature {
			get {
				if (base.MapHeld == null) {
					Log.ErrorOnce("RFF.NullMapHeld".Translate(), 847163513);
					return 7f;
				}
				return base.PositionHeld.GetTemperature(base.MapHeld);
			}
		}
		public int SpaceLeftForCompost {
			get {
				if (this.Distilled) {
					return 0;
				}
				return 30 - this.compostcount;
			}
		}
		private bool Empty {
			get {
				return this.compostcount <= 0;
			}
		}
		public bool Distilled {
			get {
				return !this.Empty && this.Progress >= 1f;
			}
		}
		private float CurrentTempProgressSpeedFactor {
			get {
				CompProperties_TemperatureRuinable compProperties = this.def.GetCompProperties<CompProperties_TemperatureRuinable>();
				float temperature = this.Temperature;
				if (temperature < compProperties.minSafeTemperature) {
					return 0.1f;
				}
				if (temperature < 7f) {
					return GenMath.LerpDouble(compProperties.minSafeTemperature, 7f, 0.1f, 1f, temperature);
				}
				return 1f;
			}
		}
		private float ProgressPerTickAtCurrentTemp {
			get {
				float progPerTick = 5.55555555E-06f * this.CurrentTempProgressSpeedFactor;
				if (!this.compPowerTrader.PowerOn) {
					progPerTick = progPerTick / 2;
				}
				return progPerTick;
			}
		}
		private int EstimatedTicksLeft {
			get {
				return Mathf.Max(Mathf.RoundToInt((1f - this.Progress) / this.ProgressPerTickAtCurrentTemp), 0);
			}
		}
		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look<int>(ref this.compostcount, "compostcount", 0, false);
			Scribe_Values.Look<float>(ref this.progressInt, "progress", 0f, false);
		}
		public override void TickRare() {
			base.TickRare();
			if (!this.Empty) {
				this.Progress = Mathf.Min(this.Progress + 250f * this.ProgressPerTickAtCurrentTemp, 1f);
			}
		}
		public void Addoil(int count) {
			if (this.Distilled) {
				Log.Warning("RFF.FullOfFert".Translate());
				return;
			}
			int num = Mathf.Min(count, 30 - this.compostcount);
			if (num <= 0) {
				return;
			}
			this.Progress = GenMath.WeightedAverage(0f, (float)num, this.Progress, (float)this.compostcount);
			this.compostcount += num;
			base.GetComp<CompTemperatureRuinable>().Reset();
		}
		protected override void ReceiveCompSignal(string signal) {
			if (signal == "RuinedByTemperature") {
				this.Reset();
			}
		}
		private void Reset() {
			this.compostcount = 0;
			this.Progress = 0f;
		}
		public void Addoil(Thing RawCompost) {
			CompTemperatureRuinable comp = base.GetComp<CompTemperatureRuinable>();
			if (comp.Ruined) {
				comp.Reset();
			}
			this.Addoil(RawCompost.stackCount);
			RawCompost.Destroy(DestroyMode.Vanish);
		} 
		public override string GetInspectString() {
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(base.GetInspectString());
			if (stringBuilder.Length != 0) {
				stringBuilder.AppendLine();
			}
			CompTemperatureRuinable comp = base.GetComp<CompTemperatureRuinable>();
			if (!this.Empty && !comp.Ruined) {
				if (this.Distilled) {
					stringBuilder.AppendLine("RFF.ContainsFert".Translate() + " " + (int)this.compostcount + "/" + "30");
				}
				else {
					stringBuilder.AppendLine("RFF.ContainsComp".Translate() + " " + (int)this.compostcount + "/" + "30");
				}
			}
			if (!this.Empty) {
				if (this.Distilled) {
					stringBuilder.AppendLine("RFF.Composted".Translate());
				}
				else {
					stringBuilder.AppendLine("RFF.CompProg".Translate() + " " + this.Progress.ToStringPercent() + " ~ " + this.EstimatedTicksLeft.ToStringTicksToPeriod());
					if (this.CurrentTempProgressSpeedFactor != 1f) {
						stringBuilder.AppendLine("RFF.OutOfTemp".Translate() + " " + this.CurrentTempProgressSpeedFactor.ToStringPercent());
					}
				}
			}
			if (base.MapHeld != null) {
				stringBuilder.AppendLine(string.Concat(new string[] {
					"RFF.Temp".Translate() + " " + this.Temperature.ToStringTemperature("F0"),
					" (" + "RFF.IdealTemp".Translate() + " ",
					7f.ToStringTemperature("F0"),
					" ~ ",
					comp.Props.maxSafeTemperature.ToStringTemperature("F0") + ")"
				}));
			}
			return stringBuilder.ToString().TrimEndNewlines();
		}
		public Thing TakeOutFertilizer() {
			if (!this.Distilled) {
				Log.Warning("RFF.NotReady".Translate());
				return null;
			}
			Thing thing = ThingMaker.MakeThing(ThingDefOf.Fertilizer, null);
			thing.stackCount = this.compostcount;
			this.Reset();
			return thing;
		}
		public override void Draw() {
			base.Draw();
			if (!this.Empty) {
				Vector3 drawPos = this.DrawPos;
				drawPos.y += 0.05f;
				drawPos.z += 0.25f;
				GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest {
					center = drawPos,
					size = Building_CompostBarrel.BarSize,
					fillPercent = (float)this.compostcount / 30f,
					filledMat = this.BarFilledMat,
					unfilledMat = Building_CompostBarrel.BarUnfilledMat,
					margin = 0.1f,
					rotation = Rot4.North
				});
			}
		}
		[DebuggerHidden]
		public override IEnumerable<Gizmo> GetGizmos() {
			IEnumerator<Gizmo> enumerator = base.GetGizmos().GetEnumerator();
			while (enumerator.MoveNext()) {
				Gizmo current = enumerator.Current;
				yield return current;
			}
			if (Prefs.DevMode && !this.Empty) {
				yield return new Command_Action {
					defaultLabel = "Debug: Set progress to 1",
					action = delegate {
						this.Progress = 1f;
					}
				};
			}
			yield break;
		}
	}
	
	public class WorkGiver_FillCompostBarrel : WorkGiver_Scanner {
		private static string TemperatureTrans;
		private static string NoCompostTrans;
		public override ThingRequest PotentialWorkThingRequest {
			get {
				return ThingRequest.ForDef(ThingDefOf.CompostBarrel);
			}
		}
		public override PathEndMode PathEndMode {
			get {
				return PathEndMode.Touch;
			}
		}
		public static void Reset() {
			WorkGiver_FillCompostBarrel.TemperatureTrans = "RFF.BadTemp".Translate();
			WorkGiver_FillCompostBarrel.NoCompostTrans = "RFF.NoComp".Translate();
		}
		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) {
			Building_CompostBarrel Building_RFF_CompostBarrel = t as Building_CompostBarrel;
			if (Building_RFF_CompostBarrel == null || Building_RFF_CompostBarrel.Distilled || Building_RFF_CompostBarrel.SpaceLeftForCompost <= 0) {
				return false;
			}
			float temperature = Building_RFF_CompostBarrel.Position.GetTemperature(Building_RFF_CompostBarrel.Map);
			CompProperties_TemperatureRuinable compProperties = Building_RFF_CompostBarrel.def.GetCompProperties<CompProperties_TemperatureRuinable>();
			if (temperature < compProperties.minSafeTemperature + 2f || temperature > compProperties.maxSafeTemperature - 2f) {
				JobFailReason.Is(WorkGiver_FillCompostBarrel.TemperatureTrans);
				return false;
			}
			if (t.IsForbidden(pawn) || !pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1)) {
				return false;
			}
			if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null) {
				return false;
			}
			if (this.Findmech_oil(pawn, Building_RFF_CompostBarrel) == null) {
				JobFailReason.Is(WorkGiver_FillCompostBarrel.NoCompostTrans);
				return false;
			}
			return !t.IsBurning();
		}
		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) {
			Building_CompostBarrel Building_RFF_CompostBarrel = (Building_CompostBarrel)t;
			Thing t2 = this.Findmech_oil(pawn, Building_RFF_CompostBarrel);
			return new Job(JobDefOf.FillCompostBarrel, t, t2) {
				count = Building_RFF_CompostBarrel.SpaceLeftForCompost
			};
		}
		private Thing Findmech_oil(Pawn pawn, Building_CompostBarrel barrel) {
			Predicate<Thing> predicate = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1);
			Predicate<Thing> validator = predicate;
			return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(ThingDefOf.RawCompost), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false), 9999f, validator, null, 0, -1, false);
		}
	}
	
	public class WorkGiver_EmptyCompostBarrel : WorkGiver_Scanner {
		public override ThingRequest PotentialWorkThingRequest {
			get {
				return ThingRequest.ForDef(ThingDefOf.CompostBarrel);
			}
		}
		public override PathEndMode PathEndMode {
			get {
				return PathEndMode.Touch;
			}
		}
		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) {
			Building_CompostBarrel Building_RFF_CompostBarrel = t as Building_CompostBarrel;
			return Building_RFF_CompostBarrel != null && Building_RFF_CompostBarrel.Distilled && !t.IsBurning() && !t.IsForbidden(pawn) && pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1);
		}
		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) {
			return new Job(JobDefOf.EmptyCompostBarrel, t);
		}
	}
	
	public class JobDriver_FillCompostBarrel : JobDriver {
		protected Building_CompostBarrel Barrel {
			get {
				return (Building_CompostBarrel)this.job.GetTarget(TargetIndex.A).Thing;
			}
		}
		protected Thing RawCompost {
			get {
				return this.job.GetTarget(TargetIndex.B).Thing;
			}
		}
		public override bool TryMakePreToilReservations(bool errorOnFailed) {
			return this.pawn.Reserve(this.Barrel, this.job, 1, -1, null, errorOnFailed) && this.pawn.Reserve(this.RawCompost, this.job, 1, -1, null, errorOnFailed);
		}
		[DebuggerHidden]
		protected override IEnumerable<Toil> MakeNewToils() {
			this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			this.FailOnBurningImmobile(TargetIndex.A);
			base.AddEndCondition(() => (this.Barrel.SpaceLeftForCompost > 0) ? JobCondition.Ongoing : JobCondition.Succeeded);
			yield return Toils_General.DoAtomic(delegate {
				this.job.count = this.Barrel.SpaceLeftForCompost;
			});
			Toil reserveWort = Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
			yield return reserveWort;
			yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
			yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, false).FailOnDestroyedNullOrForbidden(TargetIndex.B);
			yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveWort, TargetIndex.B, TargetIndex.None, true, null);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			yield return Toils_General.Wait(200).FailOnDestroyedNullOrForbidden(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
			yield return new Toil {
				initAction = delegate {
					this.Barrel.Addoil(this.RawCompost);
				},
				defaultCompleteMode = ToilCompleteMode.Instant
			};
		}
	}
	
	public class JobDriver_EmptyCompostBarrel : JobDriver {
		protected Building_CompostBarrel Barrel {
			get {
				return (Building_CompostBarrel)this.job.GetTarget(TargetIndex.A).Thing;
			}
		}
		protected Thing Fertilizer {
			get {
				return this.job.GetTarget(TargetIndex.B).Thing;
			}
		}
		public override bool TryMakePreToilReservations(bool errorOnFailed) {
			return this.pawn.Reserve(this.Barrel, this.job, 1, -1, null, errorOnFailed);
		}
		[DebuggerHidden]
		protected override IEnumerable<Toil> MakeNewToils() {
			this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			this.FailOnBurningImmobile(TargetIndex.A);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			yield return Toils_General.Wait(200).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch).FailOn(() => !this.Barrel.Distilled).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
			yield return new Toil {
				initAction = delegate {
					Thing thing = this.Barrel.TakeOutFertilizer();
					GenPlace.TryPlaceThing(thing, this.pawn.Position, this.Map, ThingPlaceMode.Near, null);
					StoragePriority currentPriority = StoreUtility.CurrentStoragePriorityOf(thing);
					IntVec3 c;
					if (StoreUtility.TryFindBestBetterStoreCellFor(thing, this.pawn, this.Map, currentPriority, this.pawn.Faction, out c, true)) {
						this.job.SetTarget(TargetIndex.C, c);
						this.job.SetTarget(TargetIndex.B, thing);
						this.job.count = thing.stackCount;
					}
					else {
						this.EndJobWith(JobCondition.Incompletable);
					}
				},
				defaultCompleteMode = ToilCompleteMode.Instant
			};
			yield return Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
			yield return Toils_Reserve.Reserve(TargetIndex.C, 1, -1, null);
			yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
			yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false, false);
			Toil carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.C);
			yield return carryToCell;
			yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, carryToCell, true);
		}
	}

}
