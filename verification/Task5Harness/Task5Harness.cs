using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Utilities;
using Terraria;
using Terraria.ModLoader;

namespace Task5HarnessMod;
public class Task5Harness : Mod { }
public class Task5Checks : ModSystem
{
    readonly List<string> output = new();
    int failures;
    Mod content;
    int magic, melee, bag, beacon, boss;
    Player player;
    public override void PostWorldLoad()
    {
        string root = Environment.GetEnvironmentVariable("ADAM_SMASHER_LAB_ROOT");
        if (!Main.dedServ || string.IsNullOrEmpty(root)) return;
        content = ModLoader.GetMod("AdamSmasher");
        int oldMode = Main.netMode, oldGame = Main.GameMode, oldPlayer = Main.myPlayer;
        Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
        try {
            magic = content.Find<ModItem>("AnnihilationProtocol").Type;
            melee = content.Find<ModItem>("ArasakaMantisBlades").Type;
            bag = content.Find<ModItem>("SmasherBag").Type;
            beacon = content.Find<ModItem>("ArasakaBeacon").Type;
            boss = content.Find<ModNPC>("AdamSmasherBoss").Type;
            player = Main.player[0] = new Player { whoAmI = 0, active = true };
            player.position = new Microsoft.Xna.Framework.Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16);
            Run("Loaded bag defaults and single luck-independent rule", () => {
                Item item = new(bag);
                Check(ItemID.Sets.BossBag[bag] && item.expert && item.consumable && item.ModItem.CanRightClick(), "Boss bag defaults");
                var rules = Main.ItemDropsDB.GetRulesForItemID(bag);
                Check(rules.Count == 1, "Bag has exactly one drop rule");
                Inspect(rules.Single());
                var factory = content.Code.GetType("AdamSmasherMod.Common.SmasherLoot").GetMethod("CreateWeaponRule", BindingFlags.Public | BindingFlags.Static);
                Inspect((IItemDropRule)factory.Invoke(null, null));
            });
            Run("Actual engine resolver opens 1000 bags: exactly one weapon each", () => {
                Main.GameMode = 1;
                UnifiedRandom rng = new(5052026);
                var resolver = new ItemDropResolver(Main.ItemDropsDB);
                int gunCount = 0, bladeCount = 0, sameNeighbours = 0, previous = -1;
                for (int i = 0; i < 1000; i++) {
                    ClearDrops(); player.luck = i < 500 ? 1f : -1f;
                    resolver.TryDropping(new DropAttemptInfo { item = bag, player = player, rng = rng, IsExpertMode = true, IsMasterMode = false, IsInSimulation = false });
                    Item[] weapons = Drops().Where(x => x.type == magic || x.type == melee).ToArray();
                    Check(weapons.Length == 1 && weapons[0].stack == 1 && Drops().Length == 1, $"Bag {i} exactly one weapon");
                    int chosen = weapons[0].type;
                    if (chosen == magic) gunCount++; else bladeCount++;
                    if (chosen == previous) sameNeighbours++;
                    previous = chosen;
                }
                Check(gunCount is >= 400 and <= 600 && bladeCount is >= 400 and <= 600 && sameNeighbours > 0, "50/50 statistical bounds; no forced alternating");
                output.Add($"COUNTS: 1000 bags magic={gunCount}, melee={bladeCount}, exactlyOne=1000, sameAdjacent={sameNeighbours}, seed=5052026, luck=+1/-1");
            });
            Run("Normal/Expert/Master actual registered NPC loot", () => {
                var rules = Main.ItemDropsDB.GetRulesForNPCID(boss, false);
                output.Add($"REGISTERED NPC RULES: {rules.Count} total (Calamity adds global rules); own guaranteed choice inspected separately");
                var expert = rules.OfType<DropBasedOnExpertMode>().Single();
                Check(expert.ruleForExpertMode is DropLocalPerClientAndResetsNPCMoneyTo0, "Engine per participant BossBag rule");
                var normal = rules.OfType<LeadingConditionRule>().Single(x => x.condition is Conditions.NotExpert);
                Check(normal.condition is Conditions.NotExpert && normal.ChainedRules.Count == 1, "Normal nonexpert branch only");
                Inspect(normal.ChainedRules.Single().RuleToChain);
                for (int mode = 0; mode < 3; mode++) {
                    Main.GameMode = mode;
                    NPC npc = new(); npc.SetDefaults(boss); npc.Center = player.Center; npc.playerInteraction[0] = true;
                    ClearDrops();
                    new ItemDropResolver(Main.ItemDropsDB).TryDropping(new DropAttemptInfo { npc = npc, player = player, rng = new UnifiedRandom(900+mode), IsExpertMode = mode > 0, IsMasterMode = mode == 2, IsInSimulation = false });
                    int weapons = Drops().Where(x => x.type == magic || x.type == melee).Sum(x => x.stack);
                    int bags = Drops().Where(x => x.type == bag).Sum(x => x.stack);
                    Check(mode == 0 ? weapons == 1 && bags == 0 : weapons == 0 && bags == 1, $"Mode {mode} exact reward branch");
                    output.Add($"BRANCH: {(mode == 0 ? "Normal" : mode == 1 ? "Expert" : "Master")} directWeapons={weapons}, bags={bags}");
                }
            });
            Run("Loaded exact Calamity recipe, reusable beacon", () => {
                Mod cal = ModLoader.GetMod("CalamityMod");
                var recipes = Main.recipe.Take(Recipe.numRecipes).Where(x => x.createItem.type == beacon).ToArray();
                Check(recipes.Length == 1, "One beacon recipe registered");
                Recipe recipe = recipes.Single();
                var ingredients = recipe.requiredItem.ToDictionary(x => x.type, x => x.stack);
                Check(ingredients.Count == 3 && ingredients[cal.Find<ModItem>("ShadowspecBar").Type] == 5 && ingredients[cal.Find<ModItem>("MysteriousCircuitry").Type] == 25 && ingredients[cal.Find<ModItem>("DubiousPlating").Type] == 25, "Exact installed ingredient types and amounts");
                Check(recipe.requiredTile.Count == 1 && recipe.requiredTile[0] == cal.Find<ModTile>("DraedonsForge").Type && recipe.Conditions.Count == 0 && recipe.createItem.stack == 1, "Forge ModTile and no progression/save condition");
                Check(!new Item(beacon).consumable, "Reusable beacon retained");
                output.Add($"RECIPE: ShadowspecBar5 + MysteriousCircuitry25 + DubiousPlating25; DraedonsForge tile={recipe.requiredTile[0]}; conditions=0");
            });
            Run("Installed BossChecklist actual registered entry", () => {
                Mod checklist = ModLoader.GetMod("BossChecklist");
                Check(checklist.Version == new Version(2,2,4), "Installed version 2.2.4");
                var entries = (Dictionary<string, Dictionary<string,object>>)checklist.Call("GetBossInfoDictionary", Mod, "2.2.4");
                var entry = entries.Single(x => x.Key == "AdamSmasher AdamSmasher").Value;
                Check(((List<int>)entry["npcIDs"]).SequenceEqual(new[] { boss }) && ((List<int>)entry["spawnItems"]).SequenceEqual(new[] { beacon }) && (int)entry["treasureBag"] == bag, "Boss, summon and bag inferred correctly");
                // Boss Rush is an event (25.99), not a late boss. Compare boss ordering.
                float maxCal = entries.Where(x => x.Key.StartsWith("CalamityMod ") && (bool)x.Value["isBoss"]).Select(x => (float)x.Value["progression"]).Max();
                Check((float)entry["progression"] > maxCal, "Checklist ordering after late Calamity");
                var world = content.Code.GetType("AdamSmasherMod.Common.SmasherWorld").GetField("Downed");
                bool previous = (bool)world.GetValue(null);
                world.SetValue(null, false); Check(!((Func<bool>)entry["downed"])(), "Downed predicate false");
                world.SetValue(null, true); Check(((Func<bool>)entry["downed"])(), "Downed predicate true"); world.SetValue(null, previous);
                Check(((Func<LocalizedText>)entry["spawnInfo"])().Value.Contains("Beacon"), "Localized spawn info");
                output.Add($"BOSSCHECKLIST: {checklist.Version} entry=AdamSmasher AdamSmasher; progression={entry["progression"]}, maxCalamity={maxCal}, npc={boss}, beacon={beacon}, bag={bag}");
            });
            Run("RU/EN localization names, mechanics and recipe", () => {
                foreach (string culture in new[] { "en-US", "ru-RU" }) {
                    LanguageManager.Instance.SetLanguage(GameCulture.FromCultureName(culture == "en-US" ? GameCulture.CultureName.English : GameCulture.CultureName.Russian));
                    foreach (string name in new[] { "ArasakaBeacon", "AnnihilationProtocol", "ArasakaMantisBlades", "SmasherBag" }) {
                        string key = $"Mods.AdamSmasher.Items.{name}";
                        Check(Language.GetTextValue(key+".DisplayName") != key+".DisplayName" && Language.GetTextValue(key+".Tooltip").Length > 40, $"{culture} {name}");
                    }
                    Check(Language.GetTextValue("Mods.AdamSmasher.Items.SmasherBag.Tooltip").Contains("50%"), "Explicit 50/50");
                    Check(Language.GetTextValue("Mods.AdamSmasher.Items.ArasakaBeacon.Tooltip").Contains("25"), "Recipe visible");
                }
                LanguageManager.Instance.SetLanguage(GameCulture.FromCultureName(GameCulture.CultureName.English));
            });
        } catch (Exception e) { output.Add("FAIL: setup " + e); failures++; }
        Main.netMode = oldMode; Main.GameMode = oldGame; Main.myPlayer = oldPlayer;
        output.Add($"RESULT: failures={failures}; actual UI/client/network transport unverified");
        File.WriteAllLines(Path.Combine(root, "verification/task5-runtime.txt"), output);
        Environment.Exit(failures == 0 ? 0 : 2);
    }
    void Inspect(IItemDropRule rule) {
        Check(rule is OneFromOptionsNotScaledWithLuckDropRule, "Luck-independent rule class");
        var choice = (OneFromOptionsNotScaledWithLuckDropRule)rule;
        Check(choice.chanceDenominator == 1 && choice.chanceNumerator == 1 && choice.dropIds.SequenceEqual(new[] { magic, melee }) && choice.ChainedRules.Count == 0, "Guaranteed choice between exactly two weapons");
    }
    static Item[] Drops() => Main.item.Take(Main.maxItems).Where(x => x.active).ToArray();
    static void ClearDrops() { foreach (var item in Main.item) item.active = false; Array.Clear(Main.timeItemSlotCannotBeReusedFor); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    void Run(string name, Action action) { try { action(); output.Add("PASS: " + name); } catch(Exception e) { failures++; output.Add("FAIL: " + name + " — " + e); } }
}
