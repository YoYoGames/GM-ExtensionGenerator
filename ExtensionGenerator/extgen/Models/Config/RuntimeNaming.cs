using System.Text.Json.Serialization;

namespace extgen.Models.Config
{
    /// <summary>
    /// Centralized naming conventions for runtime-level symbols shared across targets.
    /// Can be overridden via configuration if needed.
    /// </summary>
    public sealed class RuntimeNaming
    {
        /// <summary>Extension name used in generated code.</summary>
        [JsonPropertyName("extensionName")]
        public string ExtensionName { get; set; } = "MyExtension";

        /// <summary>Prefix for native C/C++ functions.</summary>
        [JsonPropertyName("nativePrefix")]
        public string NativePrefix { get; } = "__EXT_NATIVE__";

        /// <summary>Prefix for JNI functions.</summary>
        [JsonPropertyName("jniPrefix")]
        public string JniPrefix { get; } = "__EXT_JNI__";

        /// <summary>Prefix for Swift functions.</summary>
        [JsonPropertyName("swiftPrefix")]
        public string SwiftPrefix { get; } = "__EXT_SWIFT__";

        /// <summary>Prefix for JNI wrapper functions.</summary>
        [JsonPropertyName("jniWrapperPrefix")]
        public string JniWrapperPrefix { get; } = "__JNI_WRAPPER__";

        /// <summary>Name of the wire/bridge class.</summary>
        [JsonPropertyName("wireClass")]
        public string WireClass { get; } = "GMExtWire";

        /// <summary>Implementation field name.</summary>
        [JsonPropertyName("implmentationField")]
        public string ImplField { get; } = "__impl";

        /// <summary>Dispatch queue field name.</summary>
        [JsonPropertyName("dispatchQueueField")]
        public string DispatchQueueField { get; } = "__dispatch_queue";

        /// <summary>Buffer queue field name.</summary>
        [JsonPropertyName("bufferQueueField")]
        public string BufferQueueField { get; } = "__buffer_queue";

        /// <summary>Argument buffer parameter name.</summary>
        [JsonPropertyName("argBufferField")]
        public string ArgBufferParam { get; } = "__arg_buffer";

        /// <summary>Argument buffer length parameter name.</summary>
        [JsonPropertyName("argBufferLengthField")]
        public string ArgBufferLengthParam { get; } = "__arg_buffer_length";

        /// <summary>Return buffer parameter name.</summary>
        [JsonPropertyName("retBufferField")]
        public string RetBufferParam { get; } = "__ret_buffer";

        /// <summary>Return buffer length parameter name.</summary>
        [JsonPropertyName("retBufferLengthField")]
        public string RetBufferLengthParam { get; } = "__ret_buffer_length";

        /// <summary>Result variable name.</summary>
        [JsonPropertyName("resultVar")]
        public string ResultVar { get; } = "__result";

        /// <summary>Buffer reader variable name.</summary>
        [JsonPropertyName("bufferReaderVar")]
        public string BufferReaderVar { get; } = "__br";

        /// <summary>Buffer writer variable name.</summary>
        [JsonPropertyName("bufferWriterVar")]
        public string BufferWriterVar { get; } = "__bw";

        /// <summary>Byte I/O namespace.</summary>
        [JsonPropertyName("byteIONamespace")]
        public string ByteIONamespace { get; } = "gm::byteio";

        /// <summary>Extension wire namespace.</summary>
        [JsonPropertyName("extWireNamespace")]
        public string ExtWireNamespace { get; } = "gm::wire";

        /// <summary>Extension wire implementation details namespace.</summary>
        [JsonPropertyName("extWireDetailsNamespace")]
        public string ExtWireDetailsNamespace { get; } = "gm::wire::details";

        /// <summary>Runtime namespace.</summary>
        [JsonPropertyName("runtimeNamespace")]
        public string RuntimeNamespace { get; } = "gm::runtime";

        /// <summary>Code generation namespace.</summary>
        [JsonPropertyName("codeGenNamespace")]
        public string CodeGenNamespace { get; } = "gm::wire::codec";

        /// <summary>Structs namespace.</summary>
        [JsonPropertyName("structsNamespace")]
        public string StructsNamespace { get; } = "gm_structs";

        /// <summary>Enums namespace.</summary>
        [JsonPropertyName("enumsNamespace")]
        public string EnumsNamespace { get; } = "gm_enums";

        /// <summary>Constants namespace.</summary>
        [JsonPropertyName("constsNamespace")]
        public string ConstantsNamespace { get; } = "gm_consts";

        /// <summary>Base Java/Kotlin package name (supports template variables).</summary>
        public string BasePackage { get; } = "${YYAndroidPackageName}";

        /// <summary>Bridge package name for Java/Kotlin.</summary>
        public string BridgePackage { get; } = "com.gamemaker.ExtensionCore.ExtBridge";

        /// <summary>Library name format string for JNI.</summary>
        public string LibraryNameFormat { get; } = "{0}";
    }
}