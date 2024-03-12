using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RecipesLibrary.API;

public interface IRecipe
{
    bool Enabled { get; set; }
    string? RequiredTrait { get; }
}
public interface IHandBookRecipe
{
    bool ShowInCreatedBy { get; }
    int RecipeGroup { get; }
}

public interface IRecipeInput
{
    string Id { get; }

    bool Match(ItemSlot slot);
    void Take(IWorldAccessor world, Entity byEntity, ItemSlot slot, int count);
    IEnumerable<CollectibleObject> Find(IWorldAccessor world);
}

public interface IRecipeOutput
{
    ItemStack Result(Dictionary<IRecipeInput, ItemSlot> ingredients, int count);
}
