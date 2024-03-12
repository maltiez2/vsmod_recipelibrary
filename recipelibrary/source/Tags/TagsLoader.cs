using Newtonsoft.Json.Linq;
using RecipesLibrary.API;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.Common;

namespace RecipesLibrary.Tags;

internal class TagsLoader
{
    public TagsLoader(TagsManager manager, ITagsSystem system, ICoreAPI api)
    {
        _api = api;
        _system = system;
        _manager = manager;
        _ = _api.RegisterRecipeRegistry<TagsRegistry>(_tagsRegistryCode);
    }

    public event Action<ICoreAPI, ITagsSystem>? TagsLoaded;

    public static TagsLoader? Instance;
    public void LoadTagsOnClient()
    {
        TagsRegistry? registry = GetRegistry(_api);

        if (registry == null)
        {
            _api.Logger.Warning("[Recipes lib] Unable to retrieve tags registry");
            return;
        }

        registry.Deserialize(_api, _manager);
        TagsLoaded?.Invoke(_api, _system);
    }
    public void LoadTagsOnServer()
    {
        LoadTagsFromAssets(_api);
        GetRegistry(_api)?.Serialize(_manager);
        TagsLoaded?.Invoke(_api, _system);
    }


    private const string _tagsRegistryCode = "recipeslib-tags";
    private readonly ICoreAPI _api;
    private readonly ITagsSystem _system;
    private readonly TagsManager _manager;

    private void LoadTagsFromAssets(ICoreAPI api)
    {
        List<IAsset> assets = api.Assets.GetManyInCategory("config", "recipeslib-patches.json");
        Dictionary<string, HashSet<string>> tagsByObjectCode = new();

        foreach (IAsset asset in assets)
        {
            byte[] data = asset.Data;
            string json = System.Text.Encoding.UTF8.GetString(data);
            JObject token = JObject.Parse(json);

            LoadTagsFromPatchesFile(token, tagsByObjectCode);
        }

        ResolveCodesAndTags(api, tagsByObjectCode, out Dictionary<RegistryObject, Tag[]> tagsBuRegistryObject);

        foreach ((RegistryObject registryObject, Tag[] tags) in tagsBuRegistryObject)
        {
            _manager.AddTags(registryObject, tags);
        }
    }
    private static void LoadTagsFromPatchesFile(JObject file, Dictionary<string, HashSet<string>> tagsByObjectCode)
    {
        foreach ((string code, JToken? tagsToken) in file)
        {
            if (tagsToken is not JArray tags) continue;
            if (!tagsByObjectCode.ContainsKey(code)) tagsByObjectCode.Add(code, new());

            foreach (string tag in tags.Select(token => new JsonObject(token)).Select(token => token.AsString("")).Where(tag => tag != ""))
            {
                tagsByObjectCode[code].Add(tag);
            }
        }
    }
    private static void ResolveCodesAndTags(ICoreAPI api, Dictionary<string, HashSet<string>> tagsByObjectCode, out Dictionary<RegistryObject, Tag[]> tagsBuRegistryObject)
    {
        tagsBuRegistryObject = new();
        foreach ((string code, HashSet<string> tags) in tagsByObjectCode)
        {
            RegistryObject? registryObject = GetRegistryObject(api, code);
            if (registryObject == null) continue;

            Tag[] resolvedTags = tags.Select(tag => Tag.Generate(tag) ?? Tag.GetDefault()).ToArray();
            if (resolvedTags.Length == 0) continue;

            tagsBuRegistryObject.Add(registryObject, resolvedTags);
        }
    }
    private static TagsRegistry? GetRegistry(ICoreAPI api)
    {
        MethodInfo? getter = typeof(GameMain).GetMethod("GetRecipeRegistry", BindingFlags.Instance | BindingFlags.NonPublic);
        return (TagsRegistry?)getter?.Invoke(api.World, new object[] { _tagsRegistryCode });
    }
    private static RegistryObject? GetRegistryObject(ICoreAPI api, string code)
    {
        Block[] blocks = api.World.SearchBlocks(new(code));
        if (blocks.Length != 0) return blocks[0];

        Item[] items = api.World.SearchItems(new(code));
        if (items.Length != 0) return items[0];

        return null;
    }
}

internal class TagsRegistry : RecipeRegistryBase
{
    public byte[]? Data { get; set; }

    public override void FromBytes(IWorldAccessor resolver, int quantity, byte[] data)
    {
        Data = data;
        TagsLoader.Instance?.LoadTagsOnClient();
    }
    public override void ToBytes(IWorldAccessor resolver, out byte[] data, out int quantity)
    {
        data = Data ?? Array.Empty<byte>();
        quantity = 1;
    }

    public void Serialize(TagsManager manager)
    {
        Data = manager.ToBytes();
    }
    public void Deserialize(ICoreAPI api, TagsManager manager)
    {
        if (Data == null) return;

        manager.FromBytes(api, Data);
    }
}