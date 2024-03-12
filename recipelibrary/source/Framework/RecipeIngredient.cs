using Newtonsoft.Json;
using RecipesLibrary.API;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace RecipesLibrary.Framework;

public class JsonRecipeIngredient
{
    /// <summary>
    /// Is it Block or Item.
    /// </summary>
    public EnumItemClass Type;
    /// <summary>
    /// Required/output amount.
    /// </summary>
    public int Quantity = 1;
    /// <summary>
    /// Required/output durability.
    /// </summary>
    public int Durability = -1;
    /// <summary>
    /// Required/attached attributes. (not supported yet for recipes input)
    /// </summary>
    [JsonProperty]
    [JsonConverter(typeof(JsonAttributesConverter))]
    public JsonObject Attributes;
    /// <summary>
    /// Item/Block code, supports wildcards.
    /// </summary>
    public string Code;
}
public class JsonRecipeInput : JsonRecipeIngredient
{
    /// <summary>
    /// Usage depends on recipe type.<br/>
    /// Used to reference ingredients in <see cref="RecipeOutput"/>.
    /// </summary>
    public string Id = "";
    /// <summary>
    /// Whether ingredient will be consumed on crafting.
    /// </summary>
    public bool Consume = true;
    /// <summary>
    /// Durability that will be spent(positive value)/added(negative value) on crafting.
    /// </summary>
    public int DurabilityCost = 0;
    /// <summary>
    /// Whether ingredient will be destroyed if it durability reaches zero.
    /// </summary>
    public bool Destroy = true;
    /// <summary>
    /// Tags that will be used to filter ingredients before looking for codes.<br/>
    /// Item/Block will be required to have at least on tag from each list of tags.
    /// </summary>
    public string[][] Tags;
    /// <summary>
    /// Use <see cref="JsonRecipeIngredient.Code"/> as regex and not wildcard.
    /// </summary>
    public bool Regex = false;
    public string[] AllowedVariants;
}
public class JsonRecipeOutput : JsonRecipeIngredient
{
    /// <summary>
    /// Takes attributes and their values from specified <see cref="RecipeInput"/>.
    /// </summary>
    public string TakeAttributesFrom = "";
    /// <summary>
    /// Stores attributes of specified <see cref="RecipeInput"/>s in attributes in array with structure:<br/><br/>
    /// <code>
    /// "ingredientsAttributes": [
    ///     {
    ///         - attributes of first ingredient in array -
    ///     },
    ///     {
    ///         - attributes of second ingredient in array -
    ///     }
    /// ]
    /// </code>
    /// </summary>
    public string[] StoreAttributesFrom = Array.Empty<string>();
    /// <summary>
    /// If true output will have durability equal to arithmetic mean of all inputs' disabilities that has them.
    /// </summary>
    public bool GetAverageDurability = false;
}

public class RecipeInput : IIngredientMatcher, IRecipeInput
{
    public string Id { get; }
    public RecipeInput(JsonRecipeInput fromJson, ITagsSystem tagsSystem)
    {
        Id = fromJson.Id;
        _type = fromJson.Type;
        _quantity = fromJson.Quantity;
        _durability = fromJson.Durability;
        _attributes = fromJson.Attributes;
        _consume = fromJson.Consume;
        _durabilityCost = fromJson.DurabilityCost;
        _destroy = fromJson.Destroy;
        if (fromJson.Tags.Length > 0) _matcher = tagsSystem.GetMatcher(fromJson.Tags);

        string code = fromJson.Code;
        if (fromJson.Regex)
        {
            Regex regex = new(code.ToString());
            _codeMatcher = codeToMatch => regex.Match(codeToMatch.ToString()).Success;
        }
        else
        {
            AssetLocation location = new(code);
            _codeMatcher = codeToMatch => WildcardUtil.Match(location, codeToMatch, fromJson.AllowedVariants);
        }
    }

    public bool Match(ItemSlot slot)
    {
        if (slot.Itemstack == null) return false;
        if (slot.Itemstack.StackSize < _quantity) return false;
        if (_type == EnumItemClass.Item && slot.Itemstack.Item == null) return false;
        if (_type == EnumItemClass.Block && slot.Itemstack.Block == null) return false;
        if (_type == EnumItemClass.Item && _durability > 0 && slot.Itemstack.Item != null && slot.Itemstack.Item.GetRemainingDurability(slot.Itemstack) < _durability) return false;
        CollectibleObject? collectible = slot.Itemstack.Collectible;
        if (collectible == null) return false;
        if (_matcher != null && !_matcher.Match(collectible)) return false;
        if (!_codeMatcher(slot.Itemstack.Collectible.Code)) return false;
        if (!MatchAttributes(slot.Itemstack)) return false;

        return true;
    }
    public void Take(IWorldAccessor world, Entity byEntity, ItemSlot slot, int count)
    {
        if (_consume)
        {
            slot.TakeOut(count * _quantity);
            return;
        }

        if (_type == EnumItemClass.Item && slot.Itemstack.Item != null && _durabilityCost > 0 && _destroy)
        {
            slot.Itemstack.Item.DamageItem(world, byEntity, slot, _durabilityCost * count);
        }

        if (_type == EnumItemClass.Item && slot.Itemstack.Item != null && _durabilityCost != 0)
        {
            int durabilityToTake = _durabilityCost * count;
            int currentDurability = slot.Itemstack.Item.GetRemainingDurability(slot.Itemstack);
            int maxDurability = slot.Itemstack.Item.GetMaxDurability(slot.Itemstack);
            int newDurability = Math.Clamp(currentDurability - durabilityToTake, 0, maxDurability);
            slot.Itemstack.Attributes.SetInt("durability", newDurability);
        }
    }
    public IEnumerable<CollectibleObject> Find(IWorldAccessor world)
    {
        if (_collectibleObjects != null) return _collectibleObjects;

        if (_matcher != null)
        {
            _collectibleObjects = _matcher.Find().OfType<CollectibleObject>().Where(collectible => _codeMatcher(collectible.Code));
            return _collectibleObjects;
        }

        if (_type == EnumItemClass.Block)
        {
            _collectibleObjects = world.Blocks.Where(block => _codeMatcher(block.Code));
            return _collectibleObjects;
        }

        _collectibleObjects = world.Items.Where(block => _codeMatcher(block.Code));
        return _collectibleObjects;
    }

    private readonly EnumItemClass _type;
    private readonly int _quantity;
    private readonly int _durability;
    private readonly int _durabilityCost;
    private readonly bool _consume;
    private readonly bool _destroy;
    private readonly ITagMatcher? _matcher;
    private readonly JsonObject _attributes;
    private readonly System.Func<AssetLocation, bool> _codeMatcher;

    private IEnumerable<CollectibleObject>? _collectibleObjects;

    private bool MatchAttributes(ItemStack stack) => true; // @TODO
}
public class RecipeOutput : RecipesLibrary.API.IRecipeOutput
{
    public RecipeOutput(JsonRecipeOutput fromJson, IWorldAccessor world, Dictionary<string, string> variants)
    {
        _type = fromJson.Type;
        _quantity = fromJson.Quantity;
        _durability = fromJson.Durability;
        _attributes = fromJson.Attributes.ToAttribute() as ITreeAttribute;
        _takeAttributesFrom = fromJson.TakeAttributesFrom;
        _storeAttributesFrom = fromJson.StoreAttributesFrom;
        _getAverageDurability = fromJson.GetAverageDurability;

        string code = fromJson.Code;
        foreach ((string variant, string value) in variants)
        {
            code = code.Replace(variant, value);
        }

        _outputCollectible = GetCollectibleObject(world, code) ?? throw new InvalidOperationException($"Unable to resolve collectible with code: '{code}'");
    }
    public ItemStack Result(Dictionary<IRecipeInput, ItemSlot> ingredients, int count)
    {
        ItemStack result = new(_outputCollectible, count * _quantity);

        if (_attributes != null)
        {
            result.Attributes = _attributes;
        }

        if (_takeAttributesFrom != "")
        {
            IEnumerable<ItemSlot> ingredientsForAttributes = ingredients.Where(entry => entry.Key.Id == _takeAttributesFrom).Select(entry => entry.Value);
            if (ingredientsForAttributes.Any())
            {
                result.Attributes.MergeTree(ingredientsForAttributes.First().Itemstack.Attributes);
            }
        }

        if (_storeAttributesFrom.Length > 0)
        {
            foreach ((IRecipeInput? input, ItemSlot slot) in ingredients)
            {
                (result.Attributes as TreeAttribute)?.SetAttribute(input.Id, slot.Itemstack?.Attributes);
            }
        }

        if (_type == EnumItemClass.Item && _getAverageDurability)
        {
            int totalDurability = 0;
            int durabilityCount = 0;
            foreach (ItemStack stack in ingredients.Select(entry => entry.Value.Itemstack).Where(stack => stack?.Item != null))
            {
                int currentDurability = stack.Item.GetRemainingDurability(stack);
                int maxDurability = stack.Item.GetMaxDurability(stack);
                if (maxDurability > 0)
                {
                    totalDurability += currentDurability;
                    durabilityCount++;
                }
            }
            totalDurability /= durabilityCount;
            result.Attributes.SetInt("durability", _durability + totalDurability);
        }
        else
        {
            if (_type == EnumItemClass.Item && _durability > 0)
            {
                result.Attributes.SetInt("durability", _durability);
            }
        }

        return result;
    }

    private static CollectibleObject? GetCollectibleObject(IWorldAccessor world, string code)
    {
        Block[] blocks = world.SearchBlocks(new(code));
        if (blocks.Length != 0) return blocks[0];

        Item[] items = world.SearchItems(new(code));
        if (items.Length != 0) return items[0];

        return null;
    }

    private readonly EnumItemClass _type;
    private readonly int _quantity;
    private readonly int _durability;
    private readonly string _takeAttributesFrom;
    private readonly string[] _storeAttributesFrom;
    private readonly bool _getAverageDurability;
    private readonly ITreeAttribute? _attributes;
    private readonly CollectibleObject _outputCollectible;
}


public class JsonPlacedRecipeInput : JsonRecipeInput
{
    public ModelTransform? PlacedTransform { get; set; }
    public AssetLocation? PlacedSound { get; set; }
}
public class PlacedRecipeInput : RecipeInput
{
    public ModelTransform PlacedTransform { get; }
    public AssetLocation? PlacedSound { get; }

    public PlacedRecipeInput(JsonPlacedRecipeInput fromJson, ITagsSystem tagsSystem) : base(fromJson, tagsSystem)
    {
        PlacedTransform = fromJson.PlacedTransform ?? ModelTransform.NoTransform;
        PlacedSound = fromJson.PlacedSound;
    }
}