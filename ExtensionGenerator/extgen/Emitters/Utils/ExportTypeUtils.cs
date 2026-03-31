using codegencore.Writers.Lang;
using extgen.Models;
using extgen.Models.Config;

namespace extgen.Emitters.Utils
{
    /// <summary>
    /// GameMaker export type classification for runtime interop.
    /// These are the ONLY three types that GameMaker's extension system can pass/return.
    /// Everything else must be marshaled through these primitives.
    /// </summary>
    internal enum ExportType
    {
        /// <summary>
        /// Double precision floating point (default).
        /// Used for: bool, int8-32, uint8-32, float, double, and any encoded numeric value.
        /// </summary>
        Double,

        /// <summary>
        /// String type (char* on native side, String in GML).
        /// Used for: string types in direct passing mode.
        /// </summary>
        String,

        /// <summary>
        /// Pointer/buffer type (ByteBuffer/char* depending on platform).
        /// Used for: complex types (arrays, structs, nullable, int64, etc.) via buffer protocol.
        /// </summary>
        Pointer
    }

    /// <summary>
    /// Extension methods for converting export types to language-specific types.
    /// </summary>
    internal static class ExportTypeExtensions
    {
        public static string AsCppType(this ExportType hostParamType) => hostParamType switch
        {
            ExportType.Double => "double",
            ExportType.String => "char*",
            ExportType.Pointer => "char*",
            _ => throw new NotImplementedException(),
        };

        public static string AsJavaType(this ExportType hostParamType) => hostParamType switch
        {
            ExportType.Double => "double",
            ExportType.String => "String",
            ExportType.Pointer => "ByteBuffer",
            _ => throw new NotImplementedException(),
        };


        public static string AsJniType(this ExportType hostParamType) => hostParamType switch
        {
            ExportType.Double => "jdouble",
            ExportType.String => "jstring",
            ExportType.Pointer => "jobject",
            _ => throw new NotImplementedException(),
        };

        public static string AsJniSig(this ExportType hostParamType) => hostParamType switch
        {
            ExportType.Double => "D",
            ExportType.String => "Ljava/lang/String;",
            ExportType.Pointer => "Ljava/nio/ByteBuffer;",
            _ => throw new NotImplementedException(),
        };

        public static string AsSwiftType(this ExportType hostParamType) => hostParamType switch
        {
            ExportType.Double => "Double",
            ExportType.String => "String",
            ExportType.Pointer => "UnsafeMutablePointer<CChar>?",
            _ => throw new NotImplementedException(),
        };
    }

    /// <summary>
    /// Represents an export parameter with its host type and name.
    /// </summary>
    internal record ExportParam(ExportType HostType, string Name);

    /// <summary>
    /// Utilities for analyzing IR functions and determining export types.
    /// </summary>
    internal class ExportTypeUtils
    {
        private static readonly DefaultExportTypeMap defaultMap = new();

        /// <summary>
        /// Gets the export parameters for a function using a custom type map.
        /// This determines the actual native function siganture based on calling convention.
        /// </summary>
        public static IEnumerable<ExportParam> ParamsFor(IrFunction fn, RuntimeNaming naming, IExportTypeMap map)
        {
            bool needArgs = IrAnalysis.NeedsArgsBuffer(fn);
            bool needRet = IrAnalysis.NeedsRetBuffer(fn);

            var list = new List<ExportParam>();

            // Two seperate modes:
            // 1. Buffer mode: All args packed into ByteBuffer → (ptr, length) params
            // 2. Direct mode: Each simple arg becomes a seperate param (string/double)

            if (needArgs)
            {
                // Buffer protocol: pass serialized args as (pointer, size)
                list.Add(new ExportParam(ExportType.Pointer, naming.ArgBufferParam));
                list.Add(new ExportParam(ExportType.Double, naming.ArgBufferLengthParam));
            }
            else
            {
                // Direct passing: each param gets its native export type
                foreach (var p in IrAnalysis.DirectArgs(fn))
                    list.Add(new ExportParam(map.Classify(p.Type), p.Name));
            }

            // If return value is complex, we append 2 more params (return buffer ptr + length).
            // Native code writes return value into this buffer, GML reads it back.
            // The function itself returns a status double (0.0 = success).
            if (needRet)
            {
                list.Add(new ExportParam(ExportType.Pointer, naming.RetBufferParam));
                list.Add(new ExportParam(ExportType.Double, naming.RetBufferLengthParam));
            }

            return list;
        }

        /// <summary>
        /// Gets the export parameters for a function using the default type map.
        /// </summary>
        public static IEnumerable<ExportParam> ParamsFor(IrFunction fn, RuntimeNaming naming) => ParamsFor(fn, naming, defaultMap);

        /// <summary>
        /// Gets the export return type for a function using a custom type map.
        /// </summary>
        public static ExportType ReturnFor(IrFunction fn, IExportTypeMap map)
        {
            if (IrAnalysis.NeedsRetBuffer(fn))
                return ExportType.Double;

            return map.Classify(fn.ReturnType);
        }

        /// <summary>
        /// Gets the export return type for a function using the default type map.
        /// </summary>
        public static ExportType ReturnFor(IrFunction fn) => ReturnFor(fn, defaultMap);
    }

    /// <summary>
    /// Extension methods for converting export parameter collections to language-specific formats.
    /// </summary>
    internal static class ExportParamExtensions
    {
        public static IEnumerable<Param> AsCpp(this IEnumerable<ExportParam> ps) => ps.Select(p => new Param(p.HostType.AsCppType(), p.Name));

        public static IEnumerable<Param> AsJava(this IEnumerable<ExportParam> ps) => ps.Select(p => new Param(p.HostType.AsJavaType(), p.Name));

        public static IEnumerable<Param> AsJniType(this IEnumerable<ExportParam> ps) => ps.Select(p => new Param(p.HostType.AsJniType(), p.Name));

        public static IEnumerable<Param> AsJniSig(this IEnumerable<ExportParam> ps) => ps.Select(p => new Param(p.HostType.AsJniSig(), p.Name));

        public static IEnumerable<ObjcParam> AsObjc(this IEnumerable<ExportParam> ps) => ps.Select((p, i) => new ObjcParam(i == 0 ? string.Empty : $"arg{i}", p.HostType.AsCppType(), p.Name));

        public static IEnumerable<SwiftParam> AsSwift(this IEnumerable<ExportParam> ps) => ps.Select((p, i) => new SwiftParam(i == 0 ? "_" : $"arg{i}", p.Name, p.HostType.AsSwiftType()));
    }
}
