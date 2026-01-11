namespace MinimalSoundEditor
{
    internal readonly record struct BatchOptions(
       bool normalize,
    bool trimSilence,
    bool saveUnattended,
    bool outputWav,
    bool outputMp4
    );
}
