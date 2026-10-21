const { app, BrowserWindow, ipcMain, Menu, Tray, nativeImage, shell } = require('electron');
const path = require('node:path');
const { CodexUsageClient, normalizeUsage, readLocalSnapshot } = require('./codex-client.cjs');

let window;
let tray;
let refreshTimer;
const client = new CodexUsageClient();

function createWindow() {
  window = new BrowserWindow({
    width: 390,
    height: 600,
    useContentSize: true,
    minWidth: 350,
    minHeight: 540,
    show: false,
    title: 'Codex Usage',
    backgroundColor: '#10120f',
    titleBarStyle: process.platform === 'darwin' ? 'hiddenInset' : 'default',
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true
    }
  });
  window.loadFile(path.join(__dirname, 'renderer', 'index.html'));
  window.once('ready-to-show', () => window.show());
  window.on('close', (event) => {
    if (!app.isQuitting && tray) { event.preventDefault(); window.hide(); }
  });
}

function createTray() {
  // nativeImage cannot decode SVG data URLs, which left an empty (invisible)
  // tray icon that still occupied a slot. Use a real PNG from src/assets.
  const icon = nativeImage.createFromPath(path.join(__dirname, 'assets', 'tray.png'));
  if (icon.isEmpty()) console.warn('Tray icon missing: run npm run generate:icon');
  tray = new Tray(icon);
  tray.setToolTip('Codex Usage Widget');
  tray.setContextMenu(Menu.buildFromTemplate([
    { label: 'Show usage', click: () => { window.show(); window.focus(); } },
    { label: 'Refresh', click: () => refresh() },
    { type: 'separator' },
    { label: 'Quit', click: () => { app.isQuitting = true; app.quit(); } }
  ]));
  tray.on('click', () => { window.show(); window.focus(); });
}

async function refresh() {
  try {
    const usage = normalizeUsage(await client.readRateLimits());
    window?.webContents.send('usage:update', { ok: true, usage });
    return { ok: true, usage };
  } catch (error) {
    let result;
    try { result = { ok: true, usage: readLocalSnapshot(), warning: error.message }; }
    catch { result = { ok: false, error: error.message }; }
    window?.webContents.send('usage:update', result);
    return result;
  }
}

ipcMain.handle('usage:refresh', refresh);
ipcMain.handle('window:setAlwaysOnTop', (_event, value) => {
  window.setAlwaysOnTop(Boolean(value), 'floating');
  return window.isAlwaysOnTop();
});
ipcMain.handle('app:setLoginItem', (_event, value) => {
  app.setLoginItemSettings({ openAtLogin: Boolean(value) });
  return app.getLoginItemSettings().openAtLogin;
});
ipcMain.handle('app:getSettings', () => ({
  alwaysOnTop: window?.isAlwaysOnTop() ?? false,
  openAtLogin: app.getLoginItemSettings().openAtLogin
}));
ipcMain.handle('app:openUsage', () => shell.openExternal('https://chatgpt.com/#settings/Usage'));

app.whenReady().then(() => {
  createWindow();
  createTray();
  refresh();
  refreshTimer = setInterval(refresh, 60_000);
  app.on('activate', () => window?.show());
});
app.on('before-quit', () => { app.isQuitting = true; clearInterval(refreshTimer); client.close(); });
app.on('window-all-closed', () => { if (process.platform !== 'darwin' && !tray) app.quit(); });
