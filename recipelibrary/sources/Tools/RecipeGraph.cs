using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace RecipesLibrary;

public class RecipeGraph : IRecipeGraph
{
    public IGraphMatchingRecipeNode Root { get; }
    public int Depth { get; }
    public IGraphMatchingRecipe Recipe { get; }

    public RecipeGraph(IGraphMatchingRecipe recipe, IWorldAccessor world)
    {
        IIngredientMatcher root = recipe.Root(world);

        List<List<IIngredientMatcher>> ingredients = recipe.Nodes(world);

        IEnumerable<IGraphMatchingRecipeNode> previousLayer = Construct(ingredients[^1], Array.Empty<RecipeNode>(), recipe);

        for (int layer = ingredients.Count - 2; layer >= 0; layer--)
        {
            previousLayer = Construct(ingredients[layer], previousLayer, recipe);
        }

        RecipeNode rootNode = new(recipe, root, previousLayer);
        Root = rootNode;
        Depth = rootNode.GetDepth();
        Recipe = recipe;
    }
    
    private IEnumerable<IGraphMatchingRecipeNode> Construct(List<IIngredientMatcher> matchers, IEnumerable<IGraphMatchingRecipeNode> children, IGraphMatchingRecipe recipe)
    {
        List<PreNode> preGraph = PreGraph.Construct(matchers.Count);

        foreach (PreNode node in preGraph)
        {
            ConstructRecursive(node, matchers, children, recipe);
        }

        IEnumerable<RecipeNode> result = preGraph.Select(node => node.Node).OfType<RecipeNode>();

        foreach (PreNode node in preGraph)
        {
            node.ClearNodes();
        }

        return result;
    }

    private void ConstructRecursive(PreNode preNode, List<IIngredientMatcher> matchers, IEnumerable<IGraphMatchingRecipeNode> children, IGraphMatchingRecipe recipe)
    {
        foreach (PreNode child in preNode.Children)
        {
            ConstructRecursive(child, matchers, children, recipe);
        }

        if (preNode.Children.Any())
        {
            preNode.Node = new RecipeNode(recipe, matchers[preNode.Self], preNode.Children.Select(node => node.Node).OfType<IGraphMatchingRecipeNode>());
        }
        else
        {
            preNode.Node = new RecipeNode(recipe, matchers[preNode.Self], children);
        }
    }
}

internal static class PreGraph
{
    private static readonly Dictionary<int, List<PreNode>> sGraphs = new();

    public static List<PreNode> Construct(int depth)
    {
        if (sGraphs.ContainsKey(depth)) return sGraphs[depth];

        sGraphs[depth] = CollapseHeads(CollapseTails(GenerateColumns(GetPermutations(depth))));

        return sGraphs[depth];
    }

    private static List<List<int>> GetPermutations(int N)
    {
        int[] numbers = new int[N + 1];
        for (int index = 0; index <= N; index++)
        {
            numbers[index] = index;
        }

        List<List<int>> result = new();
        GeneratePermutations(numbers, new List<int>(), new bool[numbers.Length], result);
        return result;
    }

    private static void GeneratePermutations(int[] numbers, List<int> currentPermutation, bool[] used, List<List<int>> result)
    {
        if (currentPermutation.Count == numbers.Length)
        {
            result.Add(new List<int>(currentPermutation));
            return;
        }

        for (int index = 0; index < numbers.Length; index++)
        {
            if (!used[index])
            {
                used[index] = true;
                currentPermutation.Add(numbers[index]);
                GeneratePermutations(numbers, currentPermutation, used, result);
                currentPermutation.RemoveAt(currentPermutation.Count - 1);
                used[index] = false;
            }
        }
    }

    private static List<List<PreNode>> GenerateColumns(List<List<int>> permutations)
    {
        List<List<PreNode>> permutationsColumns = new();

        foreach (List<int> permutation in permutations)
        {
            permutationsColumns.Add(GenerateColumn(permutation));
        }

        return permutationsColumns;
    }

    private static List<PreNode> GenerateColumn(List<int> permutation)
    {
        List<PreNode> column = new();

        PreNode root = new()
        {
            Self = permutation[0],
            Head = permutation.ToArray(),
            Tail = Array.Empty<int>(),
            Children = new(),
            Parents = new()
        };

        column.Add(root);

        PreNode previous = root;
        for (int index = 1; index < permutation.Count; index++)
        {
            previous = new()
            {
                Self = permutation[index],
                Head = permutation.Skip(index).ToArray(),
                Tail = permutation.Take(index + 1).ToArray(),
                Children = new(),
                Parents = new() { previous }
            };

            column.Add(previous);
        }

        return column;
    }

    private static List<PreNode> CollapseTails(List<List<PreNode>> permutations)
    {
        int lowestLayer = (int)Math.Ceiling(permutations.Count / 2.0f);
        for (int layer = permutations.Count - 1; layer >= lowestLayer; layer--)
        {
            IEnumerable<PreNode> layerNodes = permutations.Select(column => column[layer]);

            CollapseLayerTails(layerNodes);
        }

        return permutations.Select(column => column[0]).ToList();
    }

    private static void CollapseLayerTails(IEnumerable<PreNode> layer)
    {
        Dictionary<int[], PreNode> uniqueNodes = new();

        foreach (PreNode node in layer)
        {
            if (!uniqueNodes.ContainsKey(node.Tail))
            {
                uniqueNodes.Add(node.Tail, node);
                continue;
            }

            node.ReplaceInParents(uniqueNodes[node.Tail]);
        }
    }

    private static List<PreNode> CollapseHeads(List<PreNode> permutations)
    {
        int depth = permutations[0].GetDepth();

        int highestLayer = (int)Math.Floor(depth / 2.0f);

        IEnumerable<PreNode> layerNodes = permutations;
        for (int layer = 0; layer <= highestLayer; layer++)
        {
            CollapseLayerHeads(layerNodes);

            layerNodes = layerNodes.Where(node => node.Children.Any()).SelectMany(node => node.Children);
        }

        return permutations.Where(node => node.Children.Any()).ToList();
    }

    private static void CollapseLayerHeads(IEnumerable<PreNode> layer)
    {
        Dictionary<int[], PreNode> uniqueNodes = new();

        foreach (PreNode node in layer)
        {
            if (!uniqueNodes.ContainsKey(node.Head))
            {
                uniqueNodes.Add(node.Head, node);
                continue;
            }

            node.ReplaceInChildren(uniqueNodes[node.Head]);
        }
    }
}

internal sealed class PreNode
{
    public int Self { get; set; }
    public int[] Head { get; set; } = Array.Empty<int>();
    public int[] Tail { get; set; } = Array.Empty<int>();
    public List<PreNode> Children { get; set; } = new();
    public List<PreNode> Parents { get; set; } = new();

    public IGraphMatchingRecipeNode? Node { get; set; }

    public void ReplaceChild(PreNode from, PreNode to)
    {
        Children.Remove(from);
        Children.Add(to);
    }

    public void ReplaceInParents(PreNode with)
    {
        foreach (PreNode parent in Parents)
        {
            parent.ReplaceChild(this, with);
        }

        Parents.Clear();
    }

    public void ReplaceParent(PreNode from, PreNode to)
    {
        Parents.Remove(from);
        Parents.Add(to);
    }

    public void ReplaceInChildren(PreNode with)
    {
        foreach (PreNode parent in Children)
        {
            parent.ReplaceParent(this, with);
        }

        Children.Clear();
    }

    public int GetDepth(int prev = 1)
    {
        if (Children.Count == 0) return prev;

        return GetDepth(prev + 1);
    }

    public void ClearNodes()
    {
        Node = null;

        foreach (PreNode child in Children)
        {
            child.ClearNodes();
        }
    }
}

public sealed class RecipeNode : IGraphMatchingRecipeNode
{
    public IGraphMatchingRecipe Recipe { get; }
    public IIngredientMatcher Matcher { get; }
    public IEnumerable<IGraphMatchingRecipeNode> Children { get; }

    public RecipeNode(IGraphMatchingRecipe recipe, IIngredientMatcher matcher, IEnumerable<IGraphMatchingRecipeNode> children)
    {
        Children = children;
        Recipe = recipe;
        Matcher = matcher;
    }

    public bool Match(ItemSlot slot) => Matcher.Match(slot);
    public bool Last() => !Children.Any();
    public IEnumerable<IGraphMatchingRecipeNode> Next(ItemSlot slot)
    {
        return Children.Where(x => x.Match(slot));
    }
    public int GetDepth(int prev = 1)
    {
        if (!Children.Any()) return prev;

        return GetDepth(prev + 1);
    }
}
