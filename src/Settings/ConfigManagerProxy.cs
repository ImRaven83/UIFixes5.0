using System.Reflection;
using BepInEx.Unity.IL2CPP;

namespace UIFixes;

// ConfigurationManager is optional under BepInEx 6, so talk to it through reflection if it's installed
public class ConfigManagerProxy
{
    public const string GUID = "com.bepis.bepinex.configurationmanager";

    private readonly object instance;
    private readonly PropertyInfo displayingWindowProperty;
    private readonly MethodInfo buildSettingListMethod;

    public ConfigManagerProxy()
    {
        if (IL2CPPChainloader.Instance.Plugins.TryGetValue(GUID, out var pluginInfo))
        {
            instance = pluginInfo.Instance;
        }

        if (instance != null)
        {
            var type = instance.GetType();
            displayingWindowProperty = type.GetProperty("DisplayingWindow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            buildSettingListMethod = type.GetMethod("BuildSettingList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }

    public bool Present => instance != null;

    public bool DisplayingWindow => instance != null && displayingWindowProperty?.GetValue(instance) is true;

    public void BuildSettingList()
    {
        if (instance != null)
        {
            buildSettingListMethod?.Invoke(instance, []);
        }
    }
}
