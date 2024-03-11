using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;

namespace RecipesLibrary.Tags;


public interface ITagMatcher
{
    bool Match(RegistryObject registryObject);
}

internal class TagsMatcher : ITagMatcher
{
    public TagsMatcher(ICoreAPI api, params string[] tags)
    {
        _manager = api.ModLoader.GetModSystem<TagsSystem>()._manager;
        IEnumerable<Tag> generated = tags.Select(tag => Tag.Generate(tag) ?? _defaultTag).Where(tag => tag != _defaultTag);
        _value = new(_manager, generated.ToHashSet());
    }
    public TagsMatcher(TagsManager manager, HashSet<Tag> tags)
    {
        _manager = manager;
        _value = new(_manager, tags);
    }

    public bool Match(RegistryObject registryObject) => _manager.MatchAny(registryObject, _value);
    public IEnumerable<RegistryObject> Find() => _manager.FindAny(_value);

    private readonly TagsManager _manager;
    private readonly TagsValues _value;
    private static readonly Tag _defaultTag = new("recipeslib", "none");
}

public class TagsSystem : ModSystem
{
    public TagsSystem() : base()
    {
        LibraryTagsRegistry.ConstructMapping();
        _manager = new TagsManager();
    }

    public override void Start(ICoreAPI api)
    {
        _api = api;
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        _manager.Construct();
    }

    public bool RegisterTags(params string[] tags)
    {
        bool allSuccessful = true;
        foreach (string tagString in tags)
        {
            Tag? tag = Tag.Generate(tagString);

            if (tag == null)
            {
                LoggerWrapper.Error(_api, this, $"(RegisterTags) Error on parsing tag: {tagString}");
                allSuccessful = false;
                continue;
            }

            _manager.RegisterTags(tag);
        }
        return allSuccessful;
    }
    public bool AddTags(RegistryObject registryObject, params string[] tags)
    {
        bool allSuccessful = true;
        foreach (string tagString in tags)
        {
            Tag? tag = Tag.Generate(tagString);

            if (tag == null)
            {
                LoggerWrapper.Error(_api, this, $"(AddTags) Error on parsing tag: {tagString}");
                allSuccessful = false;
                continue;
            }

            _manager.AddTags(registryObject, tag);
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


    internal readonly TagsManager _manager;
    private ICoreAPI? _api;
}


internal sealed class TagsManager
{
    public TagsManager()
    {

    }

    public void RegisterTags(params Tag[] tags)
    {
        foreach (Tag tag in tags)
        {
            _usedTags.Add(tag);
        }
    }
    public void AddTags(RegistryObject registryObject, params Tag[] tags)
    {
        if (!_tagsToAdd.ContainsKey(registryObject)) _tagsToAdd.Add(registryObject, new());

        foreach (Tag tag in tags)
        {
            _tagsToAdd[registryObject].Add(tag);
            _usedTags.Add(tag);
        }
    }
    public void RemoveTags(RegistryObject registryObject, params Tag[] tags)
    {
        if (!_tagsToAdd.ContainsKey(registryObject)) return;

        foreach (Tag tag in tags)
        {
            _tagsToAdd[registryObject].Remove(tag);
        }
    }
    public bool MatchAll(RegistryObject registryObject, TagsValues tags)
    {
        if (!_tagsValues.ContainsKey(registryObject)) return false;

        return _tagsValues[registryObject].MatchAll(tags);
    }
    public bool MatchAny(RegistryObject registryObject, TagsValues tags)
    {
        if (!_tagsValues.ContainsKey(registryObject)) return false;

        return _tagsValues[registryObject].MatchAny(tags);
    }
    public IEnumerable<RegistryObject> FindAll(TagsValues tags) => _tagsValues.Where(entry => entry.Value.MatchAll(tags)).Select(entry => entry.Key);
    public IEnumerable<RegistryObject> FindAny(TagsValues tags) => _tagsValues.Where(entry => entry.Value.MatchAny(tags)).Select(entry => entry.Key);

    internal bool Construct()
    {
        if (_constructed) return false;

        _customTags.Clear();
        foreach (Tag tag in _usedTags.Where(tag => !LibraryTagsRegistry._libraryTagsMapping.ContainsKey(tag)))
        {
            _customTagsMapping.Add(tag, _customTags.Count);
            _customTags.Add(tag);
        }

        foreach ((RegistryObject registryObject, HashSet<Tag> tags) in _tagsToAdd)
        {
            _tagsValues.Add(registryObject, new(this, tags));
        }

        _constructed = true;
        return true;
    }

    internal readonly List<Tag> _customTags = new();
    internal readonly Dictionary<Tag, int> _customTagsMapping = new();
    internal readonly Dictionary<RegistryObject, TagsValues> _tagsValues = new();

    private bool _constructed = false;
    private readonly Dictionary<RegistryObject, HashSet<Tag>> _tagsToAdd = new();
    private readonly HashSet<Tag> _usedTags = new();
}

public sealed partial class Tag
{
    public int Hash { get; }
    public string Domain { get; }
    public string Name { get; }
    public string Value { get; }

    [GeneratedRegex("^([a-z\\-]+):([a-z\\-]+)(/(.*))?$")]
    private static partial Regex TagRegex();

    public Tag(string domain, string name, string value = "")
    {
        Domain = domain;
        Name = name;
        Value = value;
        Hash = $"{domain}:{name}/{value}".GetHashCode();
    }

    public static Tag? Generate(string tag)
    {
        Match match = TagRegex().Match(tag);
        if (!match.Success) return null;

        string domain = match.Groups[1].Value;
        string name = match.Groups[2].Value;
        string value = match.Groups[3].Success ? match.Groups[4].Value : "";

        return new(domain, name, value);
    }

    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetHashCode() != Hash || obj is not Tag tag)
        {
            return false;
        }

        return tag.Domain == Domain && tag.Name == Name && tag.Value == Value;
    }

    public override int GetHashCode() => Hash;
}

internal readonly struct TagsValues
{
    public TagsValues(TagsManager manager, HashSet<Tag> tags)
    {
        _tagsManager = manager;

        foreach (LibraryTagsValue tag in tags.Where(LibraryTagsRegistry._libraryTagsMapping.ContainsKey).Select(tag => LibraryTagsRegistry._libraryTagsMapping[tag]))
        {
            _libraryTags = LibraryTagsValue.Or(_libraryTags, tag);
        }

        List<int> customTags = new();
        foreach (int tag in tags.Where(manager._customTagsMapping.ContainsKey).Select(tag => manager._customTagsMapping[tag]))
        {
            _customTags.Add(tag);
            customTags.Add(tag);
        }

        customTags.Sort();

        StringBuilder forHash = new();
        forHash.Append(_libraryTags.Hash);
        foreach (int tag in customTags)
        {
            forHash.Append($"|{tag}");
        }

        _hash = forHash.ToString().GetHashCode();
    }
    public bool MatchAll(TagsValues value)
    {
        if (!_libraryTags.MatchAll(value._libraryTags)) return false;

        if (value._customTags.Count == 0) return true;

        if (_customTags.Count < value._customTags.Count) return false;

        foreach (int tag in value._customTags)
        {
            if (!_customTags.Contains(tag)) return false;
        }

        return true;
    }
    public bool MatchAny(TagsValues value)
    {
        if (!_libraryTags.MatchAny(value._libraryTags)) return false;

        if (value._customTags.Count == 0) return true;

        bool foundAny = false;

        foreach (int tag in value._customTags)
        {
            if (_customTags.Contains(tag))
            {
                foundAny = true;
                break;
            }
        }

        return foundAny;
    }
    public bool Match(Tag tag)
    {
        if (LibraryTagsRegistry._libraryTagsMapping.ContainsKey(tag))
        {
            return _libraryTags.MatchAny(LibraryTagsRegistry._libraryTagsMapping[tag]);
        }

        if (_tagsManager._customTagsMapping.ContainsKey(tag))
        {
            return _customTags.Contains(_tagsManager._customTagsMapping[tag]);
        }

        return false;
    }

    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetHashCode() != _hash || obj is not TagsValues tag)
        {
            return false;
        }

        if (!tag._libraryTags.Equals(_libraryTags)) return false;

        if (tag._customTags.Count != _customTags.Count) return false;

        foreach (int item in _customTags)
        {
            if (!tag._customTags.Contains(item)) return false;
        }

        return true;
    }
    public override readonly int GetHashCode() => _hash;
    public static bool operator ==(TagsValues left, TagsValues right)
    {
        return left.Equals(right);
    }
    public static bool operator !=(TagsValues left, TagsValues right)
    {
        return !(left == right);
    }

    private readonly LibraryTagsValue _libraryTags = new();
    private readonly HashSet<int> _customTags = new();
    private readonly TagsManager _tagsManager;
    private readonly int _hash;
}
