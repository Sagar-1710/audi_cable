# Virtual Audio Cable (VAC) Clone & Dual-Bluetooth Audio Guide

## Overview

This software is an open-source, high-performance clone and modernization of **Virtual Audio Cable (VAC)** designed specifically to solve the Windows **Dual Bluetooth Audio** challenge:

> **Goal:** Connect and stream audio to **TWO Bluetooth headphones or speakers simultaneously** from a **single virtual audio source** (or Windows Master Audio).

---

## 1. Why Windows Cannot Naturally Play to Two Bluetooth Devices

In Windows 10 and Windows 11:
1. When you pair two Bluetooth headphones (e.g., *Headphones A* and *Headphones B*), Windows creates two separate **WASAPI Audio Endpoints**.
2. Windows only allows setting **one** default playback device at a time.
3. Unlike macOS (which has "Aggregate Audio Device" / "Multi-Output Device") or Samsung Galaxy (which has "Dual Audio"), Windows has no native dual-Bluetooth output setting.

---

## 2. How Virtual Audio Cable (VAC) & DualAudio Hub Solve This

### Traditional VAC Architecture (Eugene Muzychenko VAC)
- VAC installs a Windows kernel-mode driver (`volsspd.sys`) that simulates a physical sound card with interconnected pins:
  - **Render Pin** ("Virtual Cable In" / Playback device)
  - **Capture Pin** ("Virtual Cable Out" / Recording device)
- Audio sent into the Render pin is transferred through a DMA ring buffer in kernel memory to the Capture pin.
- Then, the user must run **two separate instances** of `audiorepeater.exe` (Audio Repeater MME/WASAPI/KS):
  - Repeater 1: Input = Virtual Cable 1 $\rightarrow$ Output = Bluetooth Device 1
  - Repeater 2: Input = Virtual Cable 1 $\rightarrow$ Output = Bluetooth Device 2
- **Drawbacks of original VAC**:
  - Proprietary / Trial voice watermark ("Trial" spoken every 5 seconds).
  - Outdated 1990s UI requiring manual command-line arguments.
  - Requires Windows Driver Signature Enforcement bypass or kernel driver installation.
  - No automatic sample-rate resampling (leads to robotic distortion if one Bluetooth headphone is 44.1kHz and the other is 48kHz).
  - No phase/delay alignment for Bluetooth latency differences.

### Modern DualAudio Hub Architecture
Our software improves upon VAC with:
1. **Zero-Driver Plug & Play Mode (WASAPI Direct Loopback)**:
   - Can capture directly from Windows Master Audio or any existing audio device in user mode with sub-10ms latency.
   - No unsigned kernel driver, no test-mode booting, no BSOD risks.
2. **Virtual Cable Mode (App Isolation)**:
   - Compatible with any Virtual Cable endpoint (VB-Cable, VAC, Hi-Fi Cable).
   - Allows routing single apps (Spotify, Discord, Chrome, VLC, DAWs) to the virtual cable in Windows Sound Settings, broadcasting ONLY that app to both Bluetooth devices.
3. **Dynamic Fractional Resampler**:
   - Converts 48,000 Hz $\leftrightarrow$ 44,100 Hz in real time with linear interpolation.
4. **Bluetooth Phase & Sync Alignment**:
   - Different Bluetooth codecs have different latencies (e.g. SBC ~150-220ms, AAC ~120ms, AptX-LL ~40ms).
   - Our **Sync Delay Offset** slider allows delaying one device by milliseconds so sound hits both ears at the exact same instant!
5. **Modern Pro-Audio Web & Desktop UI + CLI**:
   - Live Left/Right stereo VU peak meters.
   - Independent volume sliders (0% - 200%) and mute toggles.
   - Quick "Play Sync Tone" button to test audio alignment.

---

## 3. Step-by-Step Setup: Dual Bluetooth Audio

### Step 1: Pair Both Bluetooth Devices to Windows
1. Turn on Bluetooth on your PC.
2. Put your first Bluetooth headphone/speaker into pairing mode and connect it in:
   `Settings > Bluetooth & devices > Devices > Add device`.
3. Put your second Bluetooth headphone/speaker into pairing mode and connect it too.
4. Verify both devices show as **Connected audio** in Windows.

### Step 2: Launch DualAudio Hub
- Double-click **`Start-DualAudio.bat`** (or run `Start-DualAudio.ps1`).
- The modern dashboard will open in your browser at `http://127.0.0.1:40880`.

### Step 3: Configure Single Source & Dual Targets
1. **Source**: Leave as `★ Windows Default Audio` (to duplicate all PC audio), or select your Virtual Cable.
2. **Target 1**: Select your first Bluetooth device (highlighted with `🎧`).
3. **Target 2**: Select your second Bluetooth device (highlighted with `🎧`).
4. Click the large **CONNECT DUAL AUDIO** button!
5. Audio is now streaming to **both Bluetooth devices simultaneously**!

### Step 4: Calibrate Bluetooth Sync Delay
1. Click **Play Sync Tone** in the UI.
2. Listen to whether the beep sounds simultaneously in both headphones or if one device lags behind.
3. If Device 1 is heard slightly before Device 2, add **+20ms to +60ms** on Device 1's *Sync Delay Offset* slider until the audio is aligned.

---

## 4. App Isolation Mode (Route only Spotify/YouTube/VLC)

If you only want a specific application to play to both Bluetooth devices while your PC notification sounds or games stay on your main computer speakers:
1. Install a free virtual cable driver (like VB-Cable from vb-audio.com or VAC).
2. In Windows:
   - Go to `Settings > System > Sound > Volume mixer`.
   - Find the app (e.g. Spotify, Chrome, or VLC).
   - Change **Output device** from *Default* to **CABLE Input / Line 1**.
3. In DualAudio Hub:
   - Set **Audio Source Device** to **CABLE Output / Line 1**.
4. Both Bluetooth headphones will hear the isolated app in sync!
