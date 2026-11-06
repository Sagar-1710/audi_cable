/**
 * DualAudio Hub - Live Audio Streaming Server
 * Bridges Web UI to the C# WASAPI DualAudioEngine
 */

const http = require('http');
const fs = require('fs');
const path = require('path');
const { spawn, exec } = require('child_process');
const url = require('url');

const BASE_PORT = 40880;
const MAX_PORT_ATTEMPTS = 20;
let activePort = BASE_PORT;
const PUBLIC_DIR = path.join(__dirname, 'public');

// When running inside Electron packaged app, engine scripts live in resourcesPath
const RESOURCE_DIR = process.env.DUALAUDIO_RESOURCE_DIR || __dirname;

let engineProcess = null;
let commandQueue = [];
let lastKnownStatus = {
    isRunning: false,
    peakL: 0,
    peakR: 0,
    processedFrames: 0,
    lastError: ''
};

function initEngineProcess() {
    console.log('[DualAudio] Spawning C# WASAPI Audio Engine host...');
    const psScript = path.join(RESOURCE_DIR, 'engine_host.ps1');

    engineProcess = spawn('powershell.exe', [
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', psScript
    ], {
        stdio: ['pipe', 'pipe', 'inherit'],
        windowsHide: true
    });

    let stdoutBuffer = '';

    engineProcess.stdout.on('data', (chunk) => {
        stdoutBuffer += chunk.toString('utf8');
        const lines = stdoutBuffer.split('\n');
        stdoutBuffer = lines.pop();

        for (const rawLine of lines) {
            const line = rawLine.trim();
            if (!line) continue;
            try {
                const parsed = JSON.parse(line);
                handleEngineMessage(parsed);
            } catch (err) {
                console.log('[Engine Debug]', line);
            }
        }
    });

    engineProcess.on('exit', (code) => {
        console.warn(`[DualAudio] Engine host exited with code ${code}. Respawning in 3s...`);
        lastKnownStatus.isRunning = false;
        setTimeout(initEngineProcess, 3000);
    });

    setInterval(() => {
        if (engineProcess && engineProcess.stdin.writable) {
            sendCommand('GET_STATUS');
        }
    }, 100);
}

function handleEngineMessage(msg) {
    if (msg.event === 'ready') {
        console.log('[DualAudio] Audio Engine is READY and initialized!');
        return;
    }

    if (msg.status && msg.status.isRunning !== undefined) {
        lastKnownStatus = msg.status;
    }

    if (commandQueue.length > 0) {
        const item = commandQueue.shift();
        if (item && item.resolve) {
            item.resolve(msg);
        }
    }
}

function sendCommand(cmdStr) {
    return new Promise((resolve, reject) => {
        if (!engineProcess || !engineProcess.stdin.writable) {
            return reject(new Error('Audio engine process is not available'));
        }
        commandQueue.push({ resolve, reject, timestamp: Date.now() });
        engineProcess.stdin.write(cmdStr + '\n');
    });
}

// HTTP Server
const server = http.createServer(async (req, res) => {
    const parsedUrl = url.parse(req.url, true);
    const pathname = parsedUrl.pathname;

    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

    if (req.method === 'OPTIONS') {
        res.writeHead(204);
        res.end();
        return;
    }

    if (pathname === '/api/devices' && req.method === 'GET') {
        try {
            const reply = await sendCommand('GET_DEVICES');
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify(reply));
        } catch (err) {
            res.writeHead(500, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ error: err.message }));
        }
        return;
    }

    if (pathname === '/api/status' && req.method === 'GET') {
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ status: 'ok', data: lastKnownStatus }));
        return;
    }

    if (pathname === '/api/start' && req.method === 'POST') {
        let body = '';
        req.on('data', chunk => body += chunk);
        req.on('end', async () => {
            try {
                const data = JSON.parse(body || '{}');
                const src = data.sourceId || 'default';
                const t1 = data.target1Id || '';
                const t2 = data.target2Id || '';

                const cmd = `START ${src}|${t1}|${t2}`;
                const reply = await sendCommand(cmd);
                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify(reply));
            } catch (err) {
                res.writeHead(500, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ error: err.message }));
            }
        });
        return;
    }

    if (pathname === '/api/stop' && req.method === 'POST') {
        try {
            const reply = await sendCommand('STOP');
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify(reply));
        } catch (err) {
            res.writeHead(500, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ error: err.message }));
        }
        return;
    }

    // Static Files
    let filePath = path.join(PUBLIC_DIR, pathname === '/' ? 'index.html' : pathname);
    if (!filePath.startsWith(PUBLIC_DIR)) {
        res.writeHead(403);
        res.end('Access Denied');
        return;
    }

    fs.stat(filePath, (err, stats) => {
        if (err || !stats.isFile()) {
            res.writeHead(404, { 'Content-Type': 'text/plain' });
            res.end('404 Not Found');
            return;
        }

        const ext = path.extname(filePath).toLowerCase();
        const contentTypes = {
            '.html': 'text/html; charset=utf-8',
            '.css': 'text/css; charset=utf-8',
            '.js': 'application/javascript; charset=utf-8'
        };

        res.writeHead(200, { 'Content-Type': contentTypes[ext] || 'application/octet-stream' });
        fs.createReadStream(filePath).pipe(res);
    });
});

initEngineProcess();

// If the port is taken (leftover process, another app), fall back to the
// next port instead of crashing the Electron main process with EADDRINUSE.
server.on('error', (err) => {
    if (err && err.code === 'EADDRINUSE' && activePort < BASE_PORT + MAX_PORT_ATTEMPTS - 1) {
        console.warn(`[DualAudio] Port ${activePort} is busy — trying ${activePort + 1}...`);
        activePort += 1;
        server.listen(activePort, '127.0.0.1');
    } else if (err) {
        console.error('[DualAudio] HTTP server error:', err.message);
    }
});

server.listen(activePort, '127.0.0.1', () => {
    console.log(`======================================================`);
    console.log(` 🎧 DualAudio Hub (Virtual Audio Cable) Live!         `);
    console.log(` Open Dashboard: http://127.0.0.1:${activePort}             `);
    console.log(`======================================================`);
});

// main.js reads the actually-bound port back
module.exports = { getPort: () => activePort };
