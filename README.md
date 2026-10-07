# MySoundBoard

A Windows soundboard application built with WPF (.NET 8) that lets you trigger and play audio clips on demand. Supports dual audio output so you can simultaneously play sounds through your speakers and route them through a virtual microphone for use in Discord, OBS, or any voice/streaming software.

---

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Getting Started](#getting-started)
- [How to Use](#how-to-use)
  - [Adding Sound Buttons](#adding-sound-buttons)
  - [Configuring a Sound Button](#configuring-a-sound-button)
  - [Playing Sounds](#playing-sounds)
  - [Missing Sound Files](#missing-sound-files)
  - [Volume Control](#volume-control)
  - [Audio Output Devices](#audio-output-devices)
  - [Saving and Loading Soundboards](#saving-and-loading-soundboards)
  - [Sorting and Themes](#sorting-and-themes)
- [How It Works](#how-it-works)
- [Virtual Audio Cable Setup (VB-Cable)](#virtual-audio-cable-setup-vb-cable)

---

## Features

- Trigger audio clips (MP3, WAV, OGG) from a grid of customizable buttons
- Start and stop individual sounds independently (stopping resets to the beginning)
- Per-button play mode — click to start/stop, restart on every press, or hold to play
- Gapless looping — loop any sound continuously until manually stopped
- Dual audio output — play through speakers and a virtual mic simultaneously
- Per-button fade in and fade out with configurable durations (0–10 s)
- Fade toggle button — enable or disable fade per button without losing your configured durations
- Per-button volume slider for independent level control on each tile
- Configurable global hotkeys — assign a key combination to any button to trigger it from anywhere, plus a global Stop All hotkey
- Auto-stop timer — optionally stop a sound after a set number of seconds
- Trim — choose where each sound starts and ends, without editing the file
- Per-button background color for visual organization
- Per-button custom icons chosen from a built-in icon library
- Drag-and-drop reordering of buttons within the grid
- Drag audio files or whole folders from Explorer onto the board, or add several at once with File > Add Sounds
- Duplicate button — copy a button including all its settings
- Search/filter bar to quickly find buttons by label
- Global volume slider that applies in real time to all active sounds
- Visual playback progress indicator on each button
- System tray icon — minimize to tray; stop all sounds or exit from the tray menu
- Create, save, load, and delete multiple named soundboard layouts (stored as JSON); the last board reopens at startup
- Missing sound files are flagged and can be relinked in one go when a folder has moved
- Alphabetical sort for your button grid
- Light and Dark theme support (Windows Fluent design); theme, window size and position are remembered

---

## Requirements

- Windows 10 or 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (the installer handles this automatically)

---

## Getting Started

> **Security notice:** MySoundBoard is not code-signed with a certificate. Windows SmartScreen and some antivirus tools will flag the installer as unrecognized. This does **not** mean the app is malicious — it means the publisher is not verified by a certificate authority. If you are not comfortable bypassing these warnings, use the portable Option B below.

### Option A — Installer (recommended)

1. Download `MySoundBoard-Setup-X.X.X.exe` from the [Releases](../../releases) page.
2. If Windows Defender SmartScreen appears with **"Windows protected your PC"**:
   - Click **More info**
   - Click **Run anyway**
3. If Windows **deletes the installer file** before you can run it (common on machines with aggressive Defender settings):
   - Open **Windows Security** → **Virus & threat protection** → **Protection history**
   - Find the quarantined item and choose **Allow**
   - Alternatively, use Option B to skip the installer entirely
4. Follow the installer prompts. The app installs to `Program Files\MySoundBoard`.
5. On first launch the app creates `%APPDATA%\MySoundBoard\SoundBoards\` where your saved layouts are stored.

### Option B — Portable (no installer)

1. Download `MySoundBoard-Build-X.X.X.zip` from the [Releases](../../releases) page.
2. Extract the ZIP to any folder of your choice (e.g., `C:\Apps\MySoundBoard\`).
3. Run `MySoundBoard.exe` directly from that folder.
4. To create a shortcut: right-click `MySoundBoard.exe` → **Send to** → **Desktop (create shortcut)**, or drag it onto your taskbar to pin it.

> **Note:** The portable build requires [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) to be installed separately. The installer handles this automatically.

---

## How to Use

### Adding Sound Buttons

Click the **+** button (always the last tile in the grid) to add a new blank sound button. You can add as many buttons as you need.

To add many sounds at once:

- **Drag audio files or folders from Explorer** onto the board. Each supported file becomes a new button, named after the file. Dropping onto an existing tile inserts the new buttons right after it; dropping on empty space adds them at the end. Folders contribute the audio files directly inside them (not subfolders), in name order.
- Use **File > Add Sounds…** (Ctrl+O) and select several files.
- In a button's file picker (the pencil), select several files: the first goes on that button and the rest are added after it.

Use the **search bar** at the top of the window to filter visible buttons by their label — useful on large boards.

### Configuring a Sound Button

Each button has a row of small action icons along the bottom:

| Icon | Action |
|------|--------|
| Pencil (Edit) | Opens a file picker to assign an audio file (MP3, WAV, or OGG). The button label updates automatically to the file name, which you can rename by typing in the text field at the top of the tile. |
| Smiley (Icon) | Opens the icon picker so you can choose a custom symbol displayed on the play button. |
| Loop | Toggles loop mode. The button is highlighted in your Windows accent colour when active. The clip loops seamlessly with no gap until you stop it manually. Enabling loop also disables the Fade button. |
| Fade | Toggles fade in/out for this button. Highlighted in the accent colour when enabled. Configure the durations (0–10 s each) via right-click → **Fade In / Out**. Automatically disabled when Loop is on. |
| Headphones | Toggles dual output mode for this button. The button is highlighted in the accent colour when enabled. When active, the sound plays on both the **Primary Output** and the **Secondary Output** device simultaneously (see [Audio Output Devices](#audio-output-devices)). |
| Trash (Delete) | Removes this button from the board and stops any active playback. |

**Right-click any sound button** for additional options:

| Option | Description |
|--------|-------------|
| Rename | Focuses the title field so you can type a new name. |
| Set Color | Applies a background tint to the tile for visual grouping. |
| Locate Sound File… | Points the button at a different file while keeping its name and all its settings. Useful after moving or renaming a sound (see [Missing Sound Files](#missing-sound-files)). |
| Play Mode | **Click to Start / Stop** (default): press to play, press again to stop. **Restart on Every Press**: each press starts the sound over from the beginning. **Hold to Play**: the sound plays only while you hold the mouse button (or the hotkey) down. |
| Set Hotkey | Assigns a global key combination (e.g., Ctrl+Alt+1) that starts or stops this button from anywhere on your desktop. If another application already owns the combination when a board loads, you'll be told which ones, and the hotkey badge is shown struck through; the hotkey is kept with the board so it works again once it's free. |
| Clear Hotkey | Removes the button's hotkey. |
| Trim… | Choose where the sound starts and ends (in 0.1 s steps) without editing the file — handy for clips with silence or extra material at either end. Looping repeats only the trimmed part, and fade-out is timed to the trimmed end. |
| Fade In / Out | Opens a slider dialog to set the fade-in and fade-out durations (0–10 s). These values are saved even when the Fade toggle is off. |
| Auto-Stop Timer | Stops playback automatically after a set number of seconds (0–300). Useful for sounds you want to cap at a fixed length regardless of file duration. |
| Duplicate | Creates a copy of the button with all its settings (hotkeys are stripped since they must be unique). |
| Delete | Same as the trash icon. |

You can also **drag and drop** buttons to reorder them within the grid.

### Playing Sounds

- **Click the large play button** in the center of a sound tile (or press its hotkey) to start playback.
- While playing, the button icon switches to a **stop** symbol and a progress bar fills across the button face.
- **Click again** to stop. Stopping always resets the sound, so the next click plays it from the beginning.
- When a sound finishes naturally it resets to the beginning. If loop mode is on, it keeps playing seamlessly.
- The **Play Mode** option (right-click) changes what a press does — see the table above. Hover over the play button to see which mode it's in.

To stop everything at once, press **Esc** while the window is focused, click **Stop All**, or use **Stop All Sounds** in the tray menu. To stop everything from inside a game or call, set a global shortcut with **Tools > Set Stop All Hotkey…** (remove it again with **Tools > Clear Stop All Hotkey**).

### Missing Sound Files

If a button's sound file has been moved, renamed or deleted, its play button is dimmed and its tooltip shows the missing path. Click it and MySoundBoard offers to let you locate the file. If you pick a file in a different folder, any other buttons whose sounds are missing from the same old folder are relinked automatically when files with the same names exist in the new one — so moving a whole sound folder only takes one fix.

The check is repeated each time the window regains focus, so files that come back (for example, a reconnected USB drive) are picked up without restarting. A missing file never opens a dialog from a hotkey; the press is simply ignored.

### Volume Control

Each button has a **thin slider directly below the play button** that controls that button's volume independently (0–100%). This multiplies with the global volume, so a button at 50% with the global at 80% plays at 40% of full volume.

The **slider at the bottom of the window** controls the global playback volume (0–100%). Moving it adjusts the volume of all currently playing sounds in real time.

### Audio Output Devices

At the very bottom of the window are two device selectors:

- **Primary Output** — the main audio device where all sounds play (your speakers, headset, etc.).
- **Secondary Output (mic routing)** — a second device used only when a button has the **Headphones** toggle enabled. Setting this to a virtual audio cable (e.g., VB-Cable Input) allows Discord or OBS to pick up the soundboard audio as if it came from a microphone. If this is set to the same device as Primary Output, dual output is automatically skipped and sound only plays once.

Both dropdowns list all DirectSound-compatible output devices detected on your system.

### Saving and Loading Soundboards

Use the **File** menu to manage soundboard layouts:

- **File > New Board** (Ctrl+N) — starts an empty board with a unique name such as `New Soundboard 2`.
- **File > Save** (Ctrl+S) — saves the current board (all buttons, their assigned files, names, icons, colors, hotkeys, loop/fade/headphone state, and volume) as a JSON file in `%APPDATA%\MySoundBoard\SoundBoards\`. The file is named after the title field at the top of the window. Change the title before saving to create a new named board; you'll be asked before a different board with the same name is replaced.
- **File > Load** — lists every saved board. Click one to load it, which clears the current grid and restores all buttons from the file.
- **File > Delete Current Board…** — permanently deletes the open board's file after confirmation (your audio files are not touched), then starts a new board. Only available once the board has been saved.
- **File > Open Boards Folder** — opens the folder where board files are stored, for backing up or copying them.
- **File > Exit** — closes the app.

If you have unsaved changes, MySoundBoard asks whether to save them before you create a new board, load another one, or exit.

The board you had open when you closed MySoundBoard is reopened automatically the next time it starts. If a board file is damaged (for example, hand-edited JSON with a typo), loading it shows an error and leaves your current board untouched.

Soundboard JSON files can be copied between machines as long as the audio file paths are still valid on the target machine.

### Sorting and Themes

- **Tools > Sort** — sorts all buttons alphabetically by their label.
- **Tools > Theme > Light / Dark** — switches the application between a light and dark Fluent UI theme. Your choice is remembered, along with the window size and position.

---

## How It Works

MySoundBoard is a WPF (.NET 8) application using the [WPF-UI](https://github.com/lepoco/wpfui) library for its Fluent design components and [NAudio](https://github.com/naudio/NAudio) for audio playback.

**Audio engine** — each sound button manages its own `AudioPlayer` instance (and optionally a second one for the headphone device). `AudioPlayer` wraps NAudio's `DirectSoundOut` with an `AudioFileReader` (or `VorbisWaveReader` for OGG), giving each button independent start/stop/volume control. A player is single-use: stopping disposes it, and the next play creates a fresh one from the beginning of the file. When dual output is enabled and the two selected devices are different, a separate `AudioPlayer` is created for the secondary device and both are started in sync.

**Fade** — fade in is applied immediately on playback start via NAudio's volume ramp. Fade out is triggered by the progress timer (below) once the playback position reaches `trackLength - fadeOutSeconds`, ramping the volume down before the clip ends. The auto-stop limit is checked on the same tick. Both are suppressed when loop mode is active or when the fade toggle is off.

**Progress tracking** — a `DispatcherTimer` ticks every 100 ms while a sound is playing. It reads the playback position within the trimmed region and updates the width of a fill rectangle overlaid on the play button, creating a visual progress bar.

**Hotkeys** — global hotkeys are registered with the Windows `RegisterHotKey` API via a `HotkeyManager` (with `MOD_NOREPEAT`, so holding a key fires once). Each button registers its key combination after its board has finished loading and unregisters it on delete or reassignment. Hotkeys work even when the app is minimized to the system tray. Windows only reports the key press, so **Hold to Play** via a hotkey polls the key state every 30 ms to notice the release.

**Persistence** — each `SoundBoardButton` implements `Serialize()` / `Deserialize()` to convert its state to and from a `JsonObject`. The main window collects these into a `JsonArray` on save. On load, every button is built first and the grid is only replaced if they all succeed, so a damaged file can't leave a half-loaded board. App-wide settings (devices, volume, theme, window placement, last board, Stop All hotkey) live in `%APPDATA%\MySoundBoard\settings.json`. Both files are written to a temporary file and then renamed, so a crash mid-save can't corrupt them.

**Loop and trim** — looping is gapless: a `LoopingSampleProvider` sits in the audio chain and plays only the trimmed region of the file, rewinding to the region's start the moment it reaches the end so the output buffer never drains between repeats. Toggling loop while a sound plays takes effect at the next end of the region.

**Structure** — `SoundBoardButton` doesn't reference the main window directly; it talks to its board through the `ISoundBoardHost` interface (volume, output devices, hotkey registration, adding/moving/removing buttons). `MainWindow` implements it, and the tests use a lightweight fake, so buttons can be tested without the whole window.

---

## Virtual Audio Cable Setup (VB-Cable)

VB-Cable is a free virtual audio device that creates a software-only audio pipe: anything sent to the **VB-Cable Input** device comes out of the **VB-Cable Output** device, which other programs (Discord, OBS, Teams, etc.) can treat as a real microphone.

### Step 1 — Download and Install VB-Cable

1. Go to the [VB-Audio website](https://vb-audio.com/Cable/) and download the VB-Cable package (it is free; a donation is appreciated).
2. Extract the ZIP file.
3. Right-click `VBCABLE_Setup_x64.exe` (on 64-bit Windows) and choose **Run as administrator**.
4. Click **Install Driver** and wait for the installation to complete.
5. **Restart your computer.** The driver requires a reboot to register properly.

### Step 2 — Verify the Devices Appear

1. Open **Settings > System > Sound** (or right-click the speaker icon in the taskbar and choose **Sound settings**).
2. Under **Output**, you should now see **CABLE Input (VB-Audio Virtual Cable)**.
3. Under **Input**, you should see **CABLE Output (VB-Audio Virtual Cable)**.

If these devices do not appear, re-run the installer as administrator and restart again.

### Step 3 — Configure MySoundBoard

1. Launch MySoundBoard.
2. In the **Primary Output** dropdown, select your normal speakers or headset (e.g., `Headphones (Realtek Audio)`). This is what you hear locally.
3. In the **Secondary Output (mic routing)** dropdown, select **CABLE Input (VB-Audio Virtual Cable)**. This is what will be sent to your voice software.
4. On any sound button you want other people to hear, click the **headphone icon** so it is highlighted.

### Step 4 — Configure Your Voice or Streaming Software

The goal is to tell Discord, OBS, Teams, etc. to use **CABLE Output** as a microphone input. Here is how to do it in common applications:

**Discord**
1. Open **User Settings > Voice & Video**.
2. Under **Input Device**, select **CABLE Output (VB-Audio Virtual Cable)**.
3. Disable **Echo Cancellation**, **Noise Suppression**, and **Automatic Gain Control** (these filters can muffle sound effects).

**OBS Studio**
1. In the **Audio Mixer**, click the gear icon on **Mic/Aux** and choose **Properties**.
2. Set **Device** to **CABLE Output (VB-Audio Virtual Cable)**.
3. Alternatively, add a new **Audio Input Capture** source and select CABLE Output.

**Microsoft Teams**
1. Click your profile picture > **Settings > Devices**.
2. Under **Microphone**, select **CABLE Output (VB-Audio Virtual Cable)**.

**Zoom**
1. Open **Settings > Audio**.
2. Under **Microphone**, select **CABLE Output (VB-Audio Virtual Cable)**.

### Step 5 — Mixing Your Real Mic with the Soundboard (Optional)

If you want others to hear both your voice and the soundboard at the same time, you need to mix the two signals. Two common approaches:

**Option A — OBS Virtual Camera / OBS Monitor**
Use OBS as a mixing hub: add your real microphone and the CABLE Output as separate audio sources in OBS, apply any filters you want, then use the **OBS-VirtualCam** or **OBS Virtual Audio** output as the microphone in Discord/Teams.

**Option B — VoiceMeeter (free, from VB-Audio)**
Install [VoiceMeeter](https://vb-audio.com/Voicemeeter/) alongside VB-Cable. Set your real microphone and CABLE Output as hardware inputs in VoiceMeeter, then point Discord/Teams at VoiceMeeter's virtual output. VoiceMeeter gives you full mixing, EQ, and routing control.

### Troubleshooting

| Problem | Solution |
|---------|----------|
| CABLE devices not showing in MySoundBoard | Close and reopen the device dropdown — the list is refreshed each time it opens. If they still don't appear, restart the app. |
| Others can't hear the soundboard | Make sure the headphone toggle is on (highlighted) for each button, and that your voice software's microphone is set to **CABLE Output**, not CABLE Input. |
| Soundboard audio is very quiet for others | VB-Cable passes audio at whatever volume MySoundBoard sends. Raise the volume slider in the app, or increase the CABLE Output level in Windows Sound settings. |
| Echo or feedback loop | Do not set your Windows **default playback** device to CABLE Input, and do not monitor CABLE Output through your speakers while also recording it. |
| Others hear a robotic/clipped sound | Turn off noise suppression and echo cancellation in Discord/Teams for the CABLE Output mic — these filters are tuned for voice, not music/SFX. |
