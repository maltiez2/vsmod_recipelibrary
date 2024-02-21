using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Common;

namespace RecipesLibrary.Ground;

public class GroundRecipeLoader : ModSystem
{
    public Dictionary<CollectibleObject, List<GroundRecipe>> RecipesByOutput { get; } = new();
    public Dictionary<CollectibleObject, List<GroundRecipe>> RecipesByStarter { get; } = new();
    public List<GroundRecipe> Recipes { get; } = new();

    public override double ExecuteOrder() => 1;
    public override void AssetsLoaded(ICoreAPI api)
    {
        if (api is ICoreServerAPI serverApi)
        {
            mServerApi = serverApi;
            mClassExclusiveRecipes = serverApi.World.Config.GetBool("classExclusiveRecipes", true);
            LoadGroundRecipes(serverApi);
            StoreRecipes(serverApi, Recipes.ToArray());
        }

        if (api is ICoreClientAPI clientApi)
        {
            RetrieveRecipes(clientApi, out List<GroundRecipe> recipes);
            foreach (GroundRecipe recipe in recipes)
            {
                Register(recipe);
            }
        }
    }
    public override void AssetsFinalize(ICoreAPI api)
    {
        Block[] blocks = api.World.SearchBlocks(new("recipeslib:groundrecipe"));
        if (blocks.Length == 0 || blocks[0] is not GroundRecipeBlock recipeBlock) return;

        foreach ((CollectibleObject collectible, List<GroundRecipe> recipes) in RecipesByStarter)
        {
            GroundRecipeStarterBehavior behavior = new(collectible)
            {
                Recipes = recipes,
                RecipeBlock = recipeBlock
            };

            collectible.CollectibleBehaviors = collectible.CollectibleBehaviors.Prepend(behavior).ToArray();
        }
    }


    private bool mClassExclusiveRecipes = true;
    private const string cRecipesSyncAsset = "config/recipeslib/groundrecipes";
    private ICoreServerAPI? mServerApi;

    private void LoadGroundRecipes(ICoreServerAPI serverApi)
    {
        Dictionary<AssetLocation, JToken> files = serverApi.Assets.GetMany<JToken>(serverApi.Server.Logger, "recipes/ground");
        int recipeQuantity = 0;

        foreach ((AssetLocation location, JObject recipe) in files.OfType<(AssetLocation, JObject)>())
        {
            LoadRecipe(location, recipe.ToObject<GroundRecipe>(location.Domain));
            recipeQuantity++;
        }

        foreach ((AssetLocation location, JArray recipesArray) in files.OfType<(AssetLocation, JArray)>())
        {
            foreach (JToken token in recipesArray)
            {
                LoadRecipe(location, token.ToObject<GroundRecipe>(location.Domain));
                recipeQuantity++;
            }
        }

        serverApi.World.Logger.Event("{0} ground recipes loaded from {1} files", recipeQuantity, files.Count);
        serverApi.World.Logger.StoryEvent(Lang.Get("Ground inventions...")); // @TODO Come up with something better
    }
    private static void GenerateSubRecipeFromVariants(string variant, string[] wildcards, int combinationIndex, GroundRecipe recipe, List<GroundRecipe> subRecipes, ref bool first)
    {
        GroundRecipe subRecipe;

        if (first)
        {
            subRecipe = recipe.Clone();
            subRecipes.Add(subRecipe);
            first = false;
        }
        else
        {
            subRecipe = subRecipes[combinationIndex];
        }

        foreach (GroundRecipeIngredient ingredient in subRecipe.Ingredients.SelectMany(list => list))
        {
            if (ingredient.Name == variant)
            {
                ingredient.FillPlaceHolder(variant, wildcards[combinationIndex % wildcards.Length]);
                ingredient.Code.Path = ingredient.Code.Path.Replace("*", wildcards[combinationIndex % wildcards.Length]);
            }

            if (ingredient.ReturnedStack?.Code != null)
            {
                ingredient.ReturnedStack.Code.Path = ingredient.ReturnedStack.Code.Path.Replace("{" + variant + "}", wildcards[combinationIndex % wildcards.Length]);
            }
        }

        subRecipe.Output.FillPlaceHolder(variant, wildcards[combinationIndex % wildcards.Length]);
        subRecipe.Starter.FillPlaceHolder(variant, wildcards[combinationIndex % wildcards.Length]);
        subRecipe.Finisher?.FillPlaceHolder(variant, wildcards[combinationIndex % wildcards.Length]);
    }
    private void Register(GroundRecipe recipe)
    {
        CollectibleObject? outputCollectible = recipe.Output.ResolvedItemstack?.Collectible;
        if (outputCollectible != null)
        {
            if (!RecipesByOutput.ContainsKey(outputCollectible)) RecipesByOutput.Add(outputCollectible, new());
            RecipesByOutput[outputCollectible].Add(recipe);
        }

        CollectibleObject? starterCollectible = recipe.Starter.ResolvedItemstack?.Collectible;
        if (outputCollectible != null)
        {
            if (!RecipesByStarter.ContainsKey(starterCollectible)) RecipesByOutput.Add(starterCollectible, new());
            RecipesByOutput[starterCollectible].Add(recipe);
        }

        Recipes.Add(recipe);
    }
    private void LoadRecipe(AssetLocation location, GroundRecipe recipe)
    {
        if (mServerApi == null) return;
        if (!recipe.Enabled) return;
        if (!mClassExclusiveRecipes) recipe.RequiresTrait = null;
        if (recipe.Name == null) recipe.Name = location;

        Dictionary<string, string[]> resolvedWildcards = recipe.ResolveWildcards(mServerApi.World);

        if (resolvedWildcards.Count == 0)
        {
            if (!recipe.ResolveIngredients(mServerApi.World)) return;
            Register(recipe);
            return;
        }

        int combinations = 1;

        foreach ((string variant, string[] wildcards) in resolvedWildcards)
        {
            combinations *= wildcards.Length;
        }

        List<GroundRecipe> subRecipes = new();

        bool first = true;

        foreach ((string variant, string[] wildcards) in resolvedWildcards)
        {
            for (int combinationIndex = 0; combinationIndex < combinations; combinationIndex++)
            {
                GenerateSubRecipeFromVariants(variant, wildcards, combinationIndex, recipe, subRecipes, ref first);
            }
        }

        foreach (GroundRecipe subRecipe in subRecipes)
        {
            if (!subRecipe.ResolveIngredients(mServerApi.World)) continue;
            Register(subRecipe);
        }
    }

    private static void RetrieveRecipes(ICoreAPI api, out List<GroundRecipe> recipes)
    {
        IAsset asset = api.Assets.Get(cRecipesSyncAsset);
        byte[] data = asset.Data;
        recipes = new();

        using MemoryStream serializedRecipesList = new(data);
        using (BinaryReader reader = new(serializedRecipesList))
        {
            int count = reader.ReadInt32();
            for (int recipeIndex = 0; recipeIndex < count; recipeIndex++)
            {
                GroundRecipe recipe = new();
                recipe.FromBytes(reader, api.World);
                recipes.Add(recipe);
            }
        }
    }
    private static void StoreRecipes(ICoreAPI api, GroundRecipe[] recipes)
    {
        using MemoryStream serializedRecipesList = new();
        using (BinaryWriter writer = new(serializedRecipesList))
        {
            writer.Write(recipes.Length);
            foreach (GroundRecipe recipe in recipes)
            {
                recipe.ToBytes(writer);
            }
        }

        byte[] recipeData = serializedRecipesList.ToArray();

        AssetLocation location = new("recipeslib", cRecipesSyncAsset);
        Asset configAsset = new(recipeData, location, new GroundRecipeOrigin(recipeData, location));
        api?.Assets.Add(location, configAsset);
    }
}

internal class GroundRecipeOrigin : IAssetOrigin
{
    public string OriginPath { get; protected set; }

    private readonly byte[] mData;
    private readonly AssetLocation mLocation;

    public GroundRecipeOrigin(byte[] data, AssetLocation location)
    {
        mData = data;
        mLocation = location;
        OriginPath = mLocation.Path;
    }

    public void LoadAsset(IAsset asset)
    {

    }

    public bool TryLoadAsset(IAsset asset)
    {
        return true;
    }

    public List<IAsset> GetAssets(AssetCategory Category, bool shouldLoad = true)
    {
        List<IAsset> list = new()
        {
            new Asset(mData, mLocation, this)
        };

        return list;
    }

    public List<IAsset> GetAssets(AssetLocation baseLocation, bool shouldLoad = true)
    {
        List<IAsset> list = new()
        {
            new Asset(mData, mLocation, this)
        };

        return list;
    }

    public virtual bool IsAllowedToAffectGameplay()
    {
        return true;
    }
}