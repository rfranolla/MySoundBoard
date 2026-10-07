using MySoundBoard.Managers;
using NAudio.Wave;

namespace MySoundBoard.Controls
{
    /// <summary>What a <see cref="SoundBoardButton"/> needs from the board it sits on.</summary>
    public interface ISoundBoardHost
    {
        /// <summary>Global volume as a 0–1 multiplier.</summary>
        float Volume { get; }
        DirectSoundDeviceInfo? PrimaryDevice { get; }
        DirectSoundDeviceInfo? SecondaryDevice { get; }
        /// <summary>Null until the window has a handle to receive hotkey messages.</summary>
        IHotkeyRegistrar? Hotkeys { get; }

        void RemoveButton(SoundBoardButton button);
        void InsertButtonAfter(SoundBoardButton reference, SoundBoardButton newButton);
        void MoveButton(SoundBoardButton source, SoundBoardButton target);

        /// <summary>Creates a button per supported audio file (folders are expanded); null appends to the end.</summary>
        void AddSoundFiles(IEnumerable<string> paths, SoundBoardButton? after);

        /// <summary>Points other buttons whose missing files sit in <paramref name="oldDirectory"/> at the same names in <paramref name="newDirectory"/>.</summary>
        int RelinkMissingSounds(string oldDirectory, string newDirectory);
    }

    /// <summary>How the play button and hotkey respond to a press.</summary>
    public enum PlayMode
    {
        /// <summary>Press to start, press again to stop.</summary>
        Toggle,
        /// <summary>Every press starts the sound over from the beginning.</summary>
        Restart,
        /// <summary>Plays only while the button or hotkey is held down.</summary>
        Hold,
    }
}
