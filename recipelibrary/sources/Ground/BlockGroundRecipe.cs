using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RecipeLibrary.Ground;
public class GroundRecipeBlock : Block
{
    public const string ID = "Toolworks.BlockBindSpot";

    WorldInteraction[] handleInteractions;
    WorldInteraction[] bindInteractions;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        if (api.Side != EnumAppSide.Client) return;
        ICoreClientAPI capi = api as ICoreClientAPI;

        handleInteractions = ObjectCacheUtil.GetOrCreate(api, "toolworks:bindSpotHandleInteractions", () =>
        {
            List<ItemStack> handleList = new();

            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                if (obj.Attributes?.KeyExists(AttributeToolHandle.ID) == true)
                {
                    handleList.Add(new ItemStack(obj));
                }
            }

            return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "toolworks:blockhelp-bindspot-addhandle",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = handleList.ToArray(),
                    }
                };
        });
        bindInteractions = ObjectCacheUtil.GetOrCreate(api, "toolworks:bindSpotBindInteractions", () =>
        {
            List<ItemStack> bindList = new();

            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                if (obj.Attributes?.KeyExists(AttributeToolBinding.ID) == true)
                {
                    bindList.Add(new ItemStack(obj));
                }
            }

            return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "toolworks:blockhelp-bindspot-bind",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = bindList.ToArray(),
                    }
                };
        });
    }

    public bool TryCreateSpot(IWorldAccessor world, BlockSelection blockSel, IPlayer player)
    {
        if (!world.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
        {
            player.InventoryManager.ActiveHotbarSlot.MarkDirty();
            return false;
        }

        if (blockSel.Face != BlockFacing.UP) return false;
        Block onBlock = world.BlockAccessor.GetBlock(blockSel.Position);

        if (!onBlock.CanAttachBlockAt(world.BlockAccessor, this, blockSel.Position, BlockFacing.UP))
            return false;

        BlockPos upPos = blockSel.Position.AddCopy(blockSel.Face);
        if (world.BlockAccessor.GetBlock(upPos).Replaceable < 6000) return false;

        world.BlockAccessor.SetBlock(BlockId, upPos);
        BlockEntity be = world.BlockAccessor.GetBlockEntity(upPos);
        (be as GroundRecipeEntity)?.OnCreated(player);

        (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
        {
            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            return false;
        }
        GroundRecipeEntity be = world.BlockAccessor.GetBlockEntity<GroundRecipeEntity>(blockSel.Position);
        if (be == null)
        {
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        return be.OnPlayerInteractStart(world, byPlayer);
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
        {
            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            return false;
        }
        GroundRecipeEntity be = world.BlockAccessor.GetBlockEntity<GroundRecipeEntity>(blockSel.Position);
        if (be == null)
        {
            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
        }
        return be.OnPlayerInteractStep(world, byPlayer, secondsUsed);
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
        {
            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            return;
        }
        GroundRecipeEntity be = world.BlockAccessor.GetBlockEntity<GroundRecipeEntity>(blockSel.Position);
        if (be == null)
        {
            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
            return;
        }
        be.OnPlayerInteractStop(world, byPlayer, secondsUsed);
    }

    public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
    {
        GroundRecipeEntity? be = world.BlockAccessor.GetBlockEntity(pos) as GroundRecipeEntity;
        if (be == null) return base.GetPlacedBlockName(world, pos);

        return be.Inventory[0].GetStackName();
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        WorldInteraction[] interactions = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        GroundRecipeEntity? be = world.BlockAccessor.GetBlockEntity(selection.Position) as GroundRecipeEntity;
        if (be == null) return interactions;

        if (be.Inventory[1].Empty)
        {
            return handleInteractions.Append(interactions);
        }
        else
        {
            return bindInteractions.Append(interactions);
        }
    }
}