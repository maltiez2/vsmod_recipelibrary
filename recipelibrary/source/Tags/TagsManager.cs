using System.Text.RegularExpressions;
using Vintagestory.API.Common;

namespace RecipesLibrary.Tags;

internal sealed class TagsManager
{
    #region Register
    public void AddTags(RegistryObject registryObject, params Tag[] tags)
    {
        foreach (Tag tag in tags.Where(tag => !_tags.Contains(tag)))
        {
            _tags.Add(tag);
            _tagsToId.Add(tag, _tagsById.Count);
            _tagsById.Add(tag);
        }

        if (!_tagsValues.ContainsKey(registryObject))
        {
            _tagsValues.Add(registryObject, new());
        }

        foreach (Tag tag in tags)
        {
            _tagsValues[registryObject].Add(_tagsToId[tag]);
        }
    }
    public void RemoveTags(RegistryObject registryObject, params Tag[] tags)
    {
        if (!_tagsValues.ContainsKey(registryObject)) return;

        foreach (Tag tag in tags.Where(_tagsToId.ContainsKey))
        {
            _tagsValues[registryObject].Remove(_tagsToId[tag]);
        }

        if (_tagsValues[registryObject].Count == 0) _tagsValues.Remove(registryObject);
    }
    public void RemoveAll(RegistryObject registryObject)
    {
        _tagsValues.Remove(registryObject);
    }
    public IEnumerable<Tag> GetTags(RegistryObject registryObject)
    {
        if (!_tagsValues.ContainsKey(registryObject)) return Enumerable.Empty<Tag>();

        return _tagsValues[registryObject].Select(tag => _tagsById[tag]);
    }
    public IEnumerable<Tag> GetAllTags() => _tagsById;
    #endregion

    #region Match
    public bool MatchAll(RegistryObject registryObject, Tag[] tags)
    {
        if (!_tagsValues.ContainsKey(registryObject)) return false;

        foreach (Tag tag in tags.Where(_tagsToId.ContainsKey))
        {
            if (!_tagsValues[registryObject].Contains(_tagsToId[tag])) return false;
        }

        return true;
    }
    public bool MatchAny(RegistryObject registryObject, Tag[] tags)
    {
        if (!_tagsValues.ContainsKey(registryObject)) return false;

        foreach (Tag tag in tags.Where(_tagsToId.ContainsKey))
        {
            if (_tagsValues[registryObject].Contains(_tagsToId[tag])) return true;
        }

        return false;
    }
    public IEnumerable<RegistryObject> FindAll(Tag[] tags) => _tagsValues.Where(entry => MatchAll(entry.Key, tags)).Select(entry => entry.Key);
    public IEnumerable<RegistryObject> FindAny(Tag[] tags) => _tagsValues.Where(entry => MatchAny(entry.Key, tags)).Select(entry => entry.Key);
    #endregion

    private readonly List<Tag> _tagsById = new();
    private readonly Dictionary<Tag, int> _tagsToId = new();
    private readonly Dictionary<RegistryObject, HashSet<int>> _tagsValues = new();
    private readonly HashSet<Tag> _tags = new();
}

internal sealed partial class Tag
{
    public int Hash { get; }
    public string Domain { get; }
    public string Name { get; }
    public string Value { get; }

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
    public static bool Generate(string tag, out Tag? tagValue, out Tag? tagName)
    {
        Match match = TagRegex().Match(tag);
        tagValue = null;
        tagName = null;
        if (!match.Success) return false;

        string domain = match.Groups[1].Value;
        string name = match.Groups[2].Value;
        string value = match.Groups[3].Success ? match.Groups[4].Value : "";

        tagValue = new(domain, name, value);
        tagName = new(domain, name);

        return true;
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
    public static Tag GetDefault()
    {
        _default ??= new("recipeslib", "none");
        return _default;
    }
    public override string ToString() => Value == "" ? $"{Domain}:{Name}" : $"{Domain}:{Name}/{Value}";

    [GeneratedRegex("^([a-z\\-]+):([a-z\\-]+)(/(.*))?$")]
    private static partial Regex TagRegex();
    private static Tag? _default = null;
}