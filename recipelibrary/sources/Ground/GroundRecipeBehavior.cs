using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RecipesLibrary.Ground;

public class GroundRecipeStarterBehavior : CollectibleBehavior
{
    public List<GroundRecipe> Recipes { get; set; } = new();
    public GroundRecipeBlock? RecipeBlock { get; set; }

    public GroundRecipeStarterBehavior(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        if (Recipes.Count > 0)
        {
            JArray surfaceMatchers = new();
            foreach (string wildcard in Recipes.Where(recipe => recipe.SurfaceRequirement != null).Select(recipe => recipe.SurfaceRequirement).OfType<string>())
            {
                surfaceMatchers.Add(new JValue(wildcard));
            }
            (properties.Token as JObject)?.Add("surfaceMatchers", surfaceMatchers);
        }   

        mProperties = properties;

        base.Initialize(properties);
    }

    public override void OnLoaded(ICoreAPI api)
    {
        if (Recipes.Count > 0)
        {
            List<AssetLocation> surfaceMatchers = new();
            foreach (string wildcard in Recipes.Where(recipe => recipe.SurfaceRequirement != null).Select(recipe => recipe.SurfaceRequirement).OfType<string>())
            {
                surfaceMatchers.Add(new(wildcard));
            }
            mSurfaceMatchers = surfaceMatchers.ToArray();
        }
        else
        {
            mSurfaceMatchers = mProperties?["surfaceMatchers"]?.AsArray().Select(json => new AssetLocation(json.AsString())).ToArray() ?? System.Array.Empty<AssetLocation>();
        }
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!byEntity.Controls.ShiftKey || byEntity.Controls.CtrlKey || byEntity is not EntityPlayer player || RecipeBlock == null)
        {
            handling = EnumHandling.PassThrough;
            return;
        }
        
        if (!Match(blockSel) || !RecipeBlock.TryCreateSpot(byEntity.World, blockSel, player.Player))
        {
            handling = EnumHandling.PassThrough;
            return;
        }

        handHandling = EnumHandHandling.PreventDefaultAction;
        handling = EnumHandling.Handled;
    }

    private JsonObject? mProperties;
    private AssetLocation[] mSurfaceMatchers = System.Array.Empty<AssetLocation>();

    private bool Match(BlockSelection selection)
    {
        return selection.Block?.WildCardMatch(mSurfaceMatchers) ?? false;
    }
}
