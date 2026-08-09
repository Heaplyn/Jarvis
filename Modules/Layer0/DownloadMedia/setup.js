// Developer: heaplyn
// Date: 2026-08-09
// Summary: Cross-platform FlareSolverr setup trigger that executes the correct platform installation script.

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

// Support running directly via: node setup.js
if (process.argv[1] === __filename) {
    ensureFlareSolverr();
}
