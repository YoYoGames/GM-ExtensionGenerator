using codegencore.Writers;
using codegencore.Writers.Concrete;
using codegencore.Writers.Lang;
using System.Text;

namespace extgen.Utils
{
    /// <summary>
    /// Helpers for writing code files with language-specific writers.
    /// </summary>
    public static class FileEmitHelpers
    {
        /// <summary>
        /// Writes a code file using a language-specific writer.
        /// Creates parent directories automatically unless disabled.
        /// </summary>
        /// <typeparam name="TWriter">Language-specific writer type.</typeparam>
        /// <param name="dir">Target directory.</param>
        /// <param name="fileName">Target filename.</param>
        /// <param name="writerFactory">Factory to create the writer from ICodeWriter.</param>
        /// <param name="emit">Action to emit code using the writer.</param>
        /// <param name="createDirectories">Whether to create parent directories.</param>
        /// <param name="emitUtf8Bom">Whether to emit UTF-8 BOM.</param>
        /// <param name="replace">Whether to replace existing files.</param>
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

            var cw = CodeWriter.From(tw, "    ");
            var writer = writerFactory(cw);
            emit(writer);
        }

        /// <summary>Writes a GML file.</summary>
        public static void WriteGml(
            string dir,
            string fileName,
            Action<GmlWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new GmlWriter(cw), emit, createDirectories, emitUtf8Bom, replace: true);

        /// <summary>Writes a GML file only if it doesn't exist.</summary>
        public static void WriteGmlIfMissing(
            string dir,
            string fileName,
            Action<GmlWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new GmlWriter(cw), emit, createDirectories, emitUtf8Bom, replace: false);

        /// <summary>Writes a Java file.</summary>
        public static void WriteJava(
            string dir,
            string fileName,
            Action<JavaWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new JavaWriter(cw), emit, createDirectories, emitUtf8Bom, replace: true);

        /// <summary>Writes a Java file only if it doesn't exist.</summary>
        public static void WriteJavaIfMissing(
            string dir,
            string fileName,
            Action<JavaWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new JavaWriter(cw), emit, createDirectories, emitUtf8Bom, replace: false);

        /// <summary>Writes a Kotlin file.</summary>
        public static void WriteKotlin(
            string dir,
            string fileName,
            Action<KotlinWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new KotlinWriter(cw), emit, createDirectories, emitUtf8Bom, replace: true);

        /// <summary>Writes a Kotlin file only if it doesn't exist.</summary>
        public static void WriteKotlinIfMissing(
            string dir,
            string fileName,
            Action<KotlinWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new KotlinWriter(cw), emit, createDirectories, emitUtf8Bom, replace: false);

        /// <summary>Writes a C++ file.</summary>
        public static void WriteCpp(
            string dir,
            string fileName,
            Action<CppWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new CppWriter(cw), emit, createDirectories, emitUtf8Bom, replace: true);

        /// <summary>Writes a C++ file only if it doesn't exist.</summary>
        public static void WriteCppIfMissing(
            string dir,
            string fileName,
            Action<CppWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new CppWriter(cw), emit, createDirectories, emitUtf8Bom, replace: false);

        /// <summary>Writes an Objective-C file.</summary>
        public static void WriteObjc(
            string dir,
            string fileName,
            Action<ObjcWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new ObjcWriter(cw), emit, createDirectories, emitUtf8Bom, replace: true);

        /// <summary>Writes an Objective-C file only if it doesn't exist.</summary>
        public static void WriteObjcIfMissing(
            string dir,
            string fileName,
            Action<ObjcWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new ObjcWriter(cw), emit, createDirectories, emitUtf8Bom, replace: false);

        /// <summary>Writes a Swift file.</summary>
        public static void WriteSwift(
            string dir,
            string fileName,
            Action<SwiftWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new SwiftWriter(cw), emit, createDirectories, emitUtf8Bom, replace: true);

        /// <summary>Writes a Swift file only if it doesn't exist.</summary>
        public static void WriteSwiftIfMissing(
            string dir,
            string fileName,
            Action<SwiftWriter> emit,
            bool createDirectories = true,
            bool emitUtf8Bom = false) => WriteFile(dir, fileName, cw => new SwiftWriter(cw), emit, createDirectories, emitUtf8Bom, replace: false);
    }
}
