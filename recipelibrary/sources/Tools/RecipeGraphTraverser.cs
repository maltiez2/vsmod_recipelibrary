using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace RecipesLibrary;

public interface IRecipeGraphTraversalStack
{
    public int Depth { get; }
    public RecipeStackStatus Push(ItemSlot slot);
    public RecipeStackStatus Pop();
    public IEnumerable<IGraphMatchingRecipeNode> Peek(RecipeStackStatus status);
    public IEnumerable<RecipeStackNode> Nodes(RecipeStackStatus status);
}

public class RecipeGraphTraversalStack : IRecipeGraphTraversalStack
{
    public int Depth => mCurrentDepth;

    public RecipeGraphTraversalStack(IEnumerable<IRecipeGraph> graphs)
    {
        foreach (IRecipeGraph graph in graphs)
        {
            mMatch.Add(new Stack<RecipeStackNode>());
            mMatch[^1].Push(new(graph.Root));
        }
    }

    public RecipeStackStatus Push(ItemSlot slot)
    {
        RecipeStackStatus status = RecipeStackStatus.Empty;

        for (int graphIndex = 0; graphIndex < mMatch.Count; graphIndex++)
        {
            mMatch[graphIndex].Push(new(mMatch[graphIndex].Peek(), slot));

            CheckStatus(mMatch[graphIndex].Peek(), ref status);
        }

        mCurrentDepth++;

        return status;
    }

    public RecipeStackStatus Pop()
    {
        RecipeStackStatus status = RecipeStackStatus.Empty;

        foreach (Stack<RecipeStackNode> nodes in mMatch)
        {
            nodes.Pop();

            CheckStatus(nodes.Peek(), ref status);
        }

        mCurrentDepth--;

        return status;
    }

    public IEnumerable<IGraphMatchingRecipeNode> Peek(RecipeStackStatus status)
    {
        return mMatch
            .Where(stack => stack.Peek().Status == status)
            .Select(stack => stack.Peek())
            .SelectMany(element => element.Nodes ?? Array.Empty<IGraphMatchingRecipeNode>());
    }

    public IEnumerable<RecipeStackNode> Nodes(RecipeStackStatus status)
    {
        return mMatch
            .Where(stack => stack.Peek().Status == status)
            .Select(stack => stack.Peek());
    }

    private readonly List<Stack<RecipeStackNode>> mMatch = new();
    private int mCurrentDepth = 0;

    private static void CheckStatus(RecipeStackNode node, ref RecipeStackStatus status)
    {
        RecipeStackStatus nodeStatus = node.Status;

        switch (nodeStatus)
        {
            case RecipeStackStatus.Unmatched:
                if (status == RecipeStackStatus.Empty) status = RecipeStackStatus.Unmatched;
                break;
            case RecipeStackStatus.Matched:
                if (status != RecipeStackStatus.Completed) status = RecipeStackStatus.Matched;
                break;
            case RecipeStackStatus.Completed:
                status = RecipeStackStatus.Completed;
                break;
            case RecipeStackStatus.Empty:
                break;
        }
    }
}

public class RecipeStackNode
{
    public readonly IEnumerable<IGraphMatchingRecipeNode>? Nodes;
    public readonly RecipeStackStatus Status;
    public readonly RecipeStackNode? Parent;

    public RecipeStackNode(IGraphMatchingRecipeNode root)
    {
        Nodes = new List<IGraphMatchingRecipeNode>() { root };
        Status = RecipeStackStatus.Matched;
    }
    public RecipeStackNode(RecipeStackNode parent, ItemSlot slot)
    {
        Parent = parent;

        switch (parent.Status)
        {
            case RecipeStackStatus.Empty:
                Status = RecipeStackStatus.Empty;
                return;
            case RecipeStackStatus.Unmatched:
                Status = RecipeStackStatus.Unmatched;
                return;
            case RecipeStackStatus.Completed:
                Status = RecipeStackStatus.Empty;
                return;
            case RecipeStackStatus.Matched:
                bool completed = Nodes?.Select(node => node.Last()).Aggregate((first, second) => first || second) ?? false;

                if (completed)
                {
                    Status = RecipeStackStatus.Completed;
                    return;
                }

                Nodes = parent.Nodes?.Select(node => node.Next(slot)).SelectMany(element => element);
                if (Nodes?.Any() != true)
                {
                    Status = RecipeStackStatus.Unmatched;
                    return;
                }

                Status = RecipeStackStatus.Matched;
                return;
        }
    }
}

public enum RecipeStackStatus
{
    Unmatched,
    Matched,
    Completed,
    Empty
}
