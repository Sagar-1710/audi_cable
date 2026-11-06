# DualAudio Hub (Virtual Audio Cable & Dual-Bluetooth Splitter)

A modern, high-performance clone and successor to **Virtual Audio Cable (VAC)** designed to stream a **single virtual audio source** simultaneously to **two Bluetooth headphones or speakers** on Windows 10 & 11.

---

## 🚀 Key Features

- **Dual-Bluetooth Broadcaster**: Stream the exact same audio in real time to two Bluetooth headphones, headsets, or wireless speakers.
- **Single Virtual Audio Source**:
  - **Direct Loopback Mode (Plug & Play)**: Captures master Windows audio without requiring any kernel drivers.
  - **Virtual Cable Mode (App Isolation)**: Routes isolated apps (Spotify, Discord, Chrome, DAWs) to the virtual cable and broadcasts them to both Bluetooth devices.
- **Low-Latency WASAPI Engine**: Native C# WASAPI Core Audio streaming engine with lock-free buffering and sub-10ms internal latency.
- **Real-Time Resampling**: Handles sample rate mismatches seamlessly (e.g. 48,000 Hz ↔ 44,100 Hz).
- **Bluetooth Phase & Sync Delay Alignment**: Eliminates echo or desync between different Bluetooth headphone codecs (SBC vs AAC vs AptX) using live millisecond delay offset sliders.
- **Independent Controls**: Per-device volume fader (0% - 200%) and mute toggles.
- **Pro Audio Web Dashboard**: Cyberpunk / DAW aesthetic with 60fps stereo VU peak meters, signal routing flow diagrams, and quick presets.
- **Command-Line Interface (CLI)**: Headless terminal utility (`DualAudioCli.ps1`) for scripting and automation.

---

## 📁 Project Structure

```text
audi_cable\
├── DualAudioEngine.cs      # Core C# WASAPI repeater & routing engine
├── engine_host.ps1         # Background engine host and IPC controller
├── server.js               # Node.js local REST API server and static host
├── DualAudioCli.ps1        # Interactive command-line interface tool
├── Start-DualAudio.bat     # One-click Windows batch launcher
├── Start-DualAudio.ps1     # One-click PowerShell launcher
├── README.md               # Documentation and quickstart
├── website/                # Project showcase website (static HTML/CSS)
└── public/
    ├── index.html          # Modern DAW-style Web Dashboard
    ├── style.css           # Styling with glassmorphism and animated meters
    └── app.js              # Real-time Web UI client logic
```

---

## ⚡ Quick Start

### Option 1: Web Dashboard (Recommended)
1. Double-click **`Start-DualAudio.bat`** (or execute `.\Start-DualAudio.ps1`).
2. Your browser will automatically open:
   ```
   http://127.0.0.1:40880
   ```
3. In the dashboard:
   - Select your **First Bluetooth Headphone** under *Target 1*.
   - Select your **Second Bluetooth Headphone** under *Target 2*.
   - Click **CONNECT DUAL AUDIO**!

### Option 2: Command Line (CLI)
To list all active audio endpoints and identify Bluetooth devices:
```powershell
.\DualAudioCli.ps1 -List
```

To start streaming to two specific devices:
```powershell
.\DualAudioCli.ps1 -Target1 "Dubstep Pop 1200/1210" -Target2 "Speakers" -Delay2 40
```

---

## 🎧 Calibrating Bluetooth Sync Delay

Because different Bluetooth headphones use different internal Bluetooth DSPs and codecs:
1. Click **Play Sync Tone** in the dashboard.
2. If Headphone 1 plays slightly earlier than Headphone 2, increase **Target 1 Delay Offset** by `+20ms` to `+60ms`.
3. When both headphones sound simultaneously in phase, your settings are saved and active!

---

## 🔀 Isolating Specific Applications (Virtual Cable Mode)

If you only want music (e.g. Spotify) to broadcast to both Bluetooth devices, while games or notification beeps stay on your PC speakers:
1. Select your Virtual Cable as the **Audio Source Device** in DualAudio Hub.
2. In Windows: Go to **Settings > System > Sound > Volume mixer**.
3. Under *Apps*, set the Output of Spotify (or browser) to your Virtual Cable.
4. DualAudio Hub will now capture ONLY that app and duplicate it to both Bluetooth devices.
