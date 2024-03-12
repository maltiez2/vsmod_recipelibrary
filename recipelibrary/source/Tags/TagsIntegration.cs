using HarmonyLib;
using Vintagestory.API.Common;

namespace RecipesLibrary.Tags;

internal static class TagsIntegration
{
    public static void Patch()
    {
        new Harmony(_harmonyId).Patch(
                AccessTools.Method(typeof(CollectibleObject), nameof(CollectibleObject.OnLoaded)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(TagsIntegration), nameof(OnCollectibleLoaded)))
            );
    }
    public static void Unpatch()
    {
        new Harmony(_harmonyId).Unpatch(AccessTools.Method(typeof(CollectibleObject), nameof(CollectibleObject.OnLoaded)), HarmonyPatchType.Prefix, _harmonyId);
    }

    private const string _harmonyId = "recipeslib_tags";
    private static void OnCollectibleLoaded(CollectibleObject __instance, ICoreAPI api)
    {
        try
        {
            if (!__instance.Attributes.KeyExists("tags") || !__instance.Attributes["tags"].IsArray()) return;

            string[] tags = __instance.Attributes["tags"].AsArray().Select(x => x.AsString()).ToArray();

            TagsSystem system = api.ModLoader.GetModSystem<TagsSystem>();

            system.AddTags(__instance, tags);
        }
        catch (Exception exception)
        {
            api.Logger.Error(exception);
        }
    }
}
