// Developer: heaplyn
// Date: 2026-08-09
// Summary: Standalone Command Line Interface (CLI) script to download media via Lucida (Deezer/Tidal/etc.) or YT-DLP (YouTube) inside Jarvis Layer 0.

import { LucidaClient } from './lucida.js';
import { YtDlp } from 'ytdlp-nodejs';
import { ensureFlareSolverr } from './setup.js';
import * as path from 'node:path';
import * as fs from 'node:fs';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const downloadDir = path.resolve(__dirname, 'downloads');

// Ensure downloads directory exists
if (!fs.existsSync(downloadDir)) {
    fs.mkdirSync(downloadDir, { recursive: true });
}

async function download_yt(url: string) {
    console.log('[INFO] Route: YouTube URL detected. Initializing extraction via yt-dlp...');
    try {
        const ytdlp = new YtDlp();
        const result = await ytdlp
            .download(url, {
                output: path.join(downloadDir, '%(title)s.%(ext)s'),
                extractAudio: true,
                audioFormat: 'mp3',
                audioQuality: '0',
                noPlaylist: true
            })
            .run();

        const filepath = result.filePaths[0];
        
        if (filepath && fs.existsSync(filepath)) {
            const stats = fs.statSync(filepath);
            return {
                success: true,
                filepath: filepath,
                size: stats.size
            };
        } else {
            return {
                success: false,
                error: 'Download finished, but output file could not be found on disk.'
            };
        }
    } catch (err: any) {
        return {
            success: false,
            error: err.message || err.toString()
        };
    }
}

async function download_lucida(url: string) {
    console.log('[INFO] Route: Non-YouTube URL detected. Initializing browser automation via Lucida...');
    const client = new LucidaClient();
    return client.downloadTrack(url, downloadDir);
}

async function main() {
    // Ensure all required system dependencies (like FlareSolverr) are pre-installed
    ensureFlareSolverr();

    const args = process.argv.slice(2);
    if (args.length === 0) {
        console.log('❌ Error: Missing URL parameter.');
        console.log('Usage: npx tsx DownloadMedia.ts <url>');
        console.log('Example: npx tsx DownloadMedia.ts "https://www.deezer.com/track/1435235"');
        process.exit(1);
    }

    const url = args[0].trim();
    console.log(`[INFO] Starting download task for target: "${url}"`);

    try {
        const cleanUrl = url.toLowerCase();
        let result;

        if (cleanUrl.includes('youtube.com') || cleanUrl.includes('youtu.be') || cleanUrl.includes('youtube-nocookie.com')) {
            result = await download_yt(url);
        } else {
            result = await download_lucida(url);
        }

        if (result.success && result.filepath) {
            const filename = path.basename(result.filepath);
            const sizeMb = (result.size || 0) / (1024 * 1024);
            console.log('\n==================================================');
            console.log('🎉 DOWNLOAD SUCCESSFUL!');
            console.log(`📁 File: ${filename}`);
            console.log(`📍 Path: ${result.filepath}`);
            console.log(`⚡ Size: ${sizeMb.toFixed(2)} MB`);
            console.log('==================================================\n');
        } else {
            console.error('\n❌ DOWNLOAD FAILED!');
            console.error(`Reason: ${result.error || 'Unknown extraction error.'}\n`);
            process.exit(1);
        }
    } catch (err: any) {
        console.error('\n❌ CRITICAL PROCESS EXCEPTION:');
        console.error(err.message || err);
        console.error('\n');
        process.exit(1);
    }
}

main();
