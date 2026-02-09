using System.Text.Json.Serialization;

namespace extgen.Emitters.Cmake
{
    public sealed class CmakeEmitterSettings
    {
        public int CppStandard { get; set; } = 17;

        public bool CppExtensions { get; set; } = false;

        public bool StrictWarnings { get; set; } = true;

        public bool UseThirdParty { get; set; } = true;

        public bool EmitPresets { get; set; } = true;

    }
}
