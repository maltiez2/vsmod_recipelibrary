using RecipesLibrary.API;
using System.Collections.Immutable;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RecipesLibrary.Tags;

public class TagsSystem : ModSystem, ITagsSystem
{
    public TagsSystem() : base()
    {
        _manager = new TagsManager();
        TagsIntegration.Patch();
    }

    public event Action<ICoreAPI, ITagsSystem>? TagsLoaded
    {
        add
        {
            if (_loader != null) _loader.TagsLoaded += value;

        }
        remove
        {
            if (_loader != null) _loader.TagsLoaded -= value;
        }
    }

    #region Overrides
    public override void Start(ICoreAPI api)
    {
        _api = api;
        _loader = new(_manager, this, api);
    }
    public override double ExecuteOrder() => 0.21;
    public override void AssetsLoaded(ICoreAPI api)
    {
        if (api is ICoreServerAPI)
        {
            _loader?.LoadTagsOnServer();
        }
    }
    public override void Dispose()
    {
        TagsIntegration.Unpatch();
    }
    #endregion

    #region ITagsSystem
    public bool AddTags(RegistryObject registryObject, params string[] tags)
    {
        bool allSuccessful = true;
        List<Tag> tagsList = new();
        foreach (string tagString in tags)
        {
            if (!Tag.Generate(tagString, out Tag? value, out Tag? name) || value == null || name == null)
            {
                LoggerWrapper.Error(_api, this, $"(AddTags) Error on parsing tag: {tagString}");
                allSuccessful = false;
                continue;
            }

            tagsList.Add(value);
            tagsList.Add(name);
        }

        if (tagsList.Count > 0) _manager.AddTags(registryObject, tagsList.ToArray());

        return allSuccessful;
    }
    public bool RemoveTags(RegistryObject registryObject, params string[] tags)
    {
        bool allSuccessful = true;
        List<Tag> tagsList = new();
        foreach (string tagString in tags)
        {
            Tag? tag = Tag.Generate(tagString);

            if (tag == null)
            {
                LoggerWrapper.Error(_api, this, $"(RemoveTags) Error on parsing tag: {tagString}");
                allSuccessful = false;
                continue;
            }

            tagsList.Add(tag);
        }

        if (tagsList.Count > 0) _manager.RemoveTags(registryObject, tagsList.ToArray());
        return allSuccessful;
    }
    public void RemoveAll(RegistryObject registryObject) => _manager.RemoveAll(registryObject);
    public ITagMatcher GetMatcher(params string[] tags) => GetMatcher(tagsGroups: tags);
    public ITagMatcher GetMatcher(params string[][] tagsGroups)
    {
        string cacheKey = tagsGroups.Select(batch => batch.ToImmutableSortedSet().Aggregate((first, seconds) => $"{first},{seconds}")).Aggregate((first, seconds) => $"{first};{seconds}");

        if (!_matchersCache.ContainsKey(cacheKey))
        {
            TagsMatcher matcher = new(_manager, tagsGroups);
            _matchersCache.Add(cacheKey, matcher);
        }

        return _matchersCache[cacheKey];
    }
    public IEnumerable<string> GetTags(RegistryObject registryObject) => _manager.GetTags(registryObject).Select(tag => tag.ToString());
    public IEnumerable<string> GetAllTags() => _manager.GetAllTags().Select(tag => tag.ToString());
    #endregion

    internal readonly TagsManager _manager;
    private TagsLoader? _loader;
    private ICoreAPI? _api;
    private readonly Dictionary<string, TagsMatcher> _matchersCache = new();
}

internal class TagsMatcher : ITagMatcher
{
    public TagsMatcher(TagsManager manager, params string[] tags)
    {
        _manager = manager;
        IEnumerable<Tag> generated = tags.Select(tag => Tag.Generate(tag) ?? _defaultTag).Where(tag => tag != _defaultTag);
        _value = new Tag[][] { generated.ToArray() };
    }
    public TagsMatcher(TagsManager manager, params string[][] tags)
    {
        _manager = manager;
        IEnumerable<Tag[]> generated = tags.Select(batch => batch.Select(tag => Tag.Generate(tag) ?? _defaultTag).Where(tag => tag != _defaultTag).ToArray());
        _value = generated.ToArray();
    }

    public bool Match(RegistryObject registryObject) => _manager.Match(registryObject, _value);
    public IEnumerable<RegistryObject> Find() => _manager.Find(_value);

    private readonly TagsManager _manager;
    private readonly Tag[][] _value;
    private static readonly Tag _defaultTag = new("recipeslib", "none");
}