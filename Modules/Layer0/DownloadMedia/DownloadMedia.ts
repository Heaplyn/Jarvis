// Developer: heaplyn
// Date: 2026-08-15
// Summary: Standalone Command Line Interface (CLI) script to download media via Lucida (Deezer/Tidal/etc.) or YT-DLP (YouTube/SoundCloud).

import { LucidaClient } from './lucida.js';
import { YtDlp } from 'ytdlp-nodejs';
import { ensureAllDependencies } from './setup.js';
import * as path from 'node:path';
import * as fs from 'node:fs';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
let downloadDir = path.resolve(__dirname, 'downloads');
let targetFormat = 'mp3';

async function download_yt(url: string) {
    console.log(`[INFO] Route: Native Extraction (yt-dlp) detected. Target: ${targetFormat}`);
    try {
        const isVideo = targetFormat === 'mp4' || targetFormat === 'mkv' || targetFormat === 'webm';
        const ytdlp = new YtDlp();

        // Configuration for high-quality extraction
        const options: any = {
            output: path.join(downloadDir, '%(title)s.%(ext)s'),
            noPlaylist: true,
            addMetadata: true,
            embedThumbnail: true,
        };

        if (isVideo) {
            options.format = `bestvideo[ext=${targetFormat}]+bestaudio/best`;
        } else {
            options.extractAudio = true;
            options.audioFormat = targetFormat;
            options.audioQuality = '0'; // Best
        }

        const result = await ytdlp.download(url, options).run();
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
                error: 'yt-dlp finished but output file was not found.'
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
    console.log('[INFO] Route: Browser Automation (Lucida) detected.');
    const client = new LucidaClient();
    return client.downloadTrack(url, downloadDir);
}

async function main() {
    // Ensure FlareSolverr and binaries are ready
    ensureAllDependencies();

    const args = process.argv.slice(2);
    if (args.length === 0) {
        console.log('❌ Error: Missing URL.');
        console.log('Usage: npx tsx DownloadMedia.ts <url> [directory] [format]');
        process.exit(1);
    }

    const url = args[0].trim();
    if (args[1]) downloadDir = path.resolve(args[1].trim());
    if (args[2]) targetFormat = args[2].trim().toLowerCase();

    if (!fs.existsSync(downloadDir)) {
        fs.mkdirSync(downloadDir, { recursive: true });
    }

    console.log(`[START] Task: "${url}"`);
    console.log(`[INFO] Output Folder: "${downloadDir}"`);

    try {
        const cleanUrl = url.toLowerCase();
        let result;

        // Routing logic
        const useYtDlp = cleanUrl.includes('youtube.com') ||
                         cleanUrl.includes('youtu.be') ||
                         cleanUrl.includes('youtube-nocookie.com') ||
                         cleanUrl.includes('soundcloud.com') ||
                         cleanUrl.includes('on.soundcloud.com') ||
                         cleanUrl.includes('snd.sc') ||
                         cleanUrl.includes('bandcamp.com') ||
                         cleanUrl.includes('vimeo.com') ||
                         cleanUrl.includes('twitch.tv');

        if (useYtDlp) {
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
            console.error(`Reason: ${result.error || 'Unknown Error.'}\n`);
            process.exit(1);
        }
    } catch (err: any) {
        console.error('\n❌ CRITICAL ERROR:');
        console.error(err.message || err);
        process.exit(1);
    }
}

main();