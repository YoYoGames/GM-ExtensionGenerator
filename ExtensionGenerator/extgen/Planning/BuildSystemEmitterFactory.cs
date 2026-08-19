using extgen.Emitters;
using extgen.Emitters.Cargo;
using extgen.Emitters.Cmake;
using extgen.Mappers;

namespace extgen.Planning
{
    /// <summary>
    /// Creates the build-system emitter selected by <see cref="EmitterPlan.BuildSystem"/>.
    /// </summary>
    public static class BuildSystemEmitterFactory
    {
        public static IBuildSystemEmitter? Create(EmitterPlan plan)
        {
            return plan.BuildSystem switch
            {
                BuildSystemKind.None => null,
                BuildSystemKind.Cmake => new CmakeEmitter(plan.Raw.Build.Cmake.ToSettings(), plan.Raw),
                BuildSystemKind.Cargo => new CargoEmitter(CargoEmitterSettings.From(plan), plan.Runtime),
                _ => throw new ArgumentOutOfRangeException(nameof(plan.BuildSystem))
            };
        }
    }
}
