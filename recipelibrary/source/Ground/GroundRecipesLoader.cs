using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Common;

namespace RecipesLibrary.Ground;

public class GroundRecipesLoader : ModSystem
{
    public Dictionary<CollectibleObject, List<GroundRecipe>> RecipesByOutput { get; } = new();
    public Dictionary<CollectibleObject, List<GroundRecipe>> RecipesByStarter { get; } = new();
    public Dictionary<int, GroundRecipe> RecipesByHashId { get; } = new();
    public List<GroundRecipe> Recipes { get; } = new();

    public override double ExecuteOrder() => 1.1;
    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("GroundRecipeBlock", typeof(GroundRecipeBlock));
        api.RegisterBlockEntityClass("GroundRecipeEntity", typeof(GroundRecipeEntity));
        api.RegisterCollectibleBehaviorClass("GroundRecipeStarterBehavior", typeof(GroundRecipeStarterBehavior));
        api.RegisterRecipeRegistry<GroundRecipeRegistry>(cRecipeRegistryCode);

        Instance = this;
        if (api is ICoreServerAPI serverApi) mServerApi = serverApi;
        if (api is ICoreClientAPI clientApi) mClientApi = clientApi;
    }
    public override void AssetsLoaded(ICoreAPI api)
    {
        mClassExclusiveRecipes = api.World.Config.GetBool("classExclusiveRecipes", true);

        if (api is ICoreServerAPI)
        {
            LoadGroundRecipes(api);
            GetRegistry(api)?.Serialize(Recipes.ToArray());
        }
    }
    public override void AssetsFinalize(ICoreAPI api)
    {
        Block[] blocks = api.World.SearchBlocks(new("recipeslib:groundrecipe"));
        if (blocks.Length == 0 || blocks[0] is not GroundRecipeBlock recipeBlock) return;

        int count = 0;
        foreach ((CollectibleObject collectible, List<GroundRecipe> recipes) in RecipesByStarter)
        {
            GroundRecipeStarterBehavior behavior = new(collectible)
            {
                Recipes = recipes,
                RecipeBlock = recipeBlock
            };
            //behavior.OnLoaded(api);

            collectible.CollectibleBehaviors = collectible.CollectibleBehaviors.Prepend(behavior).ToArray();
            count++;
        }

        api.World.Logger.Debug($"[Recipes lib] Collectible crafting behaviors added: {count}");
    }

    internal static GroundRecipesLoader? Instance;
    internal void ReloadRecipes()
    {
        if (mClientApi == null) return;

        GroundRecipeRegistry? registry = GetRegistry(mClientApi);

        if (registry == null)
        {
            mClientApi.Logger.Warning("[Recipes lib] Unable to retrieve recipes registry, recipes were not synchronized with server");
            return;
        }

        registry.Deserialize(mClientApi, out List<GroundRecipe> recipes);

        if (recipes.Count == 0)
        {
            return;
        }

        RecipesByOutput.Clear();
        RecipesByStarter.Clear();
        RecipesByHashId.Clear();
        Recipes.Clear();

        int count = 0;
        foreach (GroundRecipe recipe in recipes)
        {
            if (!recipe.ResolveIngredients(mClientApi.World)) continue;
            Register(recipe);
            count++;
        }

        mClientApi.Logger.Notification($"[Recipes lib] Loaded {count} recipes from server. ");
    }


    private bool mClassExclusiveRecipes = true;
    private const string cRecipeRegistryCode = "recipeslib:groundrecipes";
    private ICoreServerAPI? mServerApi;
    private ICoreClientAPI? mClientApi;

    private void LoadGroundRecipes(ICoreAPI api)
    {
        Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Logger, "recipes/ground");
        int recipeQuantity = 0;

        foreach ((AssetLocation location, JToken token) in files.Where(entry => entry.Value is JObject))
        {
            GroundRecipe recipe = token.ToObject<GroundRecipe>(location.Domain);
            recipe.EnsureDefaultValues();
            LoadRecipe(location, recipe);
            recipeQuantity++;
        }

        foreach ((AssetLocation location, JToken recipesArray) in files.Where(entry => entry.Value is JArray))
        {
            foreach (JToken token in recipesArray as JArray)
            {
                GroundRecipe recipe = token.ToObject<GroundRecipe>(location.Domain);
                recipe.EnsureDefaultValues();
                LoadRecipe(location, recipe);
                recipeQuantity++;
            }
        }
        
        api.World.Logger.Event($"[Recipes lib] {recipeQuantity} ground recipes loaded from {files.Count} files");
        api.World.Logger.StoryEvent(Lang.Get("Ground tinkering..."));
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
        if (starterCollectible != null)
        {
            if (!RecipesByStarter.ContainsKey(starterCollectible)) RecipesByStarter.Add(starterCollectible, new());
            RecipesByStarter[starterCollectible].Add(recipe);
        }

        RecipesByHashId.Add(recipe.HashId, recipe);
        Recipes.Add(recipe);
    }
    private void LoadRecipe(AssetLocation location, GroundRecipe recipe)
    {
        recipe.HashId = location.GetHashCode();

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

    private static GroundRecipeRegistry? GetRegistry(ICoreAPI api)
    {
        MethodInfo? getter = typeof(GameMain).GetMethod("GetRecipeRegistry", BindingFlags.Instance | BindingFlags.NonPublic);
        return (GroundRecipeRegistry?)getter?.Invoke(api.World, new object[] { cRecipeRegistryCode });
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

    public List<IAsset> GetAssets(AssetCategory category, bool shouldLoad = true)
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

internal class GroundRecipeRegistry : RecipeRegistryBase
{
    public byte[]? Data { get; set; }

    public override void FromBytes(IWorldAccessor resolver, int quantity, byte[] data)
    {
        Data = data;
        GroundRecipesLoader.Instance?.ReloadRecipes();
    }
    public override void ToBytes(IWorldAccessor resolver, out byte[] data, out int quantity)
    {
        data = Data ?? Array.Empty<byte>();
        quantity = 1;
    }

    public void Serialize(GroundRecipe[] recipes)
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
        Data = serializedRecipesList.ToArray();
    }
    public void Deserialize(ICoreAPI api, out List<GroundRecipe> recipes)
    {
        recipes = new();
        if (Data == null) return;

        using MemoryStream serializedRecipesList = new(Data);
        using BinaryReader reader = new(serializedRecipesList);

        int count = reader.ReadInt32();
        for (int recipeIndex = 0; recipeIndex < count; recipeIndex++)
        {
            GroundRecipe recipe = new();
            recipe.FromBytes(reader, api.World);
            recipes.Add(recipe);
        }
    }
}