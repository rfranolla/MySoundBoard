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
  - [Volume Control](#volume-control)
  - [Audio Output Devices](#audio-output-devices)
  - [Saving and Loading Soundboards](#saving-and-loading-soundboards)
  - [Sorting and Themes](#sorting-and-themes)
- [How It Works](#how-it-works)
- [Virtual Audio Cable Setup (VB-Cable)](#virtual-audio-cable-setup-vb-cable)

---

## Features

- Trigger audio clips (MP3, WAV) from a grid of customizable buttons
- Play, pause, and stop individual sounds independently
- Loop any sound continuously until manually stopped
- Dual audio output — play through speakers and a virtual mic simultaneously
- Per-button custom icons chosen from a built-in icon library
- Global volume slider that applies in real time to all active sounds
- Visual playback progress indicator on each button
- Save and load multiple named soundboard layouts as JSON
- Alphabetical sort for your button grid
- Light and Dark theme support (Windows Fluent design)

---

## Requirements

- Windows 10 or 11
- [.NET 8 Runtime (Desktop)](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.26-windows-x64-installer)

---

## Getting Started

1. Run `MySoundBoard-Setup-1.0.0.exe` from the releases to install the app.
2. On first launch the app creates a `%APPDATA%\MySoundBoard\SoundBoards\` folder where your saved layouts are stored.

---

## How to Use

### Adding Sound Buttons

Click the **+** button (always the last tile in the grid) to add a new blank sound button. You can add as many buttons as you need.

### Configuring a Sound Button

Each button has a row of small action icons along the top:

| Icon | Action |
|------|--------|
| Pencil (Edit) | Opens a file picker to assign an audio file (MP3 or WAV) to this button. The button label updates automatically to the file name, which you can then rename by typing in the text field below. |
| Smiley (Icon) | Opens the icon picker so you can choose a custom symbol displayed on the play button. |
| Loop | Toggles loop mode. The button turns **blue** when looping is active. When the sound finishes it will restart automatically until you stop it manually. |
| Headphones | Toggles dual output mode for this button. The button turns **blue** when enabled. When active, the sound plays on both the **Primary Output** and the **Secondary Output** device simultaneously (see [Audio Output Devices](#audio-output-devices)). |
| Trash (Delete) | Removes this button from the board and stops any active playback. |

You can also rename a button at any time by clicking on the text label and typing a new name.

### Playing Sounds

- **Click the large play button (or custom icon)** in the center of a sound tile to start playback.
- While playing, the button icon switches to a **stop** symbol and a progress bar fills across the button face showing how far through the clip you are.
- **Click again** to stop. Click once more to restart from the beginning.
- When a sound finishes naturally it resets to the beginning. If loop mode is on, it restarts immediately.

### Volume Control

The **slider at the bottom** of the window controls the global playback volume (0–100). Moving it adjusts the volume of all currently playing sounds in real time, and the new level is applied to any sounds started afterward.

### Audio Output Devices

At the very bottom of the window are two device selectors:

- **Primary Output** — the main audio device where all sounds play (your speakers, headset, etc.).
- **Secondary Output (mic routing)** — a second device used only when a button has the **Headphones** toggle enabled. Setting this to a virtual audio cable (e.g., VB-Cable Input) allows Discord or OBS to pick up the soundboard audio as if it came from a microphone. If this is set to the same device as Primary Output, dual output is automatically skipped and sound only plays once.

Both dropdowns list all DirectSound-compatible output devices detected on your system.

### Saving and Loading Soundboards

Use the **File** menu to manage soundboard layouts:

- **File > Save** — saves the current board (all buttons, their assigned files, names, icons, loop state, and headphone toggle) as a JSON file in the `%APPDATA%\MySoundBoard\SoundBoards\` folder. The file is named after whatever text is in the title field at the top of the window (default: `My Soundboard`). Change the title before saving to create a new named board.
- **File > Load** — lists every saved `.json` board. Click one to load it, which clears the current grid and restores all buttons from the file.

Soundboard JSON files can be copied between machines as long as the audio file paths are still valid on the target machine.

### Sorting and Themes

- **Tools > Sort** — sorts all buttons alphabetically by their label.
- **Tools > Theme > Light / Dark** — switches the application between a light and dark Fluent UI theme.

---

## How It Works

MySoundBoard is a WPF (.NET 8) application using the [WPF-UI](https://github.com/lepoco/wpfui) library for its Fluent design components and [NAudio](https://github.com/naudio/NAudio) for audio playback.

**Audio engine** — each sound button manages its own `AudioPlayer` instance (and optionally a second one for the headphone device). `AudioPlayer` wraps NAudio's `DirectSoundOut` with an `AudioFileReader`, allowing independent control over play/pause/stop/volume per button. When dual output is enabled and the two selected devices are different, a separate `AudioPlayer` is created for the secondary device and both are started in sync.

**Progress tracking** — a `DispatcherTimer` ticks every 100 ms while a sound is playing. It reads the current playback position from `AudioFileReader.CurrentTime` and updates the width of a fill rectangle overlaid on the play button, creating a visual progress bar.

**Persistence** — each `SoundBoardButton` implements `Serialize()` / `Deserialized()` methods that convert its state to/from a `JsonObject`. The main window collects these objects into a `JsonArray` on save and reconstructs buttons from them on load.

**Loop** — looping is handled in the `PlaybackStopped` callback. If the stop reason is `PlaybackStoppedReachingEndOfFile` (i.e., the clip ended naturally, not from a user stop) and loop mode is on, `StartPlaying()` is called again immediately.

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
4. On any sound button you want other people to hear, click the **headphone icon** so it turns blue.

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
| CABLE devices not showing in MySoundBoard | Restart the app after installing VB-Cable; devices are enumerated on startup. |
| Others can't hear the soundboard | Make sure the headphone toggle (blue) is on for each button, and that your voice software's microphone is set to **CABLE Output**, not CABLE Input. |
| Soundboard audio is very quiet for others | VB-Cable passes audio at whatever volume MySoundBoard sends. Raise the volume slider in the app, or increase the CABLE Output level in Windows Sound settings. |
| Echo or feedback loop | Do not set your Windows **default playback** device to CABLE Input, and do not monitor CABLE Output through your speakers while also recording it. |
| Others hear a robotic/clipped sound | Turn off noise suppression and echo cancellation in Discord/Teams for the CABLE Output mic — these filters are tuned for voice, not music/SFX. |
