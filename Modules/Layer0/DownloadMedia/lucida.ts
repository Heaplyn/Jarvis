import { chromium, Cookie } from 'playwright';
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
        // Default download directory
        const downloadDir = path.resolve(__dirname, 'downloads');
        if (!fs.existsSync(downloadDir)) {
            fs.mkdirSync(downloadDir, { recursive: true });
        }
    }

    /**
     * Attempts to query FlareSolverr to bypass Cloudflare Turnstile and fetch active session cookies
     */
    public async bypassCloudflare(): Promise<boolean> {
        console.log('Attempting to bypass Cloudflare protection using FlareSolverr...');
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
                throw new Error(`Server returned HTTP ${response.status}`);
            }

            const data: any = await response.json();
            
            if (data.status === 'ok' && data.solution) {
                // Save User-Agent from FlareSolverr session
                if (data.solution.userAgent) {
                    this.userAgent = data.solution.userAgent;
                }

                // Map cookies to Playwright format
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

                console.log('✓ Successfully bypassed Cloudflare! Cookies cached.');
                return true;
            }
            return false;
        } catch (e: any) {
            console.log(`✗ Could not connect to FlareSolverr: ${e.message || e}. Falling back to clean browser session.`);
            return false;
        }
    }

    /**
     * Navigates to Lucida.to via Playwright, solves the Turnstile captcha, and downloads the file
     */
    public async downloadTrack(url: string, outputDir?: string): Promise<{ success: boolean; filepath?: string; size?: number; error?: string }> {
        const downloadDir = outputDir || path.resolve(__dirname, '../../downloads');
        
        // 1. Fetch fresh cookies from FlareSolverr
        await this.bypassCloudflare();

        // 2. Launch browser with persistent User Data Directory (preserves Google and media account logins)
        const userDataDir = path.resolve(__dirname, 'user_data');
        if (!fs.existsSync(userDataDir)) {
            fs.mkdirSync(userDataDir, { recursive: true });
        }

        const context = await chromium.launchPersistentContext(userDataDir, {
            headless: false, // Visible window allows completing interactive Google Login when prompted
            acceptDownloads: true,
            userAgent: this.userAgent,
            args: ['--disable-blink-features=AutomationControlled', '--no-first-run', '--no-default-browser-check']
        });

        // 3. Inject FlareSolverr cookies
        if (this.cookies.length > 0) {
            await context.addCookies(this.cookies);
        }

        const page = context.pages().length > 0 ? context.pages()[0] : await context.newPage();

        try {
            // URL-encode target link
            const encodedUrl = encodeURIComponent(url);
            const lucidaUrl = `${this.baseUrl}/?url=${encodedUrl}`;
            
            console.log(`Navigating browser to: ${lucidaUrl}`);
            await page.goto(lucidaUrl, { waitUntil: 'domcontentloaded', timeout: 30000 });

            // 4. Wait for the download button (Max 20 seconds)
            // Use a resilient multi-selector in case lucida.to updates its DOM
            const downloadBtnSelector = 'button.download-button, a[download], button:has-text("download")';
            console.log('Waiting for download button to appear...');
            await page.waitForSelector(downloadBtnSelector, { timeout: 20000 });

            // Save a debug screenshot before clicking (good for troubleshooting)
            await page.screenshot({ path: path.join(downloadDir, 'debug_before_click.png') });

            // 5. Start download event listener and click the button
            console.log('Clicking download button...');
            const [download] = await Promise.all([
                page.waitForEvent('download', { timeout: 90000 }), // Wait up to 90 seconds for download start (processing time)
                page.locator(downloadBtnSelector).first().click()
            ]);

            // 6. Save the downloaded file
            const filename = download.suggestedFilename();
            const filepath = path.join(downloadDir, filename);
            console.log(`Downloading file to: ${filepath}`);
            await download.saveAs(filepath);

            await context.close();

            const stats = fs.statSync(filepath);
            return {
                success: true,
                filepath: filepath,
                size: stats.size
            };

        } catch (e: any) {
            // Save error screenshot of the failure
            try {
                await page.screenshot({ path: path.join(downloadDir, 'debug_error.png') });
            } catch {}

            await context.close();
            return {
                success: false,
                error: `Browser automation error: ${e.message || e}`
            };
        }
    }
}
