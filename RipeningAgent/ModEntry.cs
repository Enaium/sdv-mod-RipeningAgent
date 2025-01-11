using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace RipeningAgent;

public class ModEntry : Mod
{
    private static ModEntry _instance;

    public const string BasicRipeningAgentId = "RipeningAgent_BasicRipeningAgent";
    public const string DeluxeRipeningAgentId = "RipeningAgent_DeluxeRipeningAgent";

    public ModEntry()
    {
        _instance = this;
    }

    public override void Entry(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo("Data/Objects"))
        {
            e.Edit(assets =>
            {
                var dict = assets.AsDictionary<string, ObjectData>();
                dict.Data[BasicRipeningAgentId] = new ObjectData
                {
                    Name = BasicRipeningAgentId,
                    DisplayName =
                        "[LocalizedText Strings\\RipeningAgent:RipeningAgent.Object.BasicRipeningAgent.DisplayName]",
                    Description =
                        "[LocalizedText Strings\\RipeningAgent:RipeningAgent.Object.BasicRipeningAgent.Description]",
                    Type = "Basic",
                    Texture = BasicRipeningAgentId
                };

                dict.Data[DeluxeRipeningAgentId] = new ObjectData
                {
                    Name = DeluxeRipeningAgentId,
                    DisplayName =
                        "[LocalizedText Strings\\RipeningAgent:RipeningAgent.Object.DeluxeRipeningAgent.DisplayName]",
                    Description =
                        "[LocalizedText Strings\\RipeningAgent:RipeningAgent.Object.DeluxeRipeningAgent.Description]",
                    Type = "Basic",
                    Texture = DeluxeRipeningAgentId
                };
            });
        }
        else if (e.Name.IsEquivalentTo(BasicRipeningAgentId))
        {
            e.LoadFromModFile<Texture2D>("assets/basic_ripening_agent.png", AssetLoadPriority.Medium);
        }
        else if (e.Name.IsEquivalentTo(DeluxeRipeningAgentId))
        {
            e.LoadFromModFile<Texture2D>("assets/deluxe_ripening_agent.png", AssetLoadPriority.Medium);
        }
        else if (e.Name.IsEquivalentTo("Data/CraftingRecipes"))
        {
            e.Edit(assets =>
            {
                var dict = assets.AsDictionary<string, string>();
                dict.Data[BasicRipeningAgentId] =
                    $"74 1 768 10 769 10/Field/{BasicRipeningAgentId}/false/s Farming 10/";
                dict.Data[DeluxeRipeningAgentId] =
                    $"74 10 768 100 769 100/Field/{DeluxeRipeningAgentId}/false/s Farming 10/";
            });
        }
        else if (e.Name.IsEquivalentTo("Strings/RipeningAgent"))
        {
            var locale = Helper.Translation.LocaleEnum switch
            {
                LocalizedContentManager.LanguageCode.en => "default",
                _ => Helper.Translation.LocaleEnum.ToString()
            };
            e.LoadFromModFile<Dictionary<string, string>>($"i18n/{locale}.json",
                AssetLoadPriority.Medium);
        }
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!e.Button.IsActionButton()) return;

        var item = Game1.player.CurrentItem;

        if (item == null) return;

        var location = Game1.currentLocation;
        var tile = Game1.currentCursorTile;

        var isDeluxe = item.Name == DeluxeRipeningAgentId;
        if (item.Name != BasicRipeningAgentId && !isDeluxe) return;

        var flag = Utility.withinRadiusOfPlayer(Game1.getMouseX() + Game1.viewport.X,
            Game1.getMouseY() + Game1.viewport.Y, 1, Game1.player);
        if (!flag) return;

        TerrainFeature? target = null;
        {
            if (location.terrainFeatures.TryGetValue(tile, out var terrainFeature))
            {
                target = terrainFeature switch
                {
                    HoeDirt { crop: not null } dirt => dirt,
                    Bush or FruitTree or Tree => terrainFeature,
                    _ => target
                };
            }
            else
            {
                var bush = location.largeTerrainFeatures.Where(locationLargeTerrainFeature =>
                        locationLargeTerrainFeature is Bush bush &&
                        new Rectangle(bush.Tile.ToPoint(), new Point(bush.size.Value, bush.size.Value)).Contains(
                            tile.ToPoint()))
                    .ToList();
                if (bush.Any())
                {
                    target = bush.First();
                }
            }

            if (target == null && location.objects.TryGetValue(tile, out var obj) &&
                obj is IndoorPot pot)
            {
                if (pot.hoeDirt.Value is { crop: not null } dirt)
                    target = dirt;

                if (pot.bush.Value is { } bush)
                    target = bush;
            }
        }

        var affected = false;

        switch (target)
        {
            case HoeDirt dirt:
                var crop = dirt.crop;
                for (var i = 0; i < 100 && !crop.fullyGrown.Value; i++)
                    crop.newDay(HoeDirt.watered);

                crop.growCompletely();
                affected = true;
                break;

            case Bush bush:
                if (bush.getAge() < Bush.daysToMatureGreenTeaBush)
                {
                    bush.datePlanted.Value = (int)(Game1.stats.DaysPlayed - Bush.daysToMatureGreenTeaBush);
                    bush.dayUpdate();
                    affected = true;
                }

                if (bush.inBloom() && bush.tileSheetOffset.Value == 0)
                {
                    bush.dayUpdate();
                    affected = true;
                }

                break;
            case FruitTree fruitTree:
                if (fruitTree.growthStage.Value < FruitTree.treeStage)
                {
                    if (isDeluxe)
                    {
                        fruitTree.growthStage.Value = FruitTree.treeStage;
                    }
                    else
                    {
                        fruitTree.growthStage.Value++;
                    }

                    fruitTree.daysUntilMature.Value = 0;
                    affected = true;
                }

                if (fruitTree.IsInSeasonHere())
                {
                    fruitTree.TryAddFruit();
                    affected = true;
                }

                break;

            case Tree tree:
                if (tree.growthStage.Value < Tree.treeStage)
                {
                    if (isDeluxe)
                    {
                        tree.growthStage.Value = Tree.treeStage;
                    }
                    else
                    {
                        tree.growthStage.Value++;
                    }

                    affected = true;
                }

                tree.wasShakenToday.Value = false;

                break;
        }

        if (!affected) return;
        Game1.player.reduceActiveItemByOne();
    }

    public static ModEntry GetInstance()
    {
        return _instance;
    }
}