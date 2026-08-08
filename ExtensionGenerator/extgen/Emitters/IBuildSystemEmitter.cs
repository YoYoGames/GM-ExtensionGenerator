namespace extgen.Emitters
{
    /// <summary>
    /// Marker for build-system emitters (CMake, Cargo). Same contract as <see cref="IIrEmitter"/>.
    /// </summary>
    public interface IBuildSystemEmitter : IIrEmitter
    {
    }
}
