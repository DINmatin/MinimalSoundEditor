namespace MinimalSoundEditor
{
    internal readonly record struct BatchOptions(
       bool normalize,
    bool trimSilence,
    bool outputWav,
    bool outputMp4,
    bool uniqueFilename
    );
}
