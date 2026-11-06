/**
 * DualAudio Hub - Pure Live Audio Streaming Client
 */

let devices = [];
let isRunning = false;
let statusInterval = null;

const selectSource = document.getElementById('selectSource');
const selectTarget1 = document.getElementById('selectTarget1');
const selectTarget2 = document.getElementById('selectTarget2');

const btnMasterToggle = document.getElementById('btnMasterToggle');
const toggleButtonText = document.getElementById('toggleButtonText');
const btnRefresh = document.getElementById('btnRefresh');

const engineStatusPill = document.getElementById('engineStatusPill');
const routingVisualizer = document.querySelector('.routing-visualizer');
const routingLine = document.querySelector('.routing-line');

const visSourceName = document.getElementById('visSourceName');
const visTgt1Name = document.getElementById('visTgt1Name');
const visTgt2Name = document.getElementById('visTgt2Name');

const meterFillL = document.getElementById('meterFillL');
const meterFillR = document.getElementById('meterFillR');
const peakDbText = document.getElementById('peakDbText');

const hudStatus = document.getElementById('hudStatus');
const hudFrames = document.getElementById('hudFrames');

window.addEventListener('DOMContentLoaded', async () => {
    bindEvents();
    await loadDevices();
    startStatusPolling();
});

function bindEvents() {
    btnRefresh.addEventListener('click', async () => {
        btnRefresh.classList.add('loading');
        await loadDevices();
        btnRefresh.classList.remove('loading');
    });

    btnMasterToggle.addEventListener('click', toggleMasterStreaming);

    selectSource.addEventListener('change', updateVisualizerNames);
    selectTarget1.addEventListener('change', updateVisualizerNames);
    selectTarget2.addEventListener('change', updateVisualizerNames);
}

async function loadDevices() {
    try {
        const res = await fetch('/api/devices');
        const json = await res.json();
        if (json.devices) {
            devices = json.devices;
            renderDeviceDropdowns();
        }
    } catch (err) {
        console.error('Failed to load devices:', err);
    }
}

function renderDeviceDropdowns() {
    selectSource.innerHTML = '<option value="default">★ Windows Default Audio (Loopback)</option>';
    selectTarget1.innerHTML = '<option value="">-- Select First Bluetooth Headphone --</option>';
    selectTarget2.innerHTML = '<option value="">-- Select Second Bluetooth Headphone --</option>';

    let btDevices = [];
    let otherDevices = [];
    let hasVirtualCable = false;

    devices.forEach(dev => {
        const isBt = dev.isBluetooth;
        const isVac = dev.isVirtualCable;
        const icon = isBt ? '🎧 [Bluetooth]' : isVac ? '🔀 [Virtual Cable]' : '🔊';
        const label = `${icon} ${dev.name}`;

        const srcOpt = document.createElement('option');
        srcOpt.value = dev.id;
        srcOpt.textContent = isVac ? `★ ${label} (Virtual Medium Input)` : label;
        selectSource.appendChild(srcOpt);

        if (isVac) hasVirtualCable = true;

        if (isBt) {
            btDevices.push(dev);
        } else {
            otherDevices.push(dev);
        }
    });

    // Populate Targets with Bluetooth devices first
    const allTargets = [...btDevices, ...otherDevices];
    allTargets.forEach(dev => {
        const icon = dev.isBluetooth ? '🎧' : '🔊';
        const label = `${icon} ${dev.name}`;

        const opt1 = document.createElement('option');
        opt1.value = dev.id;
        opt1.textContent = label;
        selectTarget1.appendChild(opt1);

        const opt2 = document.createElement('option');
        opt2.value = dev.id;
        opt2.textContent = label;
        selectTarget2.appendChild(opt2);
    });

    // Smart auto-selection for the user's two Bluetooth headphones:
    // Dubstep Pop & Superpods
    const dubstep = btDevices.find(d => d.name.toLowerCase().includes('dubstep'));
    const superpods = btDevices.find(d => d.name.toLowerCase().includes('superpod'));

    if (dubstep && superpods) {
        selectTarget1.value = dubstep.id;
        selectTarget2.value = superpods.id;
    } else if (btDevices.length >= 2) {
        selectTarget1.value = btDevices[0].id;
        selectTarget2.value = btDevices[1].id;
    } else if (btDevices.length === 1) {
        selectTarget1.value = btDevices[0].id;
        if (otherDevices.length > 0) selectTarget2.value = otherDevices[0].id;
    }

    // Auto-select Virtual Cable as source if available
    if (hasVirtualCable) {
        const vacDev = devices.find(d => d.isVirtualCable);
        if (vacDev) selectSource.value = vacDev.id;
    }

    updateVisualizerNames();
}

function updateVisualizerNames() {
    // Source
    if (selectSource.value === 'default') {
        visSourceName.textContent = 'Windows Default Audio';
    } else {
        const dev = devices.find(d => d.id === selectSource.value);
        visSourceName.textContent = dev ? dev.name : 'Selected Source';
    }

    // Target 1
    const t1 = devices.find(d => d.id === selectTarget1.value);
    visTgt1Name.textContent = t1 ? t1.name : 'Select Target 1';

    // Target 2
    const t2 = devices.find(d => d.id === selectTarget2.value);
    visTgt2Name.textContent = t2 ? t2.name : 'Select Target 2';
}

async function toggleMasterStreaming() {
    if (!isRunning) {
        const src = selectSource.value;
        const t1 = selectTarget1.value;
        const t2 = selectTarget2.value;

        if (!t1 && !t2) {
            alert('Please select at least one Bluetooth device.');
            return;
        }

        btnMasterToggle.disabled = true;
        toggleButtonText.textContent = 'CONNECTING...';

        try {
            const res = await fetch('/api/start', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ sourceId: src, target1Id: t1, target2Id: t2 })
            });
            const json = await res.json();
            if (json.started) {
                setRunningState(true);
            } else {
                alert('Streaming could not start: ' + (json.message || 'Check audio devices.'));
                setRunningState(false);
            }
        } catch (err) {
            alert('Error: ' + err.message);
            setRunningState(false);
        } finally {
            btnMasterToggle.disabled = false;
        }
    } else {
        btnMasterToggle.disabled = true;
        toggleButtonText.textContent = 'STOPPING...';
        try {
            await fetch('/api/stop', { method: 'POST' });
            setRunningState(false);
        } catch (err) {
            console.error('Error stopping:', err);
        } finally {
            btnMasterToggle.disabled = false;
        }
    }
}

function setRunningState(running) {
    isRunning = running;
    btnMasterToggle.classList.toggle('active', isRunning);
    toggleButtonText.textContent = isRunning ? 'STOP DUAL AUDIO' : 'START LIVE DUAL AUDIO';

    engineStatusPill.classList.toggle('active', isRunning);
    engineStatusPill.querySelector('.status-label').textContent = isRunning ? 'STREAMING LIVE' : 'ENGINE IDLE';

    routingVisualizer.classList.toggle('active', isRunning);
    routingLine.classList.toggle('active', isRunning);

    hudStatus.textContent = isRunning ? 'STREAMING LIVE' : 'READY';
    hudStatus.className = isRunning ? 'hud-value text-green' : 'hud-value';
}

function startStatusPolling() {
    statusInterval = setInterval(async () => {
        try {
            const res = await fetch('/api/status');
            const json = await res.json();
            if (json.data) {
                updateHUD(json.data);
            }
        } catch (err) { }
    }, 80);
}

let decayL = 0;
let decayR = 0;

function updateHUD(data) {
    if (data.isRunning !== isRunning) {
        setRunningState(data.isRunning);
    }

    const pL = data.peakL || 0;
    const pR = data.peakR || 0;

    decayL = Math.max(pL, decayL * 0.85);
    decayR = Math.max(pR, decayR * 0.85);

    const pctL = Math.min(100, Math.round(decayL * 100));
    const pctR = Math.min(100, Math.round(decayR * 100));

    meterFillL.style.width = `${pctL}%`;
    meterFillR.style.width = `${pctR}%`;

    const maxVal = Math.max(decayL, decayR);
    if (maxVal > 0.001) {
        const db = (20 * Math.log10(maxVal)).toFixed(1);
        peakDbText.textContent = `${db} dB`;
    } else {
        peakDbText.textContent = '-inf dB';
    }

    hudFrames.textContent = (data.processedFrames || 0).toLocaleString();
}
