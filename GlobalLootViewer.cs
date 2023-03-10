using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.UI;
using OnConditions = On.Terraria.GameContent.ItemDropRules.Conditions;

namespace GlobalLootViewer {
	public class GlobalLootViewer : Mod {
		public HashSet<Type> IgnoreWhenHighlighting { get; private set; }
		public GlobalLootViewer() {
			IgnoreWhenHighlighting = new();
		}
		public override void Load() {
			On.Terraria.GameContent.Bestiary.BestiaryDatabase.ExtractDropsForNPC += BestiaryDatabase_ExtractDropsForNPC;
			On.Terraria.GameContent.Bestiary.ItemDropBestiaryInfoElement.ProvideUIElement += ItemDropBestiaryInfoElement_ProvideUIElement;
			On.Terraria.GameContent.Bestiary.NPCPortraitInfoElement.ProvideUIElement += NPCPortraitInfoElement_ProvideUIElement;
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
			IgnoreWhenHighlighting.Add(typeof(Conditions.MissingTwin));
			IgnoreWhenHighlighting.Add(typeof(Conditions.EmpressOfLightIsGenuinelyEnraged));
			IgnoreWhenHighlighting.Add(typeof(Conditions.NamedNPC));
			IgnoreWhenHighlighting.Add(typeof(Conditions.NotFromStatue));
			IgnoreWhenHighlighting.Add(typeof(Conditions.NotExpert));
			IgnoreWhenHighlighting.Add(typeof(Conditions.IsCorruption));
			IgnoreWhenHighlighting.Add(typeof(Conditions.IsCrimson));
			if (ModLoader.TryGetMod("AltLibrary", out Mod altLib)) {
				if (altLib.Code.GetType("CorroCrimDropCondition") is Type corroCrim) IgnoreWhenHighlighting.Add(corroCrim);
				if (altLib.Code.GetType("EvilAltDropCondition") is Type evilAlt) IgnoreWhenHighlighting.Add(evilAlt);
				if (altLib.Code.GetType("HallowAltDropCondition") is Type hallowAlt) IgnoreWhenHighlighting.Add(hallowAlt);
				if (altLib.Code.GetType("HallowDropCondition") is Type hallow) IgnoreWhenHighlighting.Add(hallow);
			}
		}
		public override void Unload() {
			On.Terraria.GameContent.Bestiary.BestiaryDatabase.ExtractDropsForNPC -= BestiaryDatabase_ExtractDropsForNPC;
			OnConditions.MechanicalBossesDummyCondition.GetConditionDescription -= MechanicalBossesDummyCondition_GetConditionDescription;
		}
		private static void BestiaryDatabase_ExtractDropsForNPC(On.Terraria.GameContent.Bestiary.BestiaryDatabase.orig_ExtractDropsForNPC orig, BestiaryDatabase self, ItemDropDatabase dropsDatabase, int npcId) {
			if (npcId != GlobalLootViewerNPC.ID && npcId != HiddenLootViewerNPC.ID) {
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
		private static string MechanicalBossesDummyCondition_GetConditionDescription(OnConditions.MechanicalBossesDummyCondition.orig_GetConditionDescription orig, Conditions.MechanicalBossesDummyCondition self) {
			return Language.GetTextValue("Bestiary_ItemDropConditions.Hardmode");
		}
		private void UIBestiaryInfoItemLine_ctor(On.Terraria.GameContent.UI.Elements.UIBestiaryInfoItemLine.orig_ctor orig, Terraria.GameContent.UI.Elements.UIBestiaryInfoItemLine self, DropRateInfo info, BestiaryUICollectionInfo uiinfo, float textScale) {
			orig(self, info, uiinfo, textScale);
			if ((uiinfo.OwnerEntry?.Info?.Count ?? 0) > 0 && uiinfo.OwnerEntry.Info[0] is NPCNetIdBestiaryInfoElement infoElement0) {
				if (infoElement0.NetId == GlobalLootViewerNPC.ID || infoElement0.NetId == HiddenLootViewerNPC.ID) {
					self.OnRightClick += (ev, el) => {
						if (LootViewerConfig.HiddenEntries.Contains(info.itemId)) {
							LootViewerConfig.HiddenEntries.Remove(info.itemId);
						} else {
							LootViewerConfig.HiddenEntries.Add(info.itemId);
						}
						LootViewerConfig.Instance.Save();
						if (el.Parent is not null) {
							Terraria.UI.UIElement parent = el.Parent;
							float diff = el.Height.Pixels + 4 + el.MarginBottom;
							int selfIndex = 0;
							int index = 0;
							foreach (var sibling in parent.Children) {
								if (sibling == el) {
									selfIndex = index;
								}
								index++;
							}
							parent.RemoveChild(el);
							index = 0;
							foreach (var sibling in parent.Children) {
								if (index >= selfIndex) {
									//sibling.Top.Pixels -= diff;
									sibling.MarginTop -= diff;
									sibling.Recalculate();
									break;
								}
								index++;
							}
							parent.Recalculate();
							parent.Parent.Recalculate();
							parent.Parent.Parent.Recalculate();
						}
					};
				}
			}
			if (!LootViewerConfig.HighlightConditional) return;
			if ((info.conditions?.Count ?? 0) > 0) {
				NPC npc = new();
				if ((uiinfo.OwnerEntry?.Info?.Count ?? 0) > 0 && uiinfo.OwnerEntry.Info[0] is NPCNetIdBestiaryInfoElement infoElement) {
					npc.SetDefaults(infoElement.NetId);
				}
				npc.position = Main.LocalPlayer.position;
				npc.target = Main.myPlayer;
				DropAttemptInfo dropInfo = default(DropAttemptInfo);
				dropInfo.player = Main.LocalPlayer;
				dropInfo.npc = npc;
				dropInfo.IsExpertMode = Main.expertMode;
				dropInfo.IsMasterMode = Main.masterMode;
				dropInfo.IsInSimulation = false;
				dropInfo.rng = Main.rand;
				bool? canDrop = null;
				try {
					for (int i = 0; i < info.conditions.Count; i++) {
						if (IgnoreWhenHighlighting.Contains(info.conditions[i].GetType())) continue;
						if (!info.conditions[i].CanDrop(dropInfo)) {
							canDrop = false;
							break;
						} else {
							canDrop = true;
						}
					}
					switch (canDrop) {
						case true:
						self.BackgroundColor = new Color(194, 175, 10);
						break;

						case false:
						self.BackgroundColor = new Color(97, 5, 5, 255);
						break;
					}
				} catch (Exception) {
					self.BackgroundColor = new Color(193, 10, 194);
				}
			}
		}
		static FieldInfo _droprateInfo;
		static DropRateInfo GetDropRateInfo(ItemDropBestiaryInfoElement self) {
			_droprateInfo ??= typeof(ItemDropBestiaryInfoElement).GetField("_droprateInfo", BindingFlags.NonPublic | BindingFlags.Instance);
			return (DropRateInfo)_droprateInfo.GetValue(self);
		}
		private static UIElement ItemDropBestiaryInfoElement_ProvideUIElement(On.Terraria.GameContent.Bestiary.ItemDropBestiaryInfoElement.orig_ProvideUIElement orig, ItemDropBestiaryInfoElement self, BestiaryUICollectionInfo info) {
			if ((info.OwnerEntry?.Info?.Count ?? 0) > 0 && info.OwnerEntry.Info[0] is NPCNetIdBestiaryInfoElement infoElement) {
				if (infoElement.NetId == GlobalLootViewerNPC.ID) {
					if (LootViewerConfig.HiddenEntries.Contains(GetDropRateInfo(self).itemId)) {
						return null;
					}
				} else if (infoElement.NetId == HiddenLootViewerNPC.ID) {
					if (!LootViewerConfig.HiddenEntries.Contains(GetDropRateInfo(self).itemId)) {
						return null;
					}
				}
			}
			return orig(self, info);
		}
		private UIElement NPCPortraitInfoElement_ProvideUIElement(On.Terraria.GameContent.Bestiary.NPCPortraitInfoElement.orig_ProvideUIElement orig, NPCPortraitInfoElement self, BestiaryUICollectionInfo info) {
			UIElement element = orig(self, info);
			if (info.UnlockState > BestiaryEntryUnlockState.NotKnownAtAll_0 && LootViewerConfig.KillCounter) {
				if ((info.OwnerEntry?.Info?.Count ?? 0) > 0 && info.OwnerEntry.Info[0] is NPCNetIdBestiaryInfoElement infoElement) {
					int kills = Main.BestiaryTracker.Kills.GetKillCount(ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[infoElement.NetId]);
					if (kills > 0) {
						UIElement element2 = CreateKillsContainer(kills);
						element.Append(element2);
					}
				}
			}
			return element;
		}

		private static UIElement CreateKillsContainer(int kills) {
			float panelHeight = 16f;
			float tombstoneScale = (panelHeight / 30);
			UIElement backPanel = new UIPanel(null, null, 5, 21) {
				Width = new StyleDimension(26, 0f),
				Height = new StyleDimension(panelHeight, 0f),
				BackgroundColor = Color.Transparent,
				BorderColor = Color.Transparent,
				Left = new StyleDimension(-16, 0f),
				Top = new StyleDimension(6f, 0f),
				VAlign = 0f,
				HAlign = 1f
			};
			backPanel.SetPadding(0f);
			backPanel.Append(new UIImage(Main.Assets.Request<Texture2D>("Images/Item_321")) {
				Left = new StyleDimension(0, 0f),
				Width = new StyleDimension(tombstoneScale * 26, 0f),
				Height = new StyleDimension(0, 1f),
				VAlign = 0.5f,
				HAlign = 1f,
				ScaleToFit = true
			});
			backPanel.Append(new UIText(kills.ToString()) {
				Left = new StyleDimension(-26 * tombstoneScale - 4, 0f),
				VAlign = 0.5f,
				HAlign = 1f
			});
			return backPanel;
		}
	}
	public class GlobalLootViewerNPC : ModNPC {
		public override string Texture => "GlobalLootViewer/icon_small";
		public static int ID { get; private set; }
		public override void SetStaticDefaults() {
			DisplayName.SetDefault("Global Loot Viewer");
			ID = Type;
		}
		public override void SetDefaults() {
			NPC.width = NPC.height = 30;
			NPC.lifeMax = 250;
			NPC.value = 250;
		}
		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
			bestiaryEntry.UIInfoProvider = new UnlockedEnemyUICollectionInfoProvider();
		}
	}
	public class HiddenLootViewerNPC : ModNPC {
		public override string Texture => "GlobalLootViewer/icon_small";
		public static int ID { get; private set; }
		public override void SetStaticDefaults() {
			DisplayName.SetDefault("Global Loot Viewer (Hidden Loot)");
			ID = Type;
		}
		public override void SetDefaults() {
			NPC.width = NPC.height = 30;
			NPC.lifeMax = 250;
			NPC.value = 250;
		}
		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
			bestiaryEntry.UIInfoProvider = new HiddenEnemyUICollectionInfoProvider();
		}
	}
	public class UnlockedEnemyUICollectionInfoProvider : IBestiaryUICollectionInfoProvider {
		public BestiaryUICollectionInfo GetEntryUICollectionInfo() {
			BestiaryUICollectionInfo result = default(BestiaryUICollectionInfo);
			result.UnlockState = BestiaryEntryUnlockState.CanShowDropsWithDropRates_4;
			return result;
		}
	}
	public class HiddenEnemyUICollectionInfoProvider : IBestiaryUICollectionInfoProvider {
		public BestiaryUICollectionInfo GetEntryUICollectionInfo() {
			BestiaryUICollectionInfo result = default(BestiaryUICollectionInfo);
			result.UnlockState = LootViewerConfig.HiddenEntries.Count > 0 ? BestiaryEntryUnlockState.CanShowDropsWithDropRates_4 : BestiaryEntryUnlockState.NotKnownAtAll_0;
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
		[Label("Show kill count")]
		[DefaultValue(true)]
		public bool killCounter = true;
		[Label("Hidden entries")]
		[Tooltip("A list of item types which have been hidden from the global loot viewer (right click to hide/unhide)\n"
			+ "unfortunately due to how overly complicated changing a mod config is this can only be edited in the bestiary or the config file")]
		public List<string> HiddenEntriesStrings {
			get {
				hiddenEntries ??= new();
				List<string> strings = new(hiddenEntries.Count);
				for (int i = 0; i < hiddenEntries.Count; i++) {
					if (hiddenEntries[i] < ItemID.Count) {
						strings.Add($"Terraria:{ItemID.Search.GetName(hiddenEntries[i])}");
					} else {
						ModItem it = ItemLoader.GetItem(hiddenEntries[i]);
						strings.Add($"{it.Mod.Name}:{it.Name}");
					}
				}
				return strings;
			}
			set {
				hiddenEntries = new(value.Count);
				for (int i = 0; i < value.Count; i++) {
					string[] s = value[i].Split(':');
					if (s[0] == "Terraria") {
						hiddenEntries.Add(ItemID.Search.GetId(s[1]));
					} else {
						if(ModContent.TryFind(s[0], s[1], out ModItem it)) {
							hiddenEntries.Add(it.Type);
						}
					}
				}
			}
		}

		[JsonIgnore]
		public List<int> hiddenEntries;
		[JsonIgnore]
		public static bool HideInactive => Instance.hideInactive;
		[JsonIgnore]
		public static bool HighlightConditional => Instance.highlightConditional;
		[JsonIgnore]
		public static bool KillCounter => Instance.killCounter;
		[JsonIgnore]
		public static List<int> HiddenEntries => Instance.hiddenEntries ??= new();
		internal void Save() {
			Directory.CreateDirectory(ConfigManager.ModConfigPath);
			string filename = Mod.Name + "_" + Name + ".json";
			string path = Path.Combine(ConfigManager.ModConfigPath, filename);
			string json = JsonConvert.SerializeObject(this, ConfigManager.serializerSettings);
			File.WriteAllText(path, json);
		}
	}
}