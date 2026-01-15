using codegencore.Writers;
using codegencore.Writers.Concrete;
using codegencore.Writers.Lang;
using System.Text;

namespace extgen.Utils
{
    public static class FileEmitHelpers
    {
        /// <summary>
        /// Creates a file only if it does not already exist.
        /// Supports any language writer through <paramref name="writerFactory"/>.
        /// Automatically creates directories unless disabled.
        /// </summary>
        public static void WriteFile<TWriter>(
            string dir,
            string fileName,
            Func<ICodeWriter, TWriter> writerFactory,
            Action<TWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false, 
            bool replace = true)
        {
            var path = Path.Combine(dir, fileName);

            if (createDirectories)
                Directory.CreateDirectory(dir);

            if (!replace && File.Exists(path))
                return;

            using var tw = new StreamWriter(
                path,
                append: false,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitUtf8Bom));

            var cw = CodeWriter.From(tw, "    "); // this is your static helper, returns ICodeWriter
            var writer = writerFactory(cw);
            emit(writer);
        }

        // Convenience overloads so call sites stay short / nice:
        public static void WriteJava(
            string dir,
            string fileName,
            Action<JavaWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new JavaWriter(cw), emit, createDirectories, emitUtf8Bom, replace: true);

        public static void WriteJavaIfMissing(
            string dir,
            string fileName,
            Action<JavaWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new JavaWriter(cw), emit, createDirectories, emitUtf8Bom, replace: false);

        public static void WriteKotlin(
            string dir,
            string fileName,
            Action<KotlinWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new KotlinWriter(cw), emit, createDirectories, emitUtf8Bom, replace: true);

        public static void WriteKotlinIfMissing(
            string dir,
            string fileName,
            Action<KotlinWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new KotlinWriter(cw), emit, createDirectories, emitUtf8Bom, replace: false);

        public static void WriteCpp(
            string dir, 
            string fileName, 
            Action<CppWriter> emit, 
            bool createDirectories = true, 
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new CppWriter(cw), emit, createDirectories, emitUtf8Bom, replace: true);

        public static void WriteCppIfMissing(
            string dir,
            string fileName,
            Action<CppWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new CppWriter(cw), emit, createDirectories, emitUtf8Bom, replace: false);

        public static void WriteObjc(
            string dir,
            string fileName,
            Action<ObjcWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new ObjcWriter(cw), emit, createDirectories, emitUtf8Bom, replace: true);

        public static void WriteObjcIfMissing(
            string dir,
            string fileName,
            Action<ObjcWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new ObjcWriter(cw), emit, createDirectories, emitUtf8Bom, replace: false);

        public static void WriteSwift(
            string dir,
            string fileName,
            Action<SwiftWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new SwiftWriter(cw), emit, createDirectories, emitUtf8Bom, replace: true);

        public static void WriteSwiftIfMissing(
            string dir,
            string fileName,
            Action<SwiftWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new SwiftWriter(cw), emit, createDirectories, emitUtf8Bom, replace: false);
    }
}
