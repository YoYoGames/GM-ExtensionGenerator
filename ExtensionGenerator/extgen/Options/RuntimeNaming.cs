using System.Text.Json.Serialization;

namespace extgen.Options
{
    /// <summary>
    /// Centralized naming for runtime-level symbols shared across targets.
    /// This can be overridden from config.json if needed.
    /// </summary>
    public sealed class RuntimeNaming
    {
        [JsonPropertyName("nativePrefix")]
        public string NativePrefix { get; } = "__EXT_NATIVE__";

        [JsonPropertyName("jniPrefix")]
        public string JniPrefix { get; } = "__EXT_JNI__";

        [JsonPropertyName("swiftPrefix")]
        public string SwiftPrefix { get; } = "__EXT_SWIFT__";

        [JsonPropertyName("jniWrapperPrefix")]
        public string JniWrapperPrefix { get; } = "__JNI_WRAPPER__";

        [JsonPropertyName("wireClass")]
        public string WireClass { get; } = "GMExtWire";

        // The following are mostly used by Java / Kotlin / JNI targets for now.

        [JsonPropertyName("implmentationField")]
        public string ImplField { get; } = "__impl";

        [JsonPropertyName("dispatchQueueField")]
        public string DispatchQueueField { get; } = "__dispatch_queue";

        [JsonPropertyName("bufferQueueField")]
        public string BufferQueueField { get; } = "__buffer_queue";

        [JsonPropertyName("argBufferField")]
        public string ArgBufferParam { get; } = "__arg_buffer";

        [JsonPropertyName("argBufferLengthField")]
        public string ArgBufferLengthParam { get; } = "__arg_buffer_length";

        [JsonPropertyName("retBufferField")]
        public string RetBufferParam { get; } = "__ret_buffer";

        [JsonPropertyName("retBufferLengthField")]
        public string RetBufferLengthParam { get; } = "__ret_buffer_length";

        [JsonPropertyName("resultVar")]
        public string ResultVar { get; } = "__result";

        [JsonPropertyName("bufferReaderVar")]
        public string BufferReaderVar { get; } = "__br";
        
        [JsonPropertyName("bufferWriterVar")]
        public string BufferWriterVar { get; } = "__bw";

        [JsonPropertyName("byteIONamespace")]
        public string ByteIONamespace { get; } = "gm::byteio";

        [JsonPropertyName("extWireNamespace")]
        public string ExtWireNamespace { get; } = "gm::wire";

        [JsonPropertyName("extWireDetailsNamespace")]
        public string ExtWireDetailsNamespace { get; } = "gm::wire::details";

        [JsonPropertyName("runtimeNamespace")]
        public string RuntimeNamespace { get; } = "gm::runtime";
        
        [JsonPropertyName("codeGenNamespace")]
        public string CodeGenNamespace { get; } = "gm::wire::codec";
        
        [JsonPropertyName("structsNamespace")]
        public string StructsNamespace { get; } = "gm_structs";
        
        [JsonPropertyName("enumsNamespace")]
        public string EnumsNamespace { get; } = "gm_enums";

        [JsonPropertyName("constsNamespace")]
        public string ConstantsNamespace { get; } = "gm_consts";

        // The following are mostly used by Java / Kotlin only

        public string BasePackage { get; } = "${YYAndroidPackageName}";

        public string BridgePackage { get; } = "com.gamemaker.ExtensionCore.ExtBridge";

        // The following are mostly used by JNI only

        public string LibraryNameFormat { get; } = "{0}";
    }
}