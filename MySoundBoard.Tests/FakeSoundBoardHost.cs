using MySoundBoard.Controls;
using MySoundBoard.Managers;
using NAudio.Wave;

namespace MySoundBoard.Tests
{
    /// <summary>Stand-in board for testing <see cref="SoundBoardButton"/> without a MainWindow.</summary>
    internal sealed class FakeSoundBoardHost : ISoundBoardHost
    {
        public float Volume { get; set; } = 1f;
        public DirectSoundDeviceInfo? PrimaryDevice { get; set; }
        public DirectSoundDeviceInfo? SecondaryDevice { get; set; }
        public IHotkeyRegistrar? Hotkeys { get; set; }

        public List<SoundBoardButton> Removed { get; } = new();
        public List<(SoundBoardButton Reference, SoundBoardButton NewButton)> Inserted { get; } = new();
        public List<(List<string> Paths, SoundBoardButton? After)> AddedFiles { get; } = new();

        public void RemoveButton(SoundBoardButton button) => Removed.Add(button);
        public void InsertButtonAfter(SoundBoardButton reference, SoundBoardButton newButton) => Inserted.Add((reference, newButton));
        public void MoveButton(SoundBoardButton source, SoundBoardButton target) { }
        public void AddSoundFiles(IEnumerable<string> paths, SoundBoardButton? after) => AddedFiles.Add((paths.ToList(), after));
        public int RelinkMissingSounds(string oldDirectory, string newDirectory) => 0;
    }

    /// <summary>Records registrations; combinations listed in <see cref="Taken"/> fail like a key owned by another app.</summary>
    internal sealed class FakeHotkeyRegistrar : IHotkeyRegistrar
    {
        private int _nextId = 1;
        public HashSet<(uint Modifiers, uint Vk)> Taken { get; } = new();
        public Dictionary<int, (uint Modifiers, uint Vk)> Registered { get; } = new();

        public int Register(uint modifiers, uint vk, Action callback)
        {
            if (Taken.Contains((modifiers, vk)) || Registered.ContainsValue((modifiers, vk))) return -1;
            int id = _nextId++;
            Registered[id] = (modifiers, vk);
            return id;
        }

        public void Unregister(int id) => Registered.Remove(id);
    }
}
