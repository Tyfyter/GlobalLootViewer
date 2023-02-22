using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using OnConditions = On.Terraria.GameContent.ItemDropRules.Conditions;

namespace GlobalLootViewer {
	public class GlobalLootViewer : Mod {
		public override void Load() {
			On.Terraria.GameContent.Bestiary.BestiaryDatabase.ExtractDropsForNPC += BestiaryDatabase_ExtractDropsForNPC;
			OnConditions.MechanicalBossesDummyCondition.GetConditionDescription += MechanicalBossesDummyCondition_GetConditionDescription;
			OnConditions.MechanicalBossesDummyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;
			OnConditions.SoulOfNight.CanShowItemDropInUI += (_, _) => Main.hardMode;
			OnConditions.SoulOfLight.CanShowItemDropInUI += (_, _) => Main.hardMode;

			OnConditions.KOCannon.CanShowItemDropInUI += (_, _) => Main.hardMode;
			OnConditions.LivingFlames.CanShowItemDropInUI += (_, _) => Main.hardMode;
			OnConditions.PirateMap.CanShowItemDropInUI += (_, _) => Main.hardMode;

			OnConditions.CorruptKeyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;
			OnConditions.CrimsonKeyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;
			OnConditions.DesertKeyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;
			OnConditions.FrozenKeyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;
			OnConditions.HallowKeyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;
			OnConditions.JungleKeyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;

			OnConditions.YoyoCascade.CanShowItemDropInUI += (_, _) => NPC.downedBoss3 && !(LootViewerConfig.HideInactive && Main.hardMode);
			OnConditions.YoyosAmarok.CanShowItemDropInUI += (_, _) => Main.hardMode;
			OnConditions.YoyosYelets.CanShowItemDropInUI += (_, _) => NPC.downedMechBossAny;
			OnConditions.YoyosKraken.CanShowItemDropInUI += (_, _) => NPC.downedPlantBoss;
			OnConditions.YoyosHelFire.CanShowItemDropInUI += (_, _) => Main.hardMode;

			OnConditions.HalloweenGoodieBagDrop.CanShowItemDropInUI += (_, _) => !LootViewerConfig.HideInactive || Main.halloween;
			OnConditions.HalloweenWeapons.CanShowItemDropInUI += (_, _) => !LootViewerConfig.HideInactive || Main.halloween;
			OnConditions.IsChristmas.CanShowItemDropInUI += (_, _) => !LootViewerConfig.HideInactive || Main.xMas;
			OnConditions.XmasPresentDrop.CanShowItemDropInUI += (_, _) => !LootViewerConfig.HideInactive || Main.xMas;
			On.Terraria.GameContent.UI.Elements.UIBestiaryInfoItemLine.ctor += UIBestiaryInfoItemLine_ctor;
		}
		public override void Unload() {
			On.Terraria.GameContent.Bestiary.BestiaryDatabase.ExtractDropsForNPC -= BestiaryDatabase_ExtractDropsForNPC;
			OnConditions.MechanicalBossesDummyCondition.GetConditionDescription -= MechanicalBossesDummyCondition_GetConditionDescription;
		}
		private void BestiaryDatabase_ExtractDropsForNPC(On.Terraria.GameContent.Bestiary.BestiaryDatabase.orig_ExtractDropsForNPC orig, BestiaryDatabase self, ItemDropDatabase dropsDatabase, int npcId) {
			if (npcId != GlobalLootViewerNPC.ID) {
				orig(self, dropsDatabase, npcId);
			} else {
				BestiaryEntry bestiaryEntry = self.FindEntryByNPCID(npcId);
				if (bestiaryEntry == null) {
					return;
				}
				List<IItemDropRule> rulesForNPCID = new GlobalLoot(dropsDatabase).Get();
				List<DropRateInfo> ruleList = new List<DropRateInfo>();
				DropRateInfoChainFeed ratesInfo = new DropRateInfoChainFeed(1f);
				foreach (IItemDropRule rule in rulesForNPCID) {
					rule.ReportDroprates(ruleList, ratesInfo);
				}
				bestiaryEntry.Info.AddRange(ruleList.Select(info => new ItemDropBestiaryInfoElement(info)));
			}
		}
		private string MechanicalBossesDummyCondition_GetConditionDescription(OnConditions.MechanicalBossesDummyCondition.orig_GetConditionDescription orig, Conditions.MechanicalBossesDummyCondition self) {
			return Language.GetTextValue("Bestiary_ItemDropConditions.Hardmode");
		}
		private void UIBestiaryInfoItemLine_ctor(On.Terraria.GameContent.UI.Elements.UIBestiaryInfoItemLine.orig_ctor orig, Terraria.GameContent.UI.Elements.UIBestiaryInfoItemLine self, DropRateInfo info, BestiaryUICollectionInfo uiinfo, float textScale) {
			orig(self, info, uiinfo, textScale);
			if (!LootViewerConfig.HighlightConditional) return;
			if ((info.conditions?.Count ?? 0) > 0) {
				NPC npc = new();
				if ((uiinfo.OwnerEntry?.Info?.Count ?? 0) > 0 && uiinfo.OwnerEntry.Info[0] is NPCNetIdBestiaryInfoElement infoElement) {
					npc.SetDefaults(infoElement.NetId);
				}
				DropAttemptInfo dropInfo = default(DropAttemptInfo);
				dropInfo.player = Main.LocalPlayer;
				dropInfo.npc = npc;
				dropInfo.IsExpertMode = Main.expertMode;
				dropInfo.IsMasterMode = Main.masterMode;
				dropInfo.IsInSimulation = false;
				dropInfo.rng = Main.rand;
				bool canDrop = true;
				try {
					for (int i = 0; i < info.conditions.Count; i++) {
						if (!info.conditions[i].CanDrop(dropInfo)) {
							canDrop = false;
							break;
						}
					}
					if (canDrop) {
						self.BackgroundColor = new Color(194, 175, 10);
					} else {
						self.BackgroundColor = new Color(97, 5, 5, 255);
					}
				} catch (Exception) {
					self.BackgroundColor = new Color(193, 10, 194);
				}
			}
		}
	}
	public class GlobalLootViewerNPC : ModNPC {
		public static int ID { get; private set; }
		public override void SetStaticDefaults() {
			DisplayName.SetDefault("Global Loot Viewer NPC");
			ID = Type;
		}
		public override void SetDefaults() {
			NPC.width = NPC.height = 30;
			NPC.value = 250;
		}
		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
			bestiaryEntry.UIInfoProvider = new UnlockedEnemyUICollectionInfoProvider();
		}
	}
	public class UnlockedEnemyUICollectionInfoProvider : IBestiaryUICollectionInfoProvider {
		public BestiaryUICollectionInfo GetEntryUICollectionInfo() {
			BestiaryUICollectionInfo result = default(BestiaryUICollectionInfo);
			result.UnlockState = BestiaryEntryUnlockState.CanShowDropsWithDropRates_4;
			return result;
		}
	}
	[Label("Settings")]
	public class LootViewerConfig : ModConfig {
		public static LootViewerConfig Instance;
		public override ConfigScope Mode => ConfigScope.ClientSide;
		[Label("Hide inactive drops")]
		[DefaultValue(true)]
		public bool hideInactive = true;
		[Label("Highlight conditional drops")]
		[Tooltip("Changes the background color of conditional drops based on whether or not they can currently drop")]
		[DefaultValue(true)]
		public bool highlightConditional = true;
		[JsonIgnore]
		public static bool HideInactive => Instance.hideInactive;
		[JsonIgnore]
		public static bool HighlightConditional => Instance.highlightConditional;
	}
}