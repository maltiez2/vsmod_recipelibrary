using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace RecipesLibrary.Ground;
public class GroundRecipeEntity : BlockEntityDisplay, IBlockEntityContainer
{
    public IEnumerable<GroundRecipe> Recipes { get; set; } = new List<GroundRecipe>();

    public override string InventoryClassName => "groundrecipespot";
    public override InventoryBase Inventory => mInventory;
    public override int DisplayedItems => mSlotsTaken;

    private InventoryGeneric? mInventory;
    private int mMaxSlots = 0;
    private int mSlotsTaken = 0;
    private ICoreAPI? mApi;
    private IRecipeGraphTraversalStack? mTraversalStack;
    private readonly List<GroundRecipeIngredient> mIngredients = new();
    private readonly object inventoryLock = new();

    public GroundRecipeEntity() : base()
    {

    }

    public override void Initialize(ICoreAPI api)
    {
        mApi = api;
        capi = api as ICoreClientAPI;
        base.Initialize(api);

        if (capi != null)
        {
            updateMeshes();
        }
    }

    public void OnCreated(IPlayer byPlayer, IEnumerable<GroundRecipe> recipes)
    {
        Recipes = recipes;
        mMaxSlots = GetDepth();
        mInventory = new InventoryGeneric(mMaxSlots, null, null);
        mTraversalStack = new RecipeGraphTraversalStack(recipes.Select(recipe => recipe.Graph).OfType<IRecipeGraph>());

        if (Api.Side == EnumAppSide.Client) return;

        ItemSlot heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        string placeSound = (heldSlot.Itemstack.ItemAttributes?["placeSound"].ToString()) ?? "sounds/player/build";
        heldSlot.TryPutInto(Api.World, Inventory[0], 1);
        Api.World.PlaySoundAt(new AssetLocation(placeSound), Pos.X, Pos.Y, Pos.Z, null, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);
        MarkDirty(true);
        updateMeshes();
    }

    public bool OnPlayerInteractStart(IWorldAccessor world, IPlayer player)
    {
        if (mTraversalStack == null) return false;
        
        ItemSlot heldSlot = player.InventoryManager.ActiveHotbarSlot;
        bool shouldUpdate;
        
        if (heldSlot.Empty)
        {
            shouldUpdate = TryTakeItem(player);
        }
        else
        {
            RecipeStackStatus status = mTraversalStack.Push(heldSlot);

            if (status == RecipeStackStatus.Empty || status == RecipeStackStatus.Unmatched)
            {
                mTraversalStack.Pop();
                return false;
            }

            shouldUpdate = TryPutItem(player);

            if (!shouldUpdate) mTraversalStack.Pop();

        }
        
        if (shouldUpdate)
        {
            ConstructIngredients();
            MarkDirty(true);
            updateMeshes();
        }

        if (mInventory?.Empty != false)
        {
            world.BlockAccessor.SetBlock(0, Pos);
            world.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
        }

        return shouldUpdate;
    }

    public bool OnPlayerInteractStep(IWorldAccessor world, IPlayer byPlayer, float secondsUsed)
    {
        return false;
    }

    public void OnPlayerInteractStop(IWorldAccessor world, IPlayer byPlayer, float secondsUsed)
    {
        
    }

    public bool TryPutItem(IPlayer player)
    {
        if (Api.Side == EnumAppSide.Client)
        {
            (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            return true;
        }

        ItemSlot putSlot = player.InventoryManager.ActiveHotbarSlot;
        string placeSound = (putSlot.Itemstack.ItemAttributes?["placeSound"].ToString()) ?? "sounds/player/build";
        lock (inventoryLock)
        {
            if (!mInventory[1].Empty) return false;
            if (putSlot.TryPutInto(Api.World, mInventory[1]) > 0)
            {
                Api.World.PlaySoundAt(new AssetLocation(placeSound), Pos.X, Pos.Y, Pos.Z, null, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);
            }
        }
        return true;
    }

    public bool TryTakeItem(IPlayer player)
    {
        if (Api.Side == EnumAppSide.Client)
        {
            (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            return true;
        }

        lock (inventoryLock)
        {
            ItemSlot takeSlot = mInventory[1];
            if (takeSlot.Empty) takeSlot = mInventory[0];
            string placeSound = (takeSlot.Itemstack.ItemAttributes?["placeSound"].ToString()) ?? "sounds/player/build";
            player.InventoryManager.TryGiveItemstack(takeSlot.Itemstack);

            Api.World.PlaySoundAt(new AssetLocation(placeSound), Pos.X, Pos.Y, Pos.Z, null, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);
            takeSlot.Itemstack = null;
            takeSlot.MarkDirty();
        }
        return true;
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        int[] recipesIds = Recipes.Select(recipe => recipe.HashId).ToArray();

        IAttribute recipesAttribute = new IntArrayAttribute(recipesIds);
        tree["recipes"] = recipesAttribute;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        if (worldForResolving.Side == EnumAppSide.Client && Api != null)
        {
            updateMeshes();
        }

        Dictionary<int, GroundRecipe>? loadedRecipes = mApi?.ModLoader.GetModSystem<GroundRecipeLoader>().RecipesByHashId;

        if (loadedRecipes == null) return;

        int[]? recipesIds = (tree["recipes"] as IntArrayAttribute)?.value;

        if (recipesIds == null) return;

        List<GroundRecipe> recipes = new();
        foreach (int hashId in recipesIds.Where(loadedRecipes.ContainsKey))
        {
            recipes.Add(loadedRecipes[hashId]);
        }
        Recipes = recipes;

        mTraversalStack = new RecipeGraphTraversalStack(recipes.Select(recipe => recipe.Graph).OfType<IRecipeGraph>());
        foreach (ItemSlot slot in Inventory)
        {
            if (slot.Itemstack == null) continue;

            mTraversalStack.Push(slot);
            mSlotsTaken++;
        }
        mMaxSlots = Inventory.Count;
    }

    protected override float[][] genTransformationMatrices()
    {
        float[][] transformMatrices = new float[DisplayedItems][];

        ModelTransform[] transforms = mIngredients.Select(item => item.GroundRecipeTransform ?? ModelTransform.NoTransform).Reverse().ToArray();

        for (int index = 0; index < transformMatrices.Length || index < transforms.Length; index++)
        {
            transformMatrices[index] = new Matrixf()
                .Translate(transforms[index].Origin.X, transforms[index].Origin.Y, transforms[index].Origin.Z)
                .Scale(transforms[index].ScaleXYZ.X, transforms[index].ScaleXYZ.Y, transforms[index].ScaleXYZ.Z)
                .Translate(transforms[index].Translation.X, transforms[index].Translation.Y, transforms[index].Translation.Z)
                .RotateX(transforms[index].Rotation.X * (MathF.PI / 180f))
                .RotateY(transforms[index].Rotation.Y * (MathF.PI / 180f))
                .RotateZ(transforms[index].Rotation.Z * (MathF.PI / 180f))
                .Values;
        }
        return transformMatrices;
    }


    private void ConstructIngredients()
    {
        IEnumerable<RecipeStackNode>? nodes = mTraversalStack?.Nodes(RecipeStackStatus.Completed);
        if (nodes == null || !nodes.Any()) nodes = mTraversalStack?.Nodes(RecipeStackStatus.Matched);
        mIngredients.Clear();
        if (nodes == null || !nodes.Any()) return;
        AddIngredient(nodes.First());
    }
    private void AddIngredient(RecipeStackNode node)
    {
        GroundRecipeIngredient? ingredient = node.Nodes?.First().Matcher as GroundRecipeIngredient;
        if (ingredient == null) return;
        mIngredients.Add(ingredient);
        if (node.Parent == null) return;
        AddIngredient(node.Parent);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {

    }

    private int GetDepth()
    {
        int maxDepth = 0;
        foreach (GroundRecipe recipe in Recipes)
        {
            int depth = recipe.Graph?.Depth ?? 0;
            if (depth > maxDepth) maxDepth = depth;
        }
        return maxDepth;
    }
}