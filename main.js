/**
 * DualAudio Hub - Electron Main Process
 * Hosts the Node.js HTTP server + WASAPI engine inside a native window
 */

const { app, BrowserWindow, Tray, Menu, nativeImage, shell } = require('electron');
const path = require('path');
const http = require('http');

// ── Determine resource paths (dev vs packaged) ─────────────────────────────
const isPackaged = app.isPackaged;
const resourceBase = isPackaged ? process.resourcesPath : __dirname;

// ── Single instance lock ───────────────────────────────────────────────────
// Launching the app twice used to crash with EADDRINUSE on port 40880.
// Now a second launch just focuses the window of the running instance.
const gotTheLock = app.requestSingleInstanceLock();
if (!gotTheLock) {
    app.quit();
} else {
    app.on('second-instance', () => {
        if (mainWindow) {
            mainWindow.show();
            mainWindow.focus();
        }
    });
}

// ── Start the embedded HTTP server (only if we own the lock) ───────────────
let serverModule = null;
if (gotTheLock) {
    // Inject resource path so server.js can find engine_host.ps1
    process.env.DUALAUDIO_RESOURCE_DIR = resourceBase;
    serverModule = require('./server');
}

// ── Window & Tray globals ──────────────────────────────────────────────────
let mainWindow = null;
let tray = null;

// ── Wait for server to be ready ────────────────────────────────────────────
function waitForServer(cb, retries = 20) {
    const port = serverModule ? serverModule.getPort() : 40880;
    http.get(`http://127.0.0.1:${port}/api/status`, (res) => {
        cb();
    }).on('error', () => {
        if (retries > 0) {
            setTimeout(() => waitForServer(cb, retries - 1), 300);
        } else {
            // Server took too long — open anyway
            cb();
        }
    });
}

function createWindow() {
    mainWindow = new BrowserWindow({
        width: 1160,
        height: 720,
        minWidth: 900,
        minHeight: 580,
        title: 'DualAudio Hub',
        backgroundColor: '#090b10',
        show: false,   // Show after page loads to avoid white flash
        webPreferences: {
            nodeIntegration: false,
            contextIsolation: true
        },
        autoHideMenuBar: true  // Hide the default Electron menu bar
    });

    // Try to set icon (won't crash if file missing)
    try {
        const iconPath = path.join(resourceBase, 'build', 'icon.ico');
        mainWindow.setIcon(iconPath);
    } catch (_) {}

    mainWindow.once('ready-to-show', () => {
        mainWindow.show();
    });

    const port = serverModule ? serverModule.getPort() : 40880;
    mainWindow.loadURL(`http://127.0.0.1:${port}`);

    // Minimise to tray on close (keep engine running)
    mainWindow.on('close', (e) => {
        if (!app.isQuitting) {
            e.preventDefault();
            mainWindow.hide();
        }
    });
}

function createTray() {
    // Build a tiny 16x16 PNG tray icon programmatically if no .ico
    let icon;
    try {
        icon = nativeImage.createFromPath(path.join(resourceBase, 'build', 'icon.ico'));
        icon = icon.resize({ width: 16, height: 16 });
    } catch (_) {
        icon = nativeImage.createEmpty();
    }

    tray = new Tray(icon);
    tray.setToolTip('DualAudio Hub — Dual BT Streamer');

    const contextMenu = Menu.buildFromTemplate([
        {
            label: 'Open DualAudio Hub',
            click: () => {
                if (mainWindow) {
                    mainWindow.show();
                    mainWindow.focus();
                }
            }
        },
        { type: 'separator' },
        {
            label: 'Quit',
            click: () => {
                app.isQuitting = true;
                app.quit();
            }
        }
    ]);

    tray.setContextMenu(contextMenu);
    tray.on('double-click', () => {
        if (mainWindow) {
            mainWindow.show();
            mainWindow.focus();
        }
    });
}

// ── App lifecycle ──────────────────────────────────────────────────────────
app.whenReady().then(() => {
    if (!gotTheLock) return; // another instance already owns the app

    createTray();

    // Wait for the embedded HTTP server to be ready before showing the window
    waitForServer(() => {
        createWindow();
    });

    app.on('activate', () => {
        if (BrowserWindow.getAllWindows().length === 0) createWindow();
    });
});

app.on('window-all-closed', () => {
    // Keep running in tray on Windows — only quit via tray menu
    if (process.platform !== 'darwin') {
        // Don't quit — stay in tray
    }
});

app.on('before-quit', () => {
    app.isQuitting = true;
});
