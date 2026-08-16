// Developer: heaplyn
// Date: 2026-08-09
// Summary: Cross-platform FlareSolverr, yt-dlp, and FFmpeg setup trigger that configures all necessary binaries.

import { execSync } from 'child_process';
import os from 'os';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const targetDir = path.join(__dirname, 'flaresolverr');

export function ensureFlareSolverr() {
    if (fs.existsSync(targetDir) && fs.readdirSync(targetDir).length > 0) {
        return;
    }

    console.log('⚡ [SETUP] FlareSolverr is missing or incomplete. Starting automated installer...');

    try {
        if (os.platform() === 'win32') {
            const psScript = path.join(__dirname, 'install_flaresolverr.ps1');
            execSync(`powershell -ExecutionPolicy Bypass -File "${psScript}"`, { stdio: 'inherit' });
        } else {
            const shScript = path.join(__dirname, 'install_flaresolverr.sh');
            execSync(`chmod +x "${shScript}" && bash "${shScript}"`, { stdio: 'inherit' });
        }
    } catch (error) {
        console.error('❌ [SETUP] FlareSolverr installation failed:', error.message || error);
    }
}

export function ensureYtDlpAndFfmpeg() {
    console.log('⚡ [SETUP] Checking yt-dlp and FFmpeg binaries...');
    try {
        // Run update to fetch the latest yt-dlp binary
        console.log('📦 Fetching/Updating yt-dlp binary...');
        execSync('npx ytdlp update', { stdio: 'inherit' });

        // Run ffmpeg download to fetch FFmpeg binaries
        console.log('📦 Fetching/Updating FFmpeg binaries...');
        execSync('npx ytdlp ffmpeg', { stdio: 'inherit' });

        console.log('✅ [SETUP] yt-dlp and FFmpeg configured successfully!');
    } catch (error) {
        console.error('❌ [SETUP] Failed to configure yt-dlp/FFmpeg binaries:', error.message || error);
    }
}

export function ensureAllDependencies() {
    ensureFlareSolverr();
    ensureYtDlpAndFfmpeg();

    // Ensure node_modules are present
    if (!fs.existsSync(path.join(__dirname, 'node_modules'))) {
        console.log('📦 [SETUP] Installing local Node.js dependencies...');
        try {
            execSync('npm install', { cwd: __dirname, stdio: 'inherit' });
        } catch (e) {
            console.error('❌ [SETUP] Failed to install Node.js dependencies:', e.message);
        }
    }
}

// Support running directly via: node setup.js
if (process.argv[1] === __filename) {
    ensureAllDependencies();
}
