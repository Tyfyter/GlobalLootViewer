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
using OnConditions = On.Terraria.GameContent.ItemDropRules.Conditions;

namespace GlobalLootViewer {
	public class GlobalLootViewer : Mod {
		public HashSet<Type> IgnoreWhenHighlighting { get; private set; }
		public FastFieldInfo<CustomEntryIcon, Func<bool>> _unlockCondition;
		public MethodInfo unlockCondition;
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
			On.Terraria.GameContent.UI.Elements.UIBestiaryFilteringOptionsGrid.ctor += UIBestiaryFilteringOptionsGrid_ctor;
			On.Terraria.GameContent.Bestiary.CustomEntryIcon.UpdateUnlockState += CustomEntryIcon_UpdateUnlockState;
			On.Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator.TryGivingEntryFlavorTextIfItIsMissing += BestiaryDatabaseNPCsPopulator_TryGivingEntryFlavorTextIfItIsMissing;
			_unlockCondition = new("_unlockCondition", BindingFlags.NonPublic | BindingFlags.Instance, true);
			unlockCondition = typeof(GlobalLootViewer).GetMethod("AlwaysUnlocked", BindingFlags.Public | BindingFlags.Static);
			On.Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator.GetExclusions += BestiaryDatabaseNPCsPopulator_GetExclusions;
			On.Terraria.ID.ContentSamples.BestiaryHelper.GetSortedBestiaryEntriesList += BestiaryHelper_GetSortedBestiaryEntriesList; NPCID.Sets.NPCBestiaryDrawOffset[GlobalLootViewerNPC.ID] = new() {
				Hide = false,
				CustomTexturePath = "Terraria/Images/UI/Camera_1"
			};
			NPCID.Sets.NPCBestiaryDrawOffset[HiddenLootViewerNPC.ID] = new() {
				Hide = false,
				CustomTexturePath = "Terraria/Images/UI/Camera_1"
			};
			On.Terraria.ID.ContentSamples.BestiaryHelper.ShouldHideBestiaryEntry += BestiaryHelper_ShouldHideBestiaryEntry;
			On.Terraria.ID.NPCID.Sets.GetLeinforsEntries += Sets_GetLeinforsEntries;
			On.Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator.AddEmptyEntries_CrittersAndEnemies_Automated += BestiaryDatabaseNPCsPopulator_AddEmptyEntries_CrittersAndEnemies_Automated;
		}

		private void BestiaryDatabaseNPCsPopulator_AddEmptyEntries_CrittersAndEnemies_Automated(On.Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator.orig_AddEmptyEntries_CrittersAndEnemies_Automated orig, BestiaryDatabaseNPCsPopulator self) {
			orig(self);
			self.
		}

		private Dictionary<int, NPCID.Sets.NPCBestiaryDrawModifiers> Sets_GetLeinforsEntries(On.Terraria.ID.NPCID.Sets.orig_GetLeinforsEntries orig) {
			var value = orig();
			value[GlobalLootViewerNPC.ID] = new() {
				Hide = false,
				CustomTexturePath = "Terraria/Images/UI/Camera_1"
			};
			value[HiddenLootViewerNPC.ID] = new() {
				Hide = false,
				CustomTexturePath = "Terraria/Images/UI/Camera_1"
			};
			return value;
		}
		//obsolete
		private bool BestiaryHelper_ShouldHideBestiaryEntry(On.Terraria.ID.ContentSamples.BestiaryHelper.orig_ShouldHideBestiaryEntry orig, NPC npc) {
			bool test = orig(npc);
			if (npc.netID == GlobalLootViewerNPC.ID || npc.netID == HiddenLootViewerNPC.ID) return false;
			return orig(npc);
		}
		//obsolete
		private List<KeyValuePair<int, NPC>> BestiaryHelper_GetSortedBestiaryEntriesList(On.Terraria.ID.ContentSamples.BestiaryHelper.orig_GetSortedBestiaryEntriesList orig, BestiaryDatabase database) {
			NPCID.Sets.NPCBestiaryDrawOffset[GlobalLootViewerNPC.ID] = new() {
				Hide = false,
				CustomTexturePath = "Terraria/Images/UI/Camera_1"
			};
			NPCID.Sets.NPCBestiaryDrawOffset[HiddenLootViewerNPC.ID] = new() {
				Hide = false,
				CustomTexturePath = "Terraria/Images/UI/Camera_1"
			};
			return orig(database);
		}
		//obsolete
		private HashSet<int> BestiaryDatabaseNPCsPopulator_GetExclusions(On.Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator.orig_GetExclusions orig) {
			HashSet<int> output = orig();
			output.Remove(GlobalLootViewerNPC.ID);
			output.Remove(HiddenLootViewerNPC.ID);
			return output;
		}

		private void BestiaryDatabaseNPCsPopulator_TryGivingEntryFlavorTextIfItIsMissing(On.Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator.orig_TryGivingEntryFlavorTextIfItIsMissing orig, BestiaryDatabaseNPCsPopulator self, BestiaryEntry entry) {
			if (entry.UIInfoProvider is UnlockedEnemyUICollectionInfoProvider or HiddenEnemyUICollectionInfoProvider) return;
			orig(self, entry);
		}

		private void CustomEntryIcon_UpdateUnlockState(On.Terraria.GameContent.Bestiary.CustomEntryIcon.orig_UpdateUnlockState orig, CustomEntryIcon self, bool state) {
			if (_unlockCondition.GetValue(self).Method != unlockCondition) {
				orig(self, state);
			}
		}

		private void UIBestiaryFilteringOptionsGrid_ctor(On.Terraria.GameContent.UI.Elements.UIBestiaryFilteringOptionsGrid.orig_ctor orig, UIBestiaryFilteringOptionsGrid self, Terraria.DataStructures.EntryFilterer<BestiaryEntry, IBestiaryEntryFilter> filterer) {
			filterer.AddFilters(new List<IBestiaryEntryFilter>() { new GlobalLootFilter() });
			orig(self, filterer);
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
		public static bool AlwaysUnlocked() => true;
	}
	public class GlobalLootViewerGlobalNPC : GlobalNPC {
		public override void SetBestiary(NPC npc, BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
			if (npc.type == GlobalLootViewerNPC.ID) {
				bestiaryEntry.Icon = new CustomEntryIcon("Global Loot Viewer", "Images/UI/Bestiary", GlobalLootViewer.AlwaysUnlocked);
				typeof(CustomEntryIcon).GetField("_sourceRectangle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bestiaryEntry.Icon, new Rectangle(32, 2, 30, 26));
				bestiaryEntry.UIInfoProvider = new UnlockedEnemyUICollectionInfoProvider();
				if (bestiaryEntry.Info[1] is NamePlateInfoElement) {
					bestiaryEntry.Info[1] = new NamePlateInfoElement("Global Loot", GlobalLootViewerNPC.ID);
				}
				bestiaryEntry.Info.RemoveAll(i => i is NPCPortraitInfoElement or NPCStatsReportInfoElement or FlavorTextBestiaryInfoElement);
				bestiaryEntry.AddTags(
					Mod.ModSourceBestiaryInfoElement
				);
			}else if (npc.type == HiddenLootViewerNPC.ID) {
				bestiaryEntry.Icon = new CustomEntryIcon("Global Loot Viewer (Hidden Loot)", "Images/UI/Camera_1", GlobalLootViewer.AlwaysUnlocked);
				typeof(CustomEntryIcon).GetField("_sourceRectangle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bestiaryEntry.Icon, new Rectangle(0, 0, 32, 32));
				bestiaryEntry.UIInfoProvider = new UnlockedEnemyUICollectionInfoProvider();
				if (bestiaryEntry.Info[1] is NamePlateInfoElement) {
					bestiaryEntry.Info[1] = new NamePlateInfoElement("Hidden Global Loot", HiddenLootViewerNPC.ID);
				}
				bestiaryEntry.Info.RemoveAll(i => i is NPCPortraitInfoElement or NPCStatsReportInfoElement or FlavorTextBestiaryInfoElement);
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
		public static int ID => NPCID.BigPincushionZombie;
	}
	public static class HiddenLootViewerNPC {
		public static int ID => NPCID.SmallPincushionZombie;
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