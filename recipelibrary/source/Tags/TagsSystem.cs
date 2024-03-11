using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;

namespace RecipesLibrary.Tags;

public sealed class TagsSystem
{
    public readonly List<Tag> CustomTags = new();
    public readonly Dictionary<Tag, int> CustomTagsMapping = new();
    public readonly Dictionary<RegistryObject, TagValues> TagsValues = new();
    public readonly Dictionary<Tag, FastTagsValues> FastTagsMapping = new()
    {
        {  new Tag("recipeslib", "type", "block"), new FastTagsValues(FastTags.Type, type: FastTag_Type.Block) },
        {  new Tag("recipeslib", "type", "item"), new FastTagsValues(FastTags.Type, type: FastTag_Type.Item) },
        {  new Tag("recipeslib", "type", "entity"), new FastTagsValues(FastTags.Type, type: FastTag_Type.Entity) }
    };
}

public sealed partial class Tag
{
    public int Hash { get; }
    public string Domain { get; }
    public string Name { get; }
    public string Value { get; }

    [GeneratedRegex("^([\\w_\\-]+):([\\w_\\-]+)(/(.*))?$")]
    private static partial Regex TagRegex();

    public Tag(string domain, string name, string value)
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

public readonly struct TagValues
{
    public TagValues(TagsSystem system, HashSet<Tag> tags)
    {
        foreach (FastTagsValues tag in tags.Where(system.FastTagsMapping.ContainsKey).Select(tag => system.FastTagsMapping[tag]))
        {
            _fastTags = FastTagsValues.Or(_fastTags, tag);
        }

        List<int> customTags = new();
        foreach (int tag in tags.Where(system.CustomTagsMapping.ContainsKey).Select(tag => system.CustomTagsMapping[tag]))
        {
            _customTags.Add(tag);
            customTags.Add(tag);
        }

        customTags.Sort();

        StringBuilder forHash = new();
        forHash.Append(_fastTags.Hash);
        foreach (int tag in customTags)
        {
            forHash.Append($"|{tag}");
        }

        _hash = forHash.ToString().GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetHashCode() != _hash || obj is not TagValues tag)
        {
            return false;
        }

        if (!tag._fastTags.Equals(_fastTags)) return false;

        if (tag._customTags.Count != _customTags.Count) return false;

        foreach (int item in _customTags)
        {
            if (!tag._customTags.Contains(item)) return false;
        }

        return true;
    }
    public override readonly int GetHashCode() => _hash;
    public static bool operator ==(TagValues left, TagValues right)
    {
        return left.Equals(right);
    }
    public static bool operator !=(TagValues left, TagValues right)
    {
        return !(left == right);
    }

    private readonly FastTagsValues _fastTags = new();
    private readonly HashSet<int> _customTags = new();
    private readonly int _hash;
}

public readonly struct FastTagsValues
{
    public readonly int Hash;
    public readonly FastTags Tags;
    public readonly FastTag_Type Type;
    public readonly FastTag_Metal Metal;

    public FastTagsValues(
        FastTags tags = FastTags.None,
        FastTag_Type type = FastTag_Type.None,
        FastTag_Metal metal = FastTag_Metal.None
        )
    {
        Tags = tags;
        Type = type;
        Metal = metal;
        Hash = $"{tags}|{type}|{metal}".GetHashCode();
    }

    public static FastTagsValues Or(FastTagsValues first, FastTagsValues second)
    {
        return new(
            first.Tags | second.Tags,
            first.Type | second.Type,
            first.Metal | second.Metal
            );
    }
    public static FastTagsValues And(FastTagsValues first, FastTagsValues second)
    {
        return new(
            first.Tags & second.Tags,
            first.Type & second.Type,
            first.Metal & second.Metal
            );
    }
    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetHashCode() != Hash || obj is not FastTagsValues values)
        {
            return false;
        }

        return values.Tags == Tags && values.Type == Type && values.Metal == Metal;
    }
    public override int GetHashCode() => Hash;
    public static bool operator ==(FastTagsValues left, FastTagsValues right)
    {
        return left.Equals(right);
    }
    public static bool operator !=(FastTagsValues left, FastTagsValues right)
    {
        return !(left == right);
    }
}


[Flags]
public enum FastTags : long
{
    None = 0b0000_0000_0000_0000_0000_0000_0000_0000,
    Type = 0b0000_0000_0000_0000_0000_0000_0000_0001,
    Metal = 0b0000_0000_0000_0000_0000_0000_0000_0010,
    Wood = 0b0000_0000_0000_0000_0000_0000_0000_0100,
    Cloth = 0b0000_0000_0000_0000_0000_0000_0000_1000,
    Stone = 0b0000_0000_0000_0000_0000_0000_0001_0000,
}

[Flags]
public enum FastTag_Type : byte
{
    None = 0b0000_0000,
    Block = 0b0000_0001,
    Item = 0b0000_0010,
    Entity = 0b0000_0100,
}

[Flags]
public enum FastTag_Metal : ulong
{
    None = 0b0000_0000_0000_0000_0000_0000_0000_0000,
    Modded = 0b1000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000,

    Copper = 0b0000_0000_0000_0000_0000_0000_0000_0001,
    TinBronze = 0b0000_0000_0000_0000_0000_0000_0000_0010,
    BismuthBronze = 0b0000_0000_0000_0000_0000_0000_0000_0100,
    BlackBronze = 0b0000_0000_0000_0000_0000_0000_0000_1000,
    Iron = 0b0000_0000_0000_0000_0000_0000_0001_0000,
    MeteoricIron = 0b0000_0000_0000_0000_0000_0000_0010_0000,
    Steel = 0b0000_0000_0000_0000_0000_0000_0100_0000,
    StainlessSteel = 0b0000_0000_0000_0000_0000_0000_1000_0000,

    LeadSolder = 0b0000_0000_0000_0000_0000_0001_0000_0000,
    SilverSolder = 0b0000_0000_0000_0000_0000_0010_0000_0000,
    Bismuth = 0b0000_0000_0000_0000_0000_0100_0000_0000,
    Brass = 0b0000_0000_0000_0000_0000_1000_0000_0000,
    Chromium = 0b0000_0000_0000_0000_0001_0000_0000_0000,
    Cupronickel = 0b0000_0000_0000_0000_0010_0000_0000_0000,
    Electrum = 0b0000_0000_0000_0000_0100_0000_0000_0000,
    Gold = 0b0000_0000_0000_0000_1000_0000_0000_0000,
    Lead = 0b0000_0000_0000_0001_0000_0000_0000_0000,
    Molybdochalkos = 0b0000_0000_0000_0010_0000_0000_0000_0000,
    Platinum = 0b0000_0000_0100_0000_0000_0000_0000_0000,
    Nickel = 0b0000_0000_1000_0000_0000_0000_0000_0000,
    Silver = 0b0000_0001_0000_0000_0000_0000_0000_0000,
    Tin = 0b0000_0000_0010_0000_0000_0000_0000_0000,
    Titanium = 0b0000_0100_0000_0000_0000_0000_0000_0000,
    Uranium = 0b0000_1000_0000_0000_0000_0000_0000_0000,
    Zinc = 0b0001_0000_0000_0000_0000_0000_0000_0000
}