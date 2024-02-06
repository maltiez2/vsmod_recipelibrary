using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RecipeLibrary.Ground;

public class GroundRecipeLoader : ModSystem
{
    public override double ExecuteOrder() => 1;
    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    private bool mClassExclusiveRecipes = true;
    private ICoreServerAPI? mServerApi;

    public override void AssetsLoaded(ICoreAPI api)
    {
        if (api is not ICoreServerAPI serverApi) return;

        mServerApi = serverApi;
        mClassExclusiveRecipes = serverApi.World.Config.GetBool("classExclusiveRecipes", true);
        LoadGroundRecipes(serverApi);
    }


    public void LoadGroundRecipes(ICoreServerAPI serverApi)
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


    public void LoadRecipe(AssetLocation location, GroundRecipe recipe)
    {
        if (!recipe.Enabled) return;
        if (!mClassExclusiveRecipes) recipe.RequiresTrait = null;
        if (recipe.Name == null) recipe.Name = location;

        Dictionary<string, string[]> nameToCodeMapping = recipe.ResolveWildcards(mServerApi.World);

        if (nameToCodeMapping.Count > 0)
        {
            List<GridRecipe> subRecipes = new();

            int qCombs = 0;
            bool first = true;
            foreach (KeyValuePair<string, string[]> val2 in nameToCodeMapping)
            {
                if (first) qCombs = val2.Value.Length;
                else qCombs *= val2.Value.Length;
                first = false;
            }

            first = true;
            foreach (KeyValuePair<string, string[]> val2 in nameToCodeMapping)
            {
                string variantCode = val2.Key;
                string[] variants = val2.Value;

                for (int i = 0; i < qCombs; i++)
                {
                    GridRecipe rec;

                    if (first) subRecipes.Add(rec = recipe.Clone());
                    else rec = subRecipes[i];

                    foreach (CraftingRecipeIngredient ingred in rec.Ingredients.Values)
                    {
                        if (ingred.Name == variantCode)
                        {
                            ingred.FillPlaceHolder(variantCode, variants[i % variants.Length]);
                            ingred.Code.Path = ingred.Code.Path.Replace("*", variants[i % variants.Length]);
                        }

                        if (ingred.ReturnedStack?.Code != null)
                        {
                            ingred.ReturnedStack.Code.Path = ingred.ReturnedStack.Code.Path.Replace("{" + variantCode + "}", variants[i % variants.Length]);
                        }
                    }

                    rec.Output.FillPlaceHolder(variantCode, variants[i % variants.Length]);
                }

                first = false;
            }

            foreach (GridRecipe subRecipe in subRecipes)
            {
                if (!subRecipe.ResolveIngredients(mServerApi.World)) continue;
                mServerApi.RegisterCraftingRecipe(subRecipe);
            }

        }
        else
        {
            if (!recipe.ResolveIngredients(mServerApi.World)) return;
            mServerApi.RegisterCraftingRecipe(recipe);
        }

    }
}
