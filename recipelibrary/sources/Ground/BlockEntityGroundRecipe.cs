using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RecipeLibrary.Ground;
public class GroundRecipeEntity : BlockEntityDisplay, IBlockEntityContainer
{
    public const string ID = "Toolworks.BindSpot";
    public const float BIND_SECONDS = 3.0f;

    public object inventoryLock = new object();

    protected InventoryGeneric inventory;

    public override string InventoryClassName => "bindspot";
    public override InventoryBase Inventory
    {
        get { return inventory; }
    }
    public override string AttributeTransformCode => "bindSpotTransform";
    public override int DisplayedItems => 2;

    public GroundRecipeEntity() : base()
    {
        inventory = new InventoryGeneric(2, null, null);
    }

    public override void Initialize(ICoreAPI api)
    {
        capi = api as ICoreClientAPI;
        base.Initialize(api);

        if (capi != null)
        {
            updateMeshes();
        }

        Item
    }

    public void OnCreated(IPlayer byPlayer)
    {
        if (Api.Side == EnumAppSide.Client)
            return;

        ItemSlot heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        string placeSound = (heldSlot.Itemstack.ItemAttributes?["placeSound"].ToString()) ?? "sounds/player/build";
        heldSlot.TryPutInto(Api.World, Inventory[0], 1);
        Api.World.PlaySoundAt(new AssetLocation(placeSound), Pos.X, Pos.Y, Pos.Z, null, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);
        MarkDirty(true);
        updateMeshes();
    }

    public bool OnPlayerInteractStart(IWorldAccessor world, IPlayer player)
    {
        ItemSlot heldSlot = player.InventoryManager.ActiveHotbarSlot;
        bool shouldUpdate = false;
        if (heldSlot.Empty)
        {
            shouldUpdate = TryTakeItem(player);
        }
        else
        {
            if (AttributeToolHandle.HasAttribute(heldSlot.Itemstack))
            {
                shouldUpdate = TryPutItem(player);
            }
            else
            if (AttributeToolBinding.HasAttribute(heldSlot.Itemstack))
            {
                if (inventory[1].Empty) return false;
                return true;
            }
        }
        if (shouldUpdate)
        {
            MarkDirty(true);
            updateMeshes();
        }

        if (inventory.Empty)
        {
            world.BlockAccessor.SetBlock(0, Pos);
            world.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
        }

        return shouldUpdate;
    }

    public bool OnPlayerInteractStep(IWorldAccessor world, IPlayer byPlayer, float secondsUsed)
    {
        ItemSlot heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (heldSlot.Empty) return false;

        if (inventory[1].Empty) return false;
        if (!AttributeToolBinding.HasAttribute(heldSlot.Itemstack)) return false;

        if (world is IClientWorldAccessor)
        {
            ModelTransform tf = new ModelTransform();
            tf.EnsureDefaultValues();
            tf.Origin.Set(0f, 0f, 0f);
            tf.Translation.X -= Math.Min(1.5f, secondsUsed * 4 * 1.57f);
            tf.Rotation.Y += Math.Min(130f, secondsUsed * 350);
            byPlayer.Entity.Controls.UsingHeldItemTransformAfter = tf;
        }
        if (world.Rand.NextDouble() < 0.025)
        {
            world.PlaySoundAt(new AssetLocation("sounds/player/poultice"), Pos.X, Pos.Y, Pos.Z, byPlayer);
        }
        //(byPlayer as IClientPlayer)?.ShowChatNotification($"bind seconds {secondsUsed}");

        return secondsUsed < BIND_SECONDS || world.Side == EnumAppSide.Client;
    }

    public void OnPlayerInteractStop(IWorldAccessor world, IPlayer byPlayer, float secondsUsed)
    {
        ItemSlot heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (heldSlot.Empty) return;

        if (inventory[1].Empty) return;
        if (!AttributeToolBinding.HasAttribute(heldSlot.Itemstack)) return;

        if (secondsUsed > BIND_SECONDS - 0.05f && world.Side == EnumAppSide.Server)
        {
            ItemStack headStack = inventory[0].Itemstack;
            ItemStack bindStack = heldSlot.TakeOut(1);
            ItemStack handleStack = inventory[1].Itemstack;

            string tool = headStack.Collectible.GetBehavior<CollectibleBehaviorToolHead>().HeadProps.tool;
            string material = headStack.Collectible.Code.EndVariant();

            AssetLocation toolCode = ToolworksMod.Identify(tool)
                .WithPathAppendix("-bound")
                .WithPathAppendix("-" + material);

            Item toolItem = world.GetItem(toolCode);

            ItemStack toolStack = new(toolItem);
            toolStack.Attributes.SetItemstack(ToolPart.HEAD.ToString(), headStack);
            toolStack.Attributes.SetItemstack(ToolPart.BINDING.ToString(), bindStack);
            toolStack.Attributes.SetItemstack(ToolPart.HANDLE.ToString(), handleStack);

            if (!byPlayer.InventoryManager.TryGiveItemstack(toolStack))
            {
                world.SpawnItemEntity(toolStack, Pos.ToVec3d());
            }

            world.BlockAccessor.SetBlock(0, Pos);
            world.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
            heldSlot.MarkDirty();
        }
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
            if (!inventory[1].Empty) return false;
            if (putSlot.TryPutInto(Api.World, inventory[1]) > 0)
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
            ItemSlot takeSlot = inventory[1];
            if (takeSlot.Empty) takeSlot = inventory[0];
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
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        if (worldForResolving.Side == EnumAppSide.Client && Api != null)
        {
            updateMeshes();
        }
    }

    protected override float[][] genTransformationMatrices()
    {
        float[][] tfMatrices = new float[DisplayedItems][];

        for (int i = 0; i < tfMatrices.Length; i++)
        {
            Vec3f off = Vec3f.Zero;
            tfMatrices[i] =
                new Matrixf()
                .Translate(off.X, off.Y, off.Z)
                .Values;
        }
        return tfMatrices;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {

    }
}