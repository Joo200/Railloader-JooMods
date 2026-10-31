using Railroader.Modding;

namespace SignalsEverywhere.Rr2026;

// Railroader 2026.1 loads SignalsEverywhere here (Info.json "assemblies"), in place of Railloader's plugin.
public sealed class ModEntryPoint : IModEntryPoint
{
    private SignalsEverywhere? _mod;

    public void OnModLoad(IModContext context)
    {
        _mod = new SignalsEverywhere(context.ModDirectory, context.CheckDependency);
        _mod.OnEnable();
        string modsDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(context.ModDirectory, ".."));
        MapFeatureGates.Load(modsDirectory, context.CheckDependency);
        Milestones.Load(modsDirectory, context.CheckDependency);
        var updater = new UnityEngine.GameObject("SignalsEverywhere Map Feature Gates");
        UnityEngine.Object.DontDestroyOnLoad(updater);
        updater.AddComponent<MapFeatureGatesUpdater>();
    }

    public void OnModUnload()
    {
        _mod?.OnDisable();
    }
}
