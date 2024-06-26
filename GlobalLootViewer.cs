using Humanizer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.UI;

namespace GlobalLootViewer {
	public class GlobalLootViewer : Mod {
		public HashSet<Type> IgnoreWhenHighlighting { get; private set; }
		public FastFieldInfo<CustomEntryIcon, Func<bool>> _unlockCondition;
		public MethodInfo unlockCondition;
		public FastFieldInfo<UIBestiaryInfoItemLine, Item> _infoDisplayItem;
		public FastFieldInfo<Filters.BySearch, string> _search;
		static FastFieldInfo<ItemDropBestiaryInfoElement, DropRateInfo> _droprateInfo;
		public GlobalLootViewer() {
			IgnoreWhenHighlighting = new();
		}
		public override void Load() {
			On_BestiaryDatabase.ExtractDropsForNPC += BestiaryDatabase_ExtractDropsForNPC;
			On_ItemDropBestiaryInfoElement.ProvideUIElement += ItemDropBestiaryInfoElement_ProvideUIElement;
			On_NPCKillCounterInfoElement.ProvideUIElement += (orig, self, info) => LootViewerConfig.AltKillCounter ? null : orig(self, info);
			On_NPCPortraitInfoElement.ProvideUIElement += NPCPortraitInfoElement_ProvideUIElement;
			On_Conditions.MechanicalBossesDummyCondition.GetConditionDescription += MechanicalBossesDummyCondition_GetConditionDescription;
			On_Conditions.MechanicalBossesDummyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;
			On_Conditions.SoulOfNight.CanShowItemDropInUI += (_, _) => Main.hardMode;
			On_Conditions.SoulOfLight.CanShowItemDropInUI += (_, _) => Main.hardMode;

			On_Conditions.LivingFlames.CanShowItemDropInUI += (_, _) => Main.hardMode;
			On_Conditions.PirateMap.CanShowItemDropInUI += (_, _) => Main.hardMode;

			On_Conditions.CorruptKeyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;
			On_Conditions.CrimsonKeyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;
			On_Conditions.DesertKeyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;
			On_Conditions.FrozenKeyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;
			On_Conditions.HallowKeyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;
			On_Conditions.JungleKeyCondition.CanShowItemDropInUI += (_, _) => Main.hardMode;

			On_Conditions.YoyoCascade.CanShowItemDropInUI += (_, _) => NPC.downedBoss3 && !(LootViewerConfig.HideInactive && Main.hardMode);
			On_Conditions.YoyosAmarok.CanShowItemDropInUI += (_, _) => Main.hardMode;
			On_Conditions.YoyosYelets.CanShowItemDropInUI += (_, _) => NPC.downedMechBossAny;
			On_Conditions.YoyosKraken.CanShowItemDropInUI += (_, _) => NPC.downedPlantBoss;
			On_Conditions.YoyosHelFire.CanShowItemDropInUI += (_, _) => Main.hardMode;

			On_Conditions.HalloweenGoodieBagDrop.CanShowItemDropInUI += (_, _) => !LootViewerConfig.HideInactive || Main.halloween;
			On_Conditions.HalloweenWeapons.CanShowItemDropInUI += (_, _) => !LootViewerConfig.HideInactive || Main.halloween;
			On_Conditions.IsChristmas.CanShowItemDropInUI += (_, _) => !LootViewerConfig.HideInactive || Main.xMas;
			On_Conditions.XmasPresentDrop.CanShowItemDropInUI += (_, _) => !LootViewerConfig.HideInactive || Main.xMas;
			On_UIBestiaryInfoItemLine.ctor += UIBestiaryInfoItemLine_ctor;
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
			On_UIBestiaryFilteringOptionsGrid.ctor += UIBestiaryFilteringOptionsGrid_ctor;
			On_CustomEntryIcon.UpdateUnlockState += CustomEntryIcon_UpdateUnlockState;
			On_BestiaryDatabaseNPCsPopulator.TryGivingEntryFlavorTextIfItIsMissing += BestiaryDatabaseNPCsPopulator_TryGivingEntryFlavorTextIfItIsMissing;
			_unlockCondition = new("_unlockCondition", BindingFlags.NonPublic | BindingFlags.Instance, true);
			unlockCondition = typeof(GlobalLootViewer).GetMethod("AlwaysUnlocked", BindingFlags.Public | BindingFlags.Static);
			_infoDisplayItem = new(nameof(_infoDisplayItem), BindingFlags.NonPublic | BindingFlags.Instance);
			_search = new(nameof(_search), BindingFlags.NonPublic | BindingFlags.Instance);
			/*
			On.Terraria.ID.ContentSamples.BestiaryHelper.GetSortedBestiaryEntriesList += BestiaryHelper_GetSortedBestiaryEntriesList;
			NPCID.Sets.NPCBestiaryDrawOffset[GlobalLootViewerNPC.ID] = new() {
				Hide = false,
				CustomTexturePath = "Terraria/Images/UI/Bestiary"
			};
			NPCID.Sets.NPCBestiaryDrawOffset[HiddenLootViewerNPC.ID] = new() {
				Hide = false,
				CustomTexturePath = "Terraria/Images/UI/Camera_1"
			};
			//*/
			On_ContentSamples.BestiaryHelper.ShouldHideBestiaryEntry += BestiaryHelper_ShouldHideBestiaryEntry;
			On_BestiaryDatabaseNPCsPopulator.GetExclusions += BestiaryDatabaseNPCsPopulator_GetExclusions;
			On_NPCID.Sets.GetLeinforsEntries += Sets_GetLeinforsEntries;
			On_Filters.BySearch.FitsFilter += this.BySearch_FitsFilter;
		}

		private bool BySearch_FitsFilter(On_Filters.BySearch.orig_FitsFilter orig, Filters.BySearch self, BestiaryEntry entry) {
			if (orig(self, entry)) return true;
			string search = _search.GetValue(self);
			if (search.Length < 1) return true;
			switch (search[0]) {
				case '$': {
					BestiaryUICollectionInfo info = entry.UIInfoProvider.GetEntryUICollectionInfo();
					for (int i = 0; i < entry.Info.Count; i++) {
						if (entry.Info[i] is ItemDropBestiaryInfoElement itemDropInfo) {
							if (itemDropInfo.ProvideUIElement(info) is null) break;
							int itemType = GetDropRateInfo(itemDropInfo).itemId;
							if (search == "$" + itemType) return true;
							List<IItemDropRule> rules = Main.ItemDropsDB.GetRulesForItemID(itemType);

							List<DropRateInfo> ruleList = [];
							DropRateInfoChainFeed ratesInfo = new(1f);
							for (int j = 0; j < rules.Count; j++) {
								rules[j].ReportDroprates(ruleList, ratesInfo);
							}
							for (int j = 0; j < ruleList.Count; j++) {
								if (search == "$" + ruleList[j].itemId) return true;
							}
						}
					}
					break;
				}
			}
			return false;
		}

		private Dictionary<int, NPCID.Sets.NPCBestiaryDrawModifiers> Sets_GetLeinforsEntries(On_NPCID.Sets.orig_GetLeinforsEntries orig) {
			var value = orig();
			value[GlobalLootViewerNPC.ID] = new() {
				Hide = false,
				CustomTexturePath = "Terraria/Images/UI/Bestiary"
			};
			value[HiddenLootViewerNPC.ID] = new() {
				Hide = false,
				CustomTexturePath = "Terraria/Images/UI/Camera_1"
			};
			return value;
		}
		private bool BestiaryHelper_ShouldHideBestiaryEntry(On_ContentSamples.BestiaryHelper.orig_ShouldHideBestiaryEntry orig, NPC npc) {
			if (npc.netID == GlobalLootViewerNPC.ID || npc.netID == HiddenLootViewerNPC.ID) return false;
			return orig(npc);
		}
		/*
		private List<KeyValuePair<int, NPC>> BestiaryHelper_GetSortedBestiaryEntriesList(Terraria.ID.On_ContentSamples.BestiaryHelper.orig_GetSortedBestiaryEntriesList orig, BestiaryDatabase database) {
			NPCID.Sets.NPCBestiaryDrawOffset[GlobalLootViewerNPC.ID] = new() {
				Hide = false,
				CustomTexturePath = "Terraria/Images/UI/Bestiary"
			};
			NPCID.Sets.NPCBestiaryDrawOffset[HiddenLootViewerNPC.ID] = new() {
				Hide = false,
				CustomTexturePath = "Terraria/Images/UI/Camera_1"
			};
			return orig(database);
		}*/
		private HashSet<int> BestiaryDatabaseNPCsPopulator_GetExclusions(On_BestiaryDatabaseNPCsPopulator.orig_GetExclusions orig) {
			HashSet<int> output = orig();
			output.Remove(GlobalLootViewerNPC.ID);
			output.Remove(HiddenLootViewerNPC.ID);
			return output;
		}

		private void BestiaryDatabaseNPCsPopulator_TryGivingEntryFlavorTextIfItIsMissing(On_BestiaryDatabaseNPCsPopulator.orig_TryGivingEntryFlavorTextIfItIsMissing orig, BestiaryDatabaseNPCsPopulator self, BestiaryEntry entry) {
			if (entry.UIInfoProvider is UnlockedEnemyUICollectionInfoProvider or HiddenEnemyUICollectionInfoProvider) return;
			orig(self, entry);
		}

		private void CustomEntryIcon_UpdateUnlockState(On_CustomEntryIcon.orig_UpdateUnlockState orig, CustomEntryIcon self, bool state) {
			if (_unlockCondition.GetValue(self).Method != unlockCondition) {
				orig(self, state);
			}
		}

		private void UIBestiaryFilteringOptionsGrid_ctor(On_UIBestiaryFilteringOptionsGrid.orig_ctor orig, UIBestiaryFilteringOptionsGrid self, EntryFilterer<BestiaryEntry, IBestiaryEntryFilter> filterer) {
			filterer.AddFilters(new List<IBestiaryEntryFilter>() { new GlobalLootFilter() });
			orig(self, filterer);
		}

		public override void Unload() {
			On_BestiaryDatabase.ExtractDropsForNPC -= BestiaryDatabase_ExtractDropsForNPC;
			On_Conditions.MechanicalBossesDummyCondition.GetConditionDescription -= MechanicalBossesDummyCondition_GetConditionDescription;
		}
		private static void BestiaryDatabase_ExtractDropsForNPC(On_BestiaryDatabase.orig_ExtractDropsForNPC orig, BestiaryDatabase self, ItemDropDatabase dropsDatabase, int npcId) {
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
		private static string MechanicalBossesDummyCondition_GetConditionDescription(On_Conditions.MechanicalBossesDummyCondition.orig_GetConditionDescription orig, Conditions.MechanicalBossesDummyCondition self) {
			return Language.GetTextValue("Bestiary_ItemDropConditions.Hardmode");
		}
		private void UIBestiaryInfoItemLine_ctor(On_UIBestiaryInfoItemLine.orig_ctor orig, UIBestiaryInfoItemLine self, DropRateInfo info, BestiaryUICollectionInfo uiinfo, float textScale) {
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
							UIElement parent = el.Parent;
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
			if (LootViewerConfig.HighlightConditional && (info.conditions?.Count ?? 0) > 0) {
				NPC npc = new();
				npc.SetDefaults(NPCID.SkeletonArcher);
				/*if ((uiinfo.OwnerEntry?.Info?.Count ?? 0) > 0 && uiinfo.OwnerEntry.Info[0] is NPCNetIdBestiaryInfoElement infoElement) {
					npc.SetDefaults(infoElement.NetId);
				}*/
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
						//if (info.conditions[i] is Conditions.IsExpert && ContentSamples.CreativeHelper.GetItemGroup(ContentSamples.ItemsByType[info.itemId], out _) == ContentSamples.CreativeHelper.ItemGroup.BossBags) continue;
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
			List<IItemDropRule> rules = Main.ItemDropsDB.GetRulesForItemID(_infoDisplayItem.GetValue(self).type);
			if (rules.Any()) {
				UIElement dropsElement = new();
				dropsElement.Top.Pixels = self.Height.Pixels;
				dropsElement.Left.Set(0, 0);
				dropsElement.Width.Set(0, 1);
				List<DropRateInfo> list = new();
				DropRateInfoChainFeed ratesInfo = new(1f);
				foreach (IItemDropRule item3 in rules) {
					item3.ReportDroprates(list, ratesInfo);
				}
				self.Height.Pixels += 2;
				float height = 0;
				foreach (DropRateInfo item2 in list) {
					UIElement el = new ItemDropBestiaryInfoElement(item2).ProvideUIElement(uiinfo);
					if (el is null) continue;
					self.Height.Pixels += el.Height.Pixels + 2;
					dropsElement.Height.Pixels += el.Height.Pixels + 2;
					el.Top.Pixels = height;
					el.Width.Set(0, 1);
					el.PaddingLeft = 0;
					el.PaddingRight = 0;
					dropsElement.Append(el);
					height += el.Height.Pixels + 2;
				}
				self.Append(dropsElement);
				dropsElement.Recalculate();
			}
		}
		static DropRateInfo GetDropRateInfo(ItemDropBestiaryInfoElement self) {
			_droprateInfo ??= new(nameof(_droprateInfo), BindingFlags.NonPublic | BindingFlags.Instance);
			return _droprateInfo.GetValue(self);
		}
		private static UIElement ItemDropBestiaryInfoElement_ProvideUIElement(On_ItemDropBestiaryInfoElement.orig_ProvideUIElement orig, ItemDropBestiaryInfoElement self, BestiaryUICollectionInfo info) {
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
		private UIElement NPCPortraitInfoElement_ProvideUIElement(On_NPCPortraitInfoElement.orig_ProvideUIElement orig, NPCPortraitInfoElement self, BestiaryUICollectionInfo info) {
			UIElement element = orig(self, info);
			if (info.UnlockState > BestiaryEntryUnlockState.NotKnownAtAll_0 && LootViewerConfig.AltKillCounter) {
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
		public static bool AlwaysUnlocked() => true;
	}
	public class GlobalLootViewerGlobalNPC : GlobalNPC {
		public override void SetBestiary(NPC npc, BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
			if (npc.type == GlobalLootViewerNPC.ID) {
				bestiaryEntry.Icon = new CustomEntryIcon("Mods.GlobalLootViewer.GlobalLootViewer", "Images/UI/Bestiary", GlobalLootViewer.AlwaysUnlocked);
				typeof(CustomEntryIcon).GetField("_sourceRectangle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bestiaryEntry.Icon, new Rectangle(32, 2, 30, 26));
				bestiaryEntry.UIInfoProvider = new UnlockedEnemyUICollectionInfoProvider();
				if (bestiaryEntry.Info[1] is NamePlateInfoElement) {
					bestiaryEntry.Info[1] = new NamePlateInfoElement("Mods.GlobalLootViewer.GlobalLoot", GlobalLootViewerNPC.ID);
				}
				bestiaryEntry.Info.RemoveAll(i => i is NPCPortraitInfoElement
					or NPCStatsReportInfoElement
					or FlavorTextBestiaryInfoElement
					or NPCKillCounterInfoElement
					or SpawnConditionBestiaryInfoElement
				);
				bestiaryEntry.AddTags(
					Mod.ModSourceBestiaryInfoElement
				);
			}else if (npc.type == HiddenLootViewerNPC.ID) {
				bestiaryEntry.Icon = new CustomEntryIcon("Mods.GlobalLootViewer.HiddenGlobalLootViewer", "Images/UI/Camera_1", GlobalLootViewer.AlwaysUnlocked);
				typeof(CustomEntryIcon).GetField("_sourceRectangle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bestiaryEntry.Icon, new Rectangle(0, 0, 32, 32));
				bestiaryEntry.UIInfoProvider = new UnlockedEnemyUICollectionInfoProvider();
				if (bestiaryEntry.Info[1] is NamePlateInfoElement) {
					bestiaryEntry.Info[1] = new NamePlateInfoElement("Mods.GlobalLootViewer.HiddenGlobalLoot", HiddenLootViewerNPC.ID);
				}
				bestiaryEntry.Info.RemoveAll(i => i is NPCPortraitInfoElement
					or NPCStatsReportInfoElement
					or FlavorTextBestiaryInfoElement
					or NPCKillCounterInfoElement
					or SpawnConditionBestiaryInfoElement
				); 
				bestiaryEntry.AddTags(
					Mod.ModSourceBestiaryInfoElement
				);
			}
		}
	}
	public class GlobalLootFilter : IBestiaryEntryFilter {
		readonly string text;
		readonly Asset<Texture2D> asset;
		public GlobalLootFilter() {
			text = ModContent.GetInstance<GlobalLootViewer>().DisplayName;
			asset = ModContent.GetInstance<GlobalLootViewer>().Assets.Request<Texture2D>("icon_small");
		}
		public bool? ForcedDisplay => true;
		public bool FitsFilter(BestiaryEntry entry) => entry.UIInfoProvider is UnlockedEnemyUICollectionInfoProvider or HiddenEnemyUICollectionInfoProvider;
		public string GetDisplayNameKey() => text;
		public UIElement GetImage() => new UIImage(asset) {
			HAlign = 0.5f,
			VAlign = 0.5f,
			ScaleToFit = true
		};
	}
	public static class GlobalLootViewerNPC {
		public static int ID => NPCID.PirateGhost;
	}
	public static class HiddenLootViewerNPC {
		public static int ID => NPCID.SlimeSpiked;
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
	public class LootViewerConfig : ModConfig {
		public static LootViewerConfig Instance;
		public override ConfigScope Mode => ConfigScope.ClientSide;
		[DefaultValue(true)]
		public bool hideInactive = true;
		[DefaultValue(true)]
		public bool highlightConditional = true;
		[DefaultValue(true)]
		public bool altKillCounter = true;
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
		public static bool AltKillCounter => Instance.altKillCounter;
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
	public class FastFieldInfo<TParent, T> {
		public readonly FieldInfo field;
		Func<TParent, T> getter;
		Action<TParent, T> setter;
		public FastFieldInfo(string name, BindingFlags bindingFlags, bool init = false) {
			field = typeof(TParent).GetField(name, bindingFlags);
			if (init) {
				getter = CreateGetter();
				setter = CreateSetter();
			}
		}
		public FastFieldInfo(FieldInfo field, bool init = false) {
			this.field = field;
			if (init) {
				getter = CreateGetter();
				setter = CreateSetter();
			}
		}
		public T GetValue(TParent parent) {
			return (getter ??= CreateGetter())(parent);
		}
		public void SetValue(TParent parent, T value) {
			(setter ??= CreateSetter())(parent, value);
		}
		private Func<TParent, T> CreateGetter() {
			if (field.FieldType != typeof(T)) throw new InvalidOperationException($"type of {field.Name} does not match provided type {typeof(T)}");
			string methodName = field.ReflectedType.FullName + ".get_" + field.Name;
			DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(T), new Type[] { typeof(TParent) }, true);
			ILGenerator gen = getterMethod.GetILGenerator();

			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Ldfld, field);
			gen.Emit(OpCodes.Ret);

			return (Func<TParent, T>)getterMethod.CreateDelegate(typeof(Func<TParent, T>));
		}
		private Action<TParent, T> CreateSetter() {
			if (field.FieldType != typeof(T)) throw new InvalidOperationException($"type of {field.Name} does not match provided type {typeof(T)}");
			string methodName = field.ReflectedType.FullName + ".set_" + field.Name;
			DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[] { typeof(TParent), typeof(T) }, true);
			ILGenerator gen = setterMethod.GetILGenerator();

			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Ldarg_1);
			gen.Emit(OpCodes.Stfld, field);
			gen.Emit(OpCodes.Ret);

			return (Action<TParent, T>)setterMethod.CreateDelegate(typeof(Action<TParent, T>));
		}
	}
}