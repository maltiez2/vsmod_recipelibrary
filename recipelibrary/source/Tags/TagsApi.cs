using Vintagestory.API.Common;

namespace RecipesLibrary.API;

public interface ITagMatcher
{
    bool Match(RegistryObject registryObject);
}

public interface ITagsSystem
{
    event Action<ICoreAPI, ITagsSystem>? TagsLoaded;

    bool AddTags(RegistryObject registryObject, params string[] tags);
    bool RemoveTags(RegistryObject registryObject, params string[] tags);
    void RemoveAll(RegistryObject registryObject);
    ITagMatcher GetMatcher(params string[] tags);
    IEnumerable<string> GetTags(RegistryObject registryObject);
    IEnumerable<string> GetAllTags();
}
