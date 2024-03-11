using System.Text;

namespace RecipesLibrary.TagsComplex;

[Flags]
internal enum LibraryTags
{
    None = 0b0000_0000_0000_0000_0000_0000_0000_0000,
    Type = 0b0000_0000_0000_0000_0000_0000_0000_0001,
    Metal = 0b0000_0000_0000_0000_0000_0000_0000_0010,
    Wood = 0b0000_0000_0000_0000_0000_0000_0000_0100,
    Cloth = 0b0000_0000_0000_0000_0000_0000_0000_1000,
    Stone = 0b0000_0000_0000_0000_0000_0000_0001_0000,
    Liquid = 0b0000_0000_0000_0000_0000_0000_0010_0000,
}

[Flags]
internal enum LibraryTags_Type : byte
{
    None = 0b0000_0000,
    Block = 0b0000_0001,
    Item = 0b0000_0010,
    Entity = 0b0000_0100,

    BlockEntity = 0b0000_1000,
    Player = 0b0001_0000,
    Projectile = 0b0010_0000
}

[Flags]
internal enum LibraryTags_Metal : uint
{
    None = 0b0000_0000_0000_0000_0000_0000_0000_0000,
    Modded = 0b1000_0000_0000_0000_0000_0000_0000_0000,

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

public static class LibraryTagsRegistry
{
    public static IEnumerable<string> Paths => _libraryTagsPaths;

    internal static readonly Dictionary<Tag, LibraryTagsValue> _libraryTagsMapping = new();
    internal static readonly HashSet<string> _libraryTagsPaths = new();
    internal static LibraryTags TagsWithValues { get; private set; } = LibraryTags.None;

    internal static void ConstructMapping()
    {
        _libraryTagsPaths.Clear();
        _libraryTagsMapping.Clear();
        TagsWithValues = LibraryTags.None;

        string[] libraryTagsNames = Enum.GetNames<LibraryTags>();
        LibraryTags[] libraryTagsValues = Enum.GetValues<LibraryTags>();

        for (int tagIndex = 0; tagIndex < libraryTagsNames.Length; tagIndex++)
        {
            string tagName = libraryTagsNames[tagIndex].ToLower();
            LibraryTags tag = libraryTagsValues[tagIndex];

            _libraryTagsMapping.Add(new("recipeslib", tagName), new LibraryTagsValue(tag));
            _libraryTagsPaths.Add($"recipeslib:{tagName}");

            Type? valueType = GetTagValuesType(tag);
            if (valueType == null) continue;

            TagsWithValues |= tag;

            string[] valueNames = Enum.GetNames(valueType);
            Array valueValues = Enum.GetValues(valueType);

            for (int valueIndex = 0; valueIndex < valueNames.Length; valueIndex++)
            {
                string valueName = valueNames[valueIndex].ToLower();
                object? valueValue = valueValues.GetValue(valueIndex);
                if (valueValue == null) continue;

                _libraryTagsMapping.Add(new("recipeslib", tagName, valueName), new LibraryTagsValue(tag, (tag, valueValue)));
                _libraryTagsPaths.Add($"recipeslib:{tagName}/{valueName}");
            }
        }
    }
    internal static Type? GetTagValuesType(LibraryTags tag)
    {
        try
        {
            return Type.GetType($"LibraryTags_{tag}");
        }
        catch
        {
            return null;
        }
    }
}
internal readonly struct LibraryTagsValue
{
    public readonly int Hash;
    public readonly LibraryTags Tags;
    public readonly LibraryTags_Type Type;
    public readonly LibraryTags_Metal Metal;

    public LibraryTagsValue(LibraryTags tags = LibraryTags.None, params (LibraryTags tag, object value)[] values)
    {
        Tags = tags;
        StringBuilder forHash = new();
        forHash.Append(tags);
        foreach ((LibraryTags tag, object value) in values)
        {
            forHash.Append($"|{tag}:{value}");
            switch (tag)
            {
                case LibraryTags.Type:
                    Type = (LibraryTags_Type)value;
                    break;
                case LibraryTags.Metal:
                    Metal = (LibraryTags_Metal)value;
                    break;
            }
        }

        Hash = forHash.ToString().GetHashCode();
    }

    public bool MatchAll(LibraryTagsValue value)
    {
        if (value.Tags == 0) return true;
        if (value.Tags != 0 && (Tags & value.Tags) != value.Tags) return false;
        if (value.Type != 0 && (Type & value.Type) != value.Type) return false;
        if (value.Metal != 0 && (Metal & value.Metal) != value.Metal) return false;

        return true;
    }
    public bool MatchAny(LibraryTagsValue value)
    {
        // Tags
        if (value.Tags == 0) return true;
        if (value.Tags != 0 && (Tags & value.Tags) == 0) return false;
        if (value.Tags != 0 && (Tags & value.Tags & LibraryTagsRegistry.TagsWithValues) != 0) return true;

        // Tags' values
        bool findAny = false;
        if (value.Type != 0 && (Type & value.Type) != 0) findAny = true;
        if (value.Metal != 0 && (Metal & value.Metal) != 0) findAny = true;
        return findAny;
    }

    public static LibraryTagsValue Or(LibraryTagsValue first, LibraryTagsValue second)
    {
        return new(
            first.Tags | second.Tags,
            (LibraryTags.Type, first.Type | second.Type),
            (LibraryTags.Metal, first.Metal | second.Metal)
            );
    }
    public static LibraryTagsValue And(LibraryTagsValue first, LibraryTagsValue second)
    {
        return new(
            first.Tags & second.Tags,
            (LibraryTags.Type, first.Type & second.Type),
            (LibraryTags.Metal, first.Metal & second.Metal)
            );
    }
    public static LibraryTagsValue Not(LibraryTagsValue value)
    {
        return new(
            ~value.Tags,
            (LibraryTags.Type, ~value.Type),
            (LibraryTags.Metal, ~value.Metal)
            );
    }
    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetHashCode() != Hash || obj is not LibraryTagsValue values)
        {
            return false;
        }

        return values.Tags == Tags && values.Type == Type && values.Metal == Metal;
    }
    public override int GetHashCode() => Hash;
    public static bool operator ==(LibraryTagsValue left, LibraryTagsValue right)
    {
        return left.Equals(right);
    }
    public static bool operator !=(LibraryTagsValue left, LibraryTagsValue right)
    {
        return !(left == right);
    }

    public override string ToString() // Needs refactoring?
    {
        StringBuilder output = new();
        LibraryTags[] libraryTagsValues = Enum.GetValues<LibraryTags>();
        LibraryTags tags = Tags;
        LibraryTags_Type typeTagsValues = Type;
        LibraryTags_Metal metalTagsValues = Metal;

        bool first = true;
        foreach (LibraryTags tag in libraryTagsValues.Where(value => (tags & value) != 0))
        {
            if ((tag & LibraryTagsRegistry.TagsWithValues) == 0)
            {
                if (!first) output.Append(',');
                first = false;
                output.Append($"recipeslib:{tag}");
                continue;
            }

            switch (tag)
            {
                case LibraryTags.Type:
                    LibraryTags_Type[] typeTags = Enum.GetValues<LibraryTags_Type>();
                    foreach (LibraryTags_Type typeTag in typeTags.Where(value => (typeTagsValues & value) != 0))
                    {
                        if (!first) output.Append(',');
                        first = false;
                        output.Append($"recipeslib:{tag}/{typeTag}");
                    }
                    break;
                case LibraryTags.Metal:
                    LibraryTags_Metal[] metalTags = Enum.GetValues<LibraryTags_Metal>();
                    foreach (LibraryTags_Metal metalTag in metalTags.Where(value => (metalTagsValues & value) != 0))
                    {
                        if (!first) output.Append(',');
                        first = false;
                        output.Append($"recipeslib:{tag}/{metalTag}");
                    }
                    break;
            }
        }

        return output.ToString();
    }
}
