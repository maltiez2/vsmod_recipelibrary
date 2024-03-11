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

    public override void OnLoaded(ICoreAPI api)
    {
        foreach (GroundRecipe recipe in Recipes)
        {
            if (recipe.SurfaceRequirement != null)
            {
                mSurfaceMatchers.Add(recipe, new(recipe.SurfaceRequirement));
            }
            else
            {
                mAnySurfaceRecipes.Add(recipe);
            }
        }
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!byEntity.Controls.ShiftKey || byEntity.Controls.CtrlKey || byEntity is not EntityPlayer player || RecipeBlock == null)
        {
            handling = EnumHandling.PassThrough;
            return;
        }

        IEnumerable<GroundRecipe> recipes = Match(blockSel);

        if (!recipes.Any() || !RecipeBlock.TryCreateSpot(byEntity.World, blockSel, player.Player, recipes))
        {
            handling = EnumHandling.PassThrough;
            return;
        }

        handHandling = EnumHandHandling.PreventDefaultAction;
        handling = EnumHandling.Handled;
    }

    private readonly Dictionary<GroundRecipe, AssetLocation> mSurfaceMatchers = new();
    private readonly List<GroundRecipe> mAnySurfaceRecipes = new();

    private IEnumerable<GroundRecipe> Match(BlockSelection selection)
    {
        IEnumerable<GroundRecipe> recipes = mSurfaceMatchers.Where(entry => selection.Block?.WildCardMatch(entry.Value) ?? false).Select(entry => entry.Key);
        return recipes.Concat(mAnySurfaceRecipes);
    }
}
