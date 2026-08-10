namespace extgen.Planning
{
    /// <summary>
    /// Which portable native FFI emitter to run (<c>__EXT_NATIVE__*</c> layer).
    /// Distinct from packaging policies (Apple/Android/injectors).
    /// </summary>
    public enum PortableNativeLanguage
    {
        None = 0,
        Cpp = 1,
        Rust = 2,
    }
}
