import { chromium, BrowserContext, Page, Cookie } from 'playwright';
import * as path from 'node:path';
import * as fs from 'node:fs';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

interface FlareSolverrCookie {
    name: string;
    value: string;
    domain: string;
    path?: string;
    expiry?: number;
    httpOnly?: boolean;
    secure?: boolean;
}

export class LucidaClient {
    private baseUrl = 'https://lucida.to';
    private flaresolverrUrl = 'http://localhost:8191/v1';
    private cookies: Cookie[] = [];
    private userAgent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36';

    constructor() {
        const downloadDir = path.resolve(__dirname, 'downloads');
        if (!fs.existsSync(downloadDir)) {
            fs.mkdirSync(downloadDir, { recursive: true });
        }
    }

    /**
     * Attempts to query FlareSolverr to bypass Cloudflare and fetch session cookies
     */
    public async bypassCloudflare(): Promise<boolean> {
        console.log('[Lucida] Attempting to bypass Cloudflare via FlareSolverr...');
        try {
            const response = await fetch(this.flaresolverrUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    cmd: 'request.get',
                    url: this.baseUrl,
                    maxTimeout: 60000
                })
            });

            if (!response.ok) {
                throw new Error(`FlareSolverr returned HTTP ${response.status}`);
            }

            const data: any = await response.json();
            
            if (data.status === 'ok' && data.solution) {
                if (data.solution.userAgent) {
                    this.userAgent = data.solution.userAgent;
                }

                const rawCookies: FlareSolverrCookie[] = data.solution.cookies || [];
                this.cookies = rawCookies.map(cookie => ({
                    name: cookie.name,
                    value: cookie.value,
                    domain: cookie.domain.startsWith('.') ? cookie.domain : `.${cookie.domain}`,
                    path: cookie.path || '/',
                    expires: cookie.expiry || -1,
                    httpOnly: cookie.httpOnly || false,
                    secure: cookie.secure || false,
                    sameSite: 'Lax' as const
                }));

                console.log('✓ [Lucida] Successfully bypassed Cloudflare! Cookies cached.');
                return true;
            }
            return false;
        } catch (e: any) {
            console.log(`✗ [Lucida] FlareSolverr unavailable (${e.message}). Using clean browser.`);
            return false;
        }
    }

    /**
     * Downloads a track via Lucida.to automation
     */
    public async downloadTrack(url: string, outputDir?: string): Promise<{ success: boolean; filepath?: string; size?: number; error?: string }> {
        const downloadDir = outputDir || path.resolve(__dirname, 'downloads');
        
        await this.bypassCloudflare();

        const browser = await chromium.launch({
            headless: true,
            args: [
                '--disable-blink-features=AutomationControlled',
                '--no-sandbox',
                '--disable-setuid-sandbox'
            ]
        });

        const context = await browser.newContext({
            acceptDownloads: true,
            userAgent: this.userAgent,
            viewport: { width: 1280, height: 720 }
        });

        await context.addInitScript(() => {
            Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
        });

        if (this.cookies.length > 0) {
            await context.addCookies(this.cookies);
        }

        const page = await context.newPage();

        try {
            const encodedUrl = encodeURIComponent(url);
            const lucidaUrl = `${this.baseUrl}/?url=${encodedUrl}`;
            
            console.log(`[Lucida] Navigating to: ${lucidaUrl}`);
            await page.goto(lucidaUrl, { waitUntil: 'networkidle', timeout: 60000 });

            const title = await page.title();
            if (title.includes('Just a moment') || title.includes('Cloudflare')) {
                console.log('[Lucida] Stuck on Cloudflare. Taking screenshot and failing...');
                await page.screenshot({ path: path.join(downloadDir, 'cloudflare_stuck.png') });
                throw new Error('Cloudflare bypass failed in browser.');
            }

            console.log('[Lucida] Waiting for metadata fetch and download button...');

            const errorElement = await page.$('.error-message, .alert-error');
            if (errorElement) {
                const errorText = await errorElement.innerText();
                throw new Error(`Lucida Error: ${errorText}`);
            }

            await page.waitForSelector('button:has-text("download"), .download-button', { timeout: 45000 });

            const btn = page.locator('button:has-text("download"), .download-button').first();
            await btn.waitFor({ state: 'visible' });

            console.log('[Lucida] Clicking download button...');
            const [download] = await Promise.all([
                page.waitForEvent('download', { timeout: 120000 }),
                btn.click()
            ]);

            const filename = download.suggestedFilename();
            const filepath = path.join(downloadDir, filename);
            console.log(`[Lucida] Saving file: ${filename}`);
            await download.saveAs(filepath);

            await browser.close();

            if (fs.existsSync(filepath)) {
                const stats = fs.statSync(filepath);
                return { success: true, filepath, size: stats.size };
            } else {
                throw new Error('File saved but could not be verified on disk.');
            }

        } catch (e: any) {
            console.error(`[Lucida] Error: ${e.message}`);
            try {
                await page.screenshot({ path: path.join(downloadDir, `error_${Date.now()}.png`) });
            } catch {}
            await browser.close();
            return { success: false, error: e.message };
        }
    }
}
