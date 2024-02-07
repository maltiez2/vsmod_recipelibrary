using System.Collections.Generic;
using Vintagestory.API.Common;

namespace RecipeLibrary;
public interface IIngredientMatcher
{
    bool Match(ItemSlot slot);
}

public interface IGraphMatchingRecipe
{
    IIngredientMatcher Root(IWorldAccessor world);

    List<List<IIngredientMatcher>> Nodes(IWorldAccessor world);
}

public interface IGraphMatchingRecipeNode : IIngredientMatcher
{
    public IGraphMatchingRecipe Recipe { get; }
    public IIngredientMatcher Matcher { get; }
    public IEnumerable<IGraphMatchingRecipeNode> Children { get; }

    IEnumerable<IGraphMatchingRecipeNode> Next(ItemSlot slot);
    bool Last();
}

public interface IRecipeGraph
{
    public IGraphMatchingRecipeNode Root { get; }
    public int Depth { get; }
    public IGraphMatchingRecipe Recipe { get; }
}