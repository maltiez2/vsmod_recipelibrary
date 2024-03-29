﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace RecipesLibrary.Ground;

public class GroundRecipeIngredient : CraftingRecipeIngredient, IIngredientMatcher
{
    public ModelTransform? GroundRecipeTransform { get; set; }
    public AssetLocation? Sound { get; set; }
    public bool Starter { get; set; }

    public void EnsureDefaultValues()
    {
        GroundRecipeIngredient defaultValues = new();

        if (GroundRecipeTransform == null) GroundRecipeTransform = defaultValues.GroundRecipeTransform;
        if (Sound == null) Sound = defaultValues.Sound;
        if (Starter == null) Starter = defaultValues.Starter;
    }

    #region Serialisation

    private static void ToBytes(ModelTransform transform, BinaryWriter writer)
    {
        writer.Write(transform.Translation.X);
        writer.Write(transform.Translation.Y);
        writer.Write(transform.Translation.Z);
        writer.Write(transform.Rotation.X);
        writer.Write(transform.Rotation.Y);
        writer.Write(transform.Rotation.Z);
        writer.Write(transform.Origin.X);
        writer.Write(transform.Origin.Y);
        writer.Write(transform.Origin.Z);
        writer.Write(transform.ScaleXYZ.X);
        writer.Write(transform.ScaleXYZ.Y);
        writer.Write(transform.ScaleXYZ.Z);
        writer.Write(transform.Rotate);
    }
    private static void FromBytes(ModelTransform transform, BinaryReader reader)
    {
        transform.Translation.X = reader.ReadSingle();
        transform.Translation.Y = reader.ReadSingle();
        transform.Translation.Z = reader.ReadSingle();
        transform.Rotation.X = reader.ReadSingle();
        transform.Rotation.Y = reader.ReadSingle();
        transform.Rotation.Z = reader.ReadSingle();
        transform.Origin.X = reader.ReadSingle();
        transform.Origin.Y = reader.ReadSingle();
        transform.Origin.Z = reader.ReadSingle();
        transform.ScaleXYZ.X = reader.ReadSingle();
        transform.ScaleXYZ.Y = reader.ReadSingle();
        transform.ScaleXYZ.Z = reader.ReadSingle();
        transform.Rotate = reader.ReadBoolean();
    }

    public override void ToBytes(BinaryWriter writer)
    {
        base.ToBytes(writer);

        writer.Write(GroundRecipeTransform != null);
        if (GroundRecipeTransform != null) ToBytes(GroundRecipeTransform, writer);

        writer.Write(Sound?.ToShortString() ?? "");

        writer.Write(Starter);

    }
    public override void FromBytes(BinaryReader reader, IWorldAccessor resolver)
    {
        base.FromBytes(reader, resolver);

        bool hasTransform = reader.ReadBoolean();
        if (hasTransform)
        {
            GroundRecipeTransform = new()
            {
                Translation = new(),
                Rotation = new(),
                Origin = new(),
                ScaleXYZ = new()
            };

            FromBytes(GroundRecipeTransform, reader);
        }

        string sound = reader.ReadString();
        Sound = sound == "" ? null : new(sound);

        Starter = reader.ReadBoolean();
    }

    public new GroundRecipeIngredient Clone()
    {
        GroundRecipeIngredient clone = new()
        {
            Code = Code.Clone(),
            Type = Type,
            Name = Name,
            Quantity = Quantity,
            IsWildCard = IsWildCard,
            IsTool = IsTool,
            ToolDurabilityCost = ToolDurabilityCost,
            AllowedVariants = ((AllowedVariants == null) ? null : ((string[])AllowedVariants.Clone())),
            SkipVariants = ((SkipVariants == null) ? null : ((string[])SkipVariants.Clone())),
            ResolvedItemstack = ResolvedItemstack?.Clone(),
            ReturnedStack = ReturnedStack?.Clone(),
            RecipeAttributes = RecipeAttributes?.Clone(),
            GroundRecipeTransform = GroundRecipeTransform?.Clone(),
            Sound = Sound?.Clone(),
            Starter = Starter
        };

        if (Attributes != null)
        {
            clone.Attributes = Attributes.Clone();
        }

        return clone;
    }

    public bool Match(ItemSlot slot) => SatisfiesAsIngredient(slot.Itemstack);

    #endregion
}

public class GroundRecipe : IGraphMatchingRecipe, IByteSerializable
{
    #region JsonFields

    public bool Enabled { get; set; } = true;

    public GroundRecipeIngredient Starter { get; set; } = new();
    public GroundRecipeIngredient? Finisher { get; set; } = new();
    public List<List<GroundRecipeIngredient>> Ingredients { get; set; } = new();
    public GroundRecipeIngredient Output { get; set; } = new();

    public string? RequiresTrait { get; set; }
    public bool AverageDurability { get; set; } = true;
    public bool ShowInCreatedBy { get; set; } = true;
    public int RecipeGroup { get; set; } = 0;
    public bool CopyAttributesTrigger { get; set; } = false;
    public int[] CopyAttributesFrom { get; set; } = Array.Empty<int>();
    public AssetLocation Name { get; set; } = new("");
    public string? SurfaceRequirement { get; set; } = null;

    [JsonConverter(typeof(JsonAttributesConverter))]
    public JsonObject? Attributes { get; set; }

    public void EnsureDefaultValues()
    {
        GroundRecipe defualtValues = new();

        if (Enabled == null) Enabled = defualtValues.Enabled;
        if (Starter == null) Starter = defualtValues.Starter;
        if (Finisher == null) Finisher = defualtValues.Finisher;
        if (Output == null) Output = defualtValues.Output;
        if (RequiresTrait == null) RequiresTrait = defualtValues.RequiresTrait;
        if (AverageDurability == null) AverageDurability = defualtValues.AverageDurability;
        if (ShowInCreatedBy == null) ShowInCreatedBy = defualtValues.ShowInCreatedBy;
        if (RecipeGroup == null) RecipeGroup = defualtValues.RecipeGroup;
        if (CopyAttributesTrigger == null) CopyAttributesTrigger = defualtValues.CopyAttributesTrigger;
        if (Name == null) Name = defualtValues.Name;
        if (SurfaceRequirement == null) SurfaceRequirement = defualtValues.SurfaceRequirement;
    }

    #endregion

    public IRecipeGraph? Graph { get; private set; }
    public int HashId { get; set; } = 0;

    #region Workaround

    private GridRecipe? mRecipeWorkaround;

    public void ConstructWorkaround()
    {
        mRecipeWorkaround = new()
        {
            // @TODO
        };
    }

    #endregion

    #region Resolvers

    public bool ResolveIngredients(IWorldAccessor world)
    {
        bool resolvedAll = true;

        if (!ResolveIngredient(Starter, world)) resolvedAll = false;
        if (!ResolveIngredient(Output, world)) resolvedAll = false;
        if (Finisher != null && !ResolveIngredient(Finisher, world)) resolvedAll = false;
        foreach (GroundRecipeIngredient ingredient in Ingredients.SelectMany(element => element))
        {
            if (!ResolveIngredient(ingredient, world)) resolvedAll = false;
        }

        Graph = new RecipeGraph(this, world);

        return resolvedAll;
    }
    private bool ResolveIngredient(GroundRecipeIngredient ingredient, IWorldAccessor world)
    {
        if (!ingredient.Resolve(world, "Ground recipe"))
        {
            LoggerWrapper.Error(world, this, $"Ground recipe '{Name}': {ingredient} cannot be resolved");
            return false;
        }

        return true;
    }

    public string[] ResolveTriggerWildcard(IWorldAccessor world)
    {
        if (Starter.Name == null || Starter.Name.Length == 0)
        {
            LoggerWrapper.Verbose(world, this, $"[ResolveWildcards()] Ground recipe '{Name}'. Trigger ingredient missing name, skipping.");
            return Array.Empty<string>();
        }

        if (!Starter.Code.Path.Contains('*'))
        {
            LoggerWrapper.Verbose(world, this, $"[ResolveWildcards()] Ground recipe '{Name}'. Trigger ingredient is not a wildcard, skipping.");

            return new string[] { Starter.Code.Path };
        }

        try
        {
            return GenerateCodes(Starter, world);
        }
        catch (Exception exception)
        {
            LoggerWrapper.Error(world, this, $"[ResolveWildcards()] Ground recipe '{Name}'. Exception on resolving wildcards for trigger.");
            LoggerWrapper.Verbose(world, this, $"[ResolveWildcards()] Ground recipe '{Name}'. Exception on resolving wildcards for trigger.\n\nException: {exception}\n\n");
            return Array.Empty<string>();
        }
    }
    public Dictionary<string, string[]> ResolveWildcards(IWorldAccessor world)
    {
        Dictionary<string, string[]> wildcards = new();

        foreach (List<GroundRecipeIngredient> ingredients in Ingredients)
        {
            ResolveWildcards(world, ingredients, wildcards);
        }

        return wildcards;
    }
    private void ResolveWildcards(IWorldAccessor world, List<GroundRecipeIngredient> ingredients, Dictionary<string, string[]> wildcards)
    {
        foreach (GroundRecipeIngredient ingredient in ingredients)
        {
            try
            {
                wildcards.Add(ingredient.Name, GenerateCodes(ingredient, world));
            }
            catch (Exception exception)
            {
                LoggerWrapper.Error(world, this, $"[ResolveWildcards()] Ground recipe '{Name}'. Exception on resolving wildcards for ingredient '{ingredient.Code}'.");
                LoggerWrapper.Verbose(world, this, $"[ResolveWildcards()] Ground recipe '{Name}'. Exception on resolving wildcards for ingredient '{ingredient.Code}'.\n\nException: {exception}\n\n");
            }
        }
    }
    private static string[] GenerateCodes(GroundRecipeIngredient ingredient, IWorldAccessor world)
    {
        int wildcardStartLength = ingredient.Code.Path.IndexOf("*");
        int wildcardEndLength = ingredient.Code.Path.Length - wildcardStartLength - 1;

        if (wildcardStartLength < 0) return new string[1] { ingredient.Code.Path };

        List<string> codes = new();

        if (ingredient.Type == EnumItemClass.Block)
        {
            for (int i = 0; i < world.Blocks.Count; i++)
            {
                Block block = world.Blocks[i];

                if (CheckCollectible(block, ingredient))
                {
                    codes.Add(block.Code.Path[wildcardStartLength..^wildcardEndLength]);
                }
            }
        }
        else
        {
            for (int i = 0; i < world.Items.Count; i++)
            {
                Item item = world.Items[i];

                if (CheckCollectible(item, ingredient))
                {
                    codes.Add(item.Code.Path[wildcardStartLength..^wildcardEndLength]);
                }
            }
        }

        return codes.ToArray();
    }
    private static bool CheckCollectible(CollectibleObject collectible, GroundRecipeIngredient ingredient)
    {
        if (collectible?.Code == null || collectible.IsMissing) return false;
        if (ingredient.SkipVariants != null && WildcardUtil.MatchesVariants(ingredient.Code, collectible.Code, ingredient.SkipVariants)) return false;
        if (!WildcardUtil.Match(ingredient.Code, collectible.Code, ingredient.AllowedVariants)) return false;

        return true;
    }

    #endregion

    #region Processing

    public bool ConsumeInput(IPlayer byPlayer, ItemSlot[] inputSlots)
    {
        List<GroundRecipeIngredient> exactMatchIngredients = GetExactMatchIngredients();
        List<GroundRecipeIngredient> wildcardIngredients = GetWildcardIngredients();

        for (int slotIndex = 0; slotIndex < inputSlots.Length; slotIndex++)
        {
            ItemStack inStack = inputSlots[slotIndex].Itemstack;
            if (inStack == null) continue;

            ConsumeExactMatch(inStack, exactMatchIngredients, byPlayer, inputSlots, inputSlots[slotIndex]);
            ConsumeWildcard(inStack, wildcardIngredients, byPlayer, inputSlots, inputSlots[slotIndex]);
        }

        return exactMatchIngredients.Count == 0;
    }
    private List<GroundRecipeIngredient> GetExactMatchIngredients()
    {
        if (Starter == null) return new();

        List<GroundRecipeIngredient> exactMatchIngredients = new();

        foreach (GroundRecipeIngredient ingredient in Ingredients.SelectMany(x => x).Where(item => !item.IsWildCard && !item.IsTool).Append(Starter))
        {
            ItemStack stack = ingredient.ResolvedItemstack;

            IEnumerable<GroundRecipeIngredient> alreadyMatching = exactMatchIngredients.Where(item => item.ResolvedItemstack.Satisfies(stack));

            if (alreadyMatching.Any())
            {
                alreadyMatching.First().ResolvedItemstack.StackSize += stack.StackSize;
            }
            else
            {
                exactMatchIngredients.Add(ingredient.Clone());
            }
        }

        return exactMatchIngredients;
    }
    private List<GroundRecipeIngredient> GetWildcardIngredients()
    {
        if (Starter == null) return new();

        return Ingredients.SelectMany(x => x).Append(Starter).Where(ingredient => ingredient.IsWildCard || ingredient.IsTool).ToList();
    }
    private void ConsumeExactMatch(ItemStack inStack, List<GroundRecipeIngredient> exactMatchIngredients, IPlayer byPlayer, ItemSlot[] inputSlots, ItemSlot fromSlot)
    {
        IEnumerable<GroundRecipeIngredient> satisfied = exactMatchIngredients.Where(ingredient => ingredient.ResolvedItemstack.Satisfies(inStack));

        if (!satisfied.Any()) return;

        GroundRecipeIngredient ingredient = satisfied.First();

        int quantity = Math.Min(ingredient.ResolvedItemstack.StackSize, inStack.StackSize);

        inStack.Collectible.OnConsumedByCrafting(inputSlots, fromSlot, mRecipeWorkaround, ingredient, byPlayer, quantity);

        ingredient.ResolvedItemstack.StackSize -= quantity;

        if (ingredient.ResolvedItemstack.StackSize <= 0)
        {
            exactMatchIngredients.Remove(ingredient);
        }
    }
    private void ConsumeWildcard(ItemStack inStack, List<GroundRecipeIngredient> wildcardIngredients, IPlayer byPlayer, ItemSlot[] inputSlots, ItemSlot fromSlot)
    {
        IEnumerable<GroundRecipeIngredient> satisfied = wildcardIngredients
            .Where(ingredient =>
            ingredient.Type == inStack.Class &&
            ingredient.ResolvedItemstack.Satisfies(inStack) &&
            inStack.StackSize >= ingredient.Quantity
            );

        if (!satisfied.Any()) return;

        GroundRecipeIngredient ingredient = satisfied.First();

        int quantity = Math.Min(ingredient.Quantity, inStack.StackSize);

        inStack.Collectible.OnConsumedByCrafting(inputSlots, fromSlot, mRecipeWorkaround, ingredient, byPlayer, quantity);

        if (ingredient.IsTool)
        {
            wildcardIngredients.Remove(ingredient);
            return;
        }

        ingredient.Quantity -= quantity;
        if (ingredient.Quantity <= 0) wildcardIngredients.Remove(ingredient);
    }

    public void GenerateOutputStack(ItemSlot[] inputSlots, ItemSlot outputSlot)
    {
        ItemStack output = outputSlot.Itemstack = Output.ResolvedItemstack.Clone();
        ItemStack? input = GetInputStackForPatternCode(inputSlots);

        if (input != null)
        {
            ITreeAttribute attr = input.Attributes.Clone();
            attr.MergeTree(output.Attributes);
            output.Attributes = attr;
        }

        outputSlot.Itemstack.Collectible.OnCreatedByCrafting(inputSlots, outputSlot, mRecipeWorkaround);
    }
    private ItemStack? GetInputStackForPatternCode(ItemSlot[] inputSlots)
    {
        GroundRecipeIngredient ingredient;

        if (CopyAttributesTrigger)
        {
            ingredient = Starter;
        }
        else if (CopyAttributesFrom.Length == 2)
        {
            ingredient = Ingredients[CopyAttributesFrom[0]][CopyAttributesFrom[1]];
        }
        else
        {
            return null;
        }

        IEnumerable<ItemStack> stacks = inputSlots.Where(slot =>
            !slot.Empty &&
            slot.Itemstack != null &&
            ingredient.SatisfiesAsIngredient(slot.Itemstack) &&
            slot.Itemstack.Collectible.MatchesForCrafting(slot.Itemstack, mRecipeWorkaround, ingredient)
            ).Select(slot => slot.Itemstack);

        if (!stacks.Any()) return null;

        return stacks.First();
    }

    #endregion

    #region Serialisation

    public void ToBytes(BinaryWriter writer)
    {
        writer.Write(Enabled);

        writer.Write(HashId);

        Output.ToBytes(writer);
        Starter.ToBytes(writer);
        writer.Write(Finisher != null);
        Finisher?.ToBytes(writer);
        writer.Write(Ingredients.Count);
        foreach (List<GroundRecipeIngredient> ingredients in Ingredients)
        {
            writer.Write(ingredients.Count);
            foreach (GroundRecipeIngredient ingredient in ingredients)
            {
                ingredient.ToBytes(writer);
            }
        }

        writer.Write(RequiresTrait ?? "");
        writer.Write(AverageDurability);
        writer.Write(ShowInCreatedBy);
        writer.Write(RecipeGroup);
        writer.Write(CopyAttributesTrigger);

        writer.Write(CopyAttributesFrom.Length);
        foreach (int item in CopyAttributesFrom)
        {
            writer.Write(item);
        }

        writer.Write(Name.ToShortString());

        writer.Write(Attributes == null);
        if (Attributes != null)
        {
            writer.Write(Attributes.Token.ToString());
        }

        writer.Write(SurfaceRequirement ?? "");
    }
    public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
    {
        Enabled = reader.ReadBoolean();

        HashId = reader.ReadInt32();

        Output = new();
        Output.FromBytes(reader, resolver);

        Starter = new();
        Starter.FromBytes(reader, resolver);

        if (reader.ReadBoolean())
        {
            Finisher = new();
            Finisher.FromBytes(reader, resolver);
        }

        Ingredients = new();
        int ingredientsCount = reader.ReadInt32();
        for (int count = 0; count < ingredientsCount; count++)
        {
            Ingredients.Add(new());
            int subIngredientsCount = reader.ReadInt32();
            for (int subCount = 0; subCount < subIngredientsCount; subCount++)
            {
                Ingredients[^1].Add(new());
                Ingredients[^1][^1].FromBytes(reader, resolver);
            }
        }

        RequiresTrait = reader.ReadString();
        RequiresTrait = RequiresTrait == "" ? null : RequiresTrait;

        AverageDurability = reader.ReadBoolean();
        ShowInCreatedBy = reader.ReadBoolean();
        RecipeGroup = reader.ReadInt32();
        CopyAttributesTrigger = reader.ReadBoolean();

        int attributesLocationCount = reader.ReadInt32();
        if (attributesLocationCount == 2)
        {
            CopyAttributesFrom = new int[]
            {
                reader.ReadInt32(),
                reader.ReadInt32()
            };
        }

        Name = new(reader.ReadString());

        if (!reader.ReadBoolean())
        {
            string json = reader.ReadString();
            Attributes = new JsonObject(JToken.Parse(json));
        }

        SurfaceRequirement = reader.ReadString();
        SurfaceRequirement = SurfaceRequirement == "" ? null : SurfaceRequirement;
    }
    public GroundRecipe Clone()
    {
        GroundRecipe recipe = new()
        {
            Enabled = Enabled,
            RequiresTrait = RequiresTrait,
            AverageDurability = AverageDurability,
            ShowInCreatedBy = ShowInCreatedBy,
            RecipeGroup = RecipeGroup,
            CopyAttributesTrigger = CopyAttributesTrigger,
            CopyAttributesFrom = (int[])CopyAttributesFrom.Clone(),
            Name = Name.Clone(),
            Starter = Starter.Clone(),
            Output = Output.Clone(),
            Finisher = Finisher?.Clone(),
            Attributes = Attributes?.Clone()
        };

        List<List<GroundRecipeIngredient>> clonedIngredients = new();

        foreach (List<GroundRecipeIngredient> ingredients in Ingredients)
        {
            clonedIngredients.Add(new());

            foreach (GroundRecipeIngredient ingredient in ingredients)
            {
                clonedIngredients[^1].Add(ingredient.Clone());
            }
        }

        recipe.Ingredients = clonedIngredients;

        return recipe;
    }

    #endregion

    public IIngredientMatcher Root(IWorldAccessor world) => Starter;
    public List<List<IIngredientMatcher>> Nodes(IWorldAccessor world) => Ingredients.Select(entry => entry.Select(entry => entry as IIngredientMatcher).ToList()).ToList();
}
