namespace extgen.Emitters.AppleMobile
{
    /// <summary>
    /// Where Apple ObjC/bridge sources are emitted for GameMaker consumption.
    /// </summary>
    public enum AppleMobileSourceLayout
    {
        /// <summary>
        /// Under the generator root (<c>code_gen/</c>, <c>src/</c>) and packaged into the
        /// extension XCFramework via CMake.
        /// </summary>
        BundledInXcframework = 0,

        /// <summary>
        /// Under GameMaker <c>iOSSource</c>/<c>tvOSSource</c> next to the <c>.yy</c>
        /// (compiled by the IDE).
        /// </summary>
        GameMakerSourceTree = 1,
    }

    /// <summary>
    /// How ObjC native wrappers resolve <c>__EXT_NATIVE__*</c> implementations.
    /// </summary>
    public enum AppleMobileNativeLink
    {
        /// <summary>
        /// Same binary: C++ exports header + <c>GMExtensionInitialise</c>.
        /// </summary>
        BundledCppExports = 0,

        /// <summary>
        /// Separate cdylib/framework: <c>extern "C"</c> declarations only.
        /// </summary>
        ExternalCdylib = 1,
    }
}
