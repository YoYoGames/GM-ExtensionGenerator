using codegencore.Writers.Lang;
using extgen.Models;
using extgen.Models.Config;

namespace extgen.Emitters.Utils
{
    internal enum ExportType
    {
        Double,
        String,
        Pointer
    }

    internal static class ExportTypeExtensions {
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

    internal record ExportParam(ExportType HostType, string Name);

    internal class ExportTypeUtils
    {
        private static readonly DefaultExportTypeMap defaultMap = new();

        public static IEnumerable<ExportParam> ParamsFor(IrFunction fn, RuntimeNaming naming, IExportTypeMap map)
        {
            bool needArgs = IrAnalysis.NeedsArgsBuffer(fn);
            bool needRet = IrAnalysis.NeedsRetBuffer(fn);

            var list = new List<ExportParam>();

            if (needArgs)
            {
                list.Add(new ExportParam(ExportType.Pointer, naming.ArgBufferParam));
                list.Add(new ExportParam(ExportType.Double, naming.ArgBufferLengthParam));
            }
            else
            {
                foreach (var p in IrAnalysis.DirectArgs(fn))
                    list.Add(new ExportParam(map.Classify(p.Type), p.Name));
            }

            if (needRet)
            {
                list.Add(new ExportParam(ExportType.Pointer, naming.RetBufferParam));
                list.Add(new ExportParam(ExportType.Double, naming.RetBufferLengthParam));
            }

            return list;
        }

        public static IEnumerable<ExportParam> ParamsFor(IrFunction fn, RuntimeNaming naming) => ParamsFor(fn, naming, defaultMap);

        public static ExportType ReturnFor(IrFunction fn, IExportTypeMap map)
        {
            // special case buffers
            if (IrAnalysis.NeedsRetBuffer(fn))
                return ExportType.Double; // engine returns `double`

            return map.Classify(fn.ReturnType);
        }

        public static ExportType ReturnFor(IrFunction fn) => ReturnFor(fn, defaultMap);
        
    }

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
