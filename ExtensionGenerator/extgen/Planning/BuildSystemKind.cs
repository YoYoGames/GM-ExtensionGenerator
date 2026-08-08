namespace extgen.Planning
{
    /// <summary>
    /// Which build-system emitter to run (selected by <see cref="EmitterPlan"/>).
    /// </summary>
    public enum BuildSystemKind
    {
        None,
        Cmake,
        Cargo
    }
}
