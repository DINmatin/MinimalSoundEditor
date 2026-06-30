namespace MinimalSoundEditor
{
    /// <summary>Immutable snapshot of the batch choices so processing cannot change mid-run.</summary>
    internal readonly record struct BatchOptions(
       bool normalize,
    bool trimSilence,
    bool outputWav,
    bool outputMp4,
    bool uniqueFilename
    );
}
