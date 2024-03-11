using RecipesLibrary.API;
using System.Collections.Immutable;
using Vintagestory.API.Common;

namespace RecipesLibrary.Tags;

public class TagsSystem : ModSystem, ITagsSystem
{
    public TagsSystem() : base()
    {
        _manager = new TagsManager();
    }

    public override void Start(ICoreAPI api)
    {
        _api = api;
    }

    public bool AddTags(RegistryObject registryObject, params string[] tags)
    {
        bool allSuccessful = true;
        foreach (string tagString in tags)
        {
            if (!Tag.Generate(tagString, out Tag? value, out Tag? name) || value == null || name == null)
            {
                LoggerWrapper.Error(_api, this, $"(AddTags) Error on parsing tag: {tagString}");
                allSuccessful = false;
                continue;
            }

            _manager.AddTags(registryObject, name);
            _manager.AddTags(registryObject, value);
        }
        return allSuccessful;
    }
    public bool RemoveTags(RegistryObject registryObject, params string[] tags)
    {
        bool allSuccessful = true;
        foreach (string tagString in tags)
        {
            Tag? tag = Tag.Generate(tagString);

            if (tag == null)
            {
                LoggerWrapper.Error(_api, this, $"(RemoveTags) Error on parsing tag: {tagString}");
                allSuccessful = false;
                continue;
            }

            _manager.RemoveTags(registryObject, tag);
        }
        return allSuccessful;
    }
    public void RemoveAll(RegistryObject registryObject) => _manager.RemoveAll(registryObject);
    public ITagMatcher GetMatcher(params string[] tags)
    {
        string cacheKey = tags.ToImmutableSortedSet().Aggregate((first, seconds) => $"{first},{seconds}");

        if (!_matchersCache.ContainsKey(cacheKey))
        {
            TagsMatcher matcher = new(_manager, tags.Select(tag => Tag.Generate(tag) ?? Tag.GetDefault()).Where(tag => tag != Tag.GetDefault()).ToArray());
            _matchersCache.Add(cacheKey, matcher);
        }

        return _matchersCache[cacheKey];
    }


    internal readonly TagsManager _manager;
    private ICoreAPI? _api;
    private readonly Dictionary<string, TagsMatcher> _matchersCache = new();
}

internal class TagsMatcher : ITagMatcher
{
    public TagsMatcher(ICoreAPI api, params string[] tags)
    {
        _manager = api.ModLoader.GetModSystem<TagsSystem>()._manager;
        IEnumerable<Tag> generated = tags.Select(tag => Tag.Generate(tag) ?? _defaultTag).Where(tag => tag != _defaultTag);
        _value = generated.ToArray();
    }
    public TagsMatcher(TagsManager manager, params Tag[] tags)
    {
        _manager = manager;
        _value = tags;
    }

    public bool Match(RegistryObject registryObject) => _manager.MatchAny(registryObject, _value);
    public IEnumerable<RegistryObject> Find() => _manager.FindAny(_value);

    private readonly TagsManager _manager;
    private readonly Tag[] _value;
    private static readonly Tag _defaultTag = new("recipeslib", "none");
}