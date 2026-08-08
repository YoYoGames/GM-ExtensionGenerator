namespace extgen.Emitters.Cargo
{
    public sealed class CargoEmitterSettings
    {
        public string CrateName { get; init; } = "extension";
        public bool HasWindows { get; init; }
        public bool HasMac { get; init; }
        public bool HasLinux { get; init; }
        public bool HasAndroid { get; init; }
        public bool HasIos { get; init; }
        public bool HasTvos { get; init; }
        public string ExtensionName { get; init; } = "MyExtension";

        public static CargoEmitterSettings From(Planning.EmitterPlan plan) => new()
        {
            CrateName = global::extgen.Emitters.Rust.RustEmitterSettings.SanitizeCrateName(plan.Config.Raw.GameMaker.Extension?.ExtensionName ?? plan.Runtime.ExtensionName),
            ExtensionName = plan.Config.Raw.GameMaker.Extension?.ExtensionName ?? plan.Runtime.ExtensionName,
            HasWindows = plan.Config.HasWindows,
            HasMac = plan.Config.HasMac,
            HasLinux = plan.Config.HasLinux,
            HasAndroid = plan.AndroidJni,
            HasIos = plan.IosEnabled && plan.AppleNative,
            HasTvos = plan.TvosEnabled && plan.AppleNative
        };
    }
}
