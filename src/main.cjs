const { app, BrowserWindow, ipcMain, Menu, Tray, nativeImage, nativeTheme, shell } = require('electron');
const path = require('node:path');
const fs = require('node:fs');
const { CodexUsageClient, normalizeUsage, readLocalSnapshot } = require('./codex-client.cjs');

let window;
let tray;
let refreshTimer;
const client = new CodexUsageClient();

const FULL_SIZE = { width: 390, height: 600 };
const MINI_SIZE = { width: 390, height: 46 }; // roughly the height of a Chrome tab strip
let miniMode = false;
let windowFrameless = false;
let windowHidden = false;
let pendingModeNotify = false;

function loadMode() {
  try {
    const file = path.join(app.getPath('userData'), 'widget-mode.json');
    miniMode = fs.readFileSync(file, 'utf8').trim() === 'mini';
  } catch { /* first run or unreadable: stay in full mode */ }
}

function saveMode() {
  try { fs.writeFileSync(path.join(app.getPath('userData'), 'widget-mode.json'), miniMode ? 'mini' : 'full'); }
  catch { /* settings are best-effort */ }
}

const themeSources = ['system', 'light', 'dark'];
let themeSource = 'system';

function loadTheme() {
  try {
    const saved = JSON.parse(fs.readFileSync(path.join(app.getPath('userData'), 'widget-theme.json'), 'utf8'));
    if (themeSources.includes(saved.theme)) themeSource = saved.theme;
  } catch {}
  return themeSource;
}

function saveTheme() {
  try {
    fs.mkdirSync(app.getPath('userData'), { recursive: true });
    fs.writeFileSync(path.join(app.getPath('userData'), 'widget-theme.json'), JSON.stringify({ theme: themeSource }));
  } catch {}
}

function backgroundForTheme() {
  return nativeTheme.shouldUseDarkColors ? '#10120f' : '#f5f7f0';
}

function applyTheme() {
  nativeTheme.themeSource = themeSource;
  window?.setBackgroundColor(backgroundForTheme());
}

function createWindow() {
  const size = miniMode ? MINI_SIZE : FULL_SIZE;
  window = new BrowserWindow({
    width: size.width,
    height: size.height,
    useContentSize: true,
    minWidth: miniMode ? 320 : 350,
    minHeight: miniMode ? 46 : 540,
    // Electron cannot change the native frame after creation, so mini mode
    // recreates the window frameless and lets the strip act as a drag handle.
    frame: !miniMode,
    resizable: !miniMode,
    maximizable: !miniMode,
    fullscreenable: !miniMode,
    skipTaskbar: miniMode,
    show: false,
    title: 'Codex Usage',
    backgroundColor: backgroundForTheme(),
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
  windowFrameless = miniMode;
  window.on('hide', () => { windowHidden = true; });
  window.on('show', () => { windowHidden = false; });
  window.once('ready-to-show', () => {
    if (!windowHidden) window.show();
    if (pendingModeNotify) { pendingModeNotify = false; window.webContents.send('mode:update', miniMode); }
  });
  window.on('close', (event) => {
    if (!app.isQuitting && tray) { event.preventDefault(); window.hide(); }
  });
}

// Electron has no runtime frame toggle, so switching modes rebuilds the window.
function applyMode() {
  if (!window) return;
  if (windowFrameless !== miniMode) {
    const wasVisible = window.isVisible();
    const wasOnTop = window.isAlwaysOnTop();
    window.removeAllListeners('close');
    window.destroy();
    createWindow();
    if (wasOnTop) window.setAlwaysOnTop(true, 'floating');
    if (!wasVisible) window.hide();
    pendingModeNotify = true; // send once the new renderer has loaded
  } else {
    const size = miniMode ? MINI_SIZE : FULL_SIZE;
    window.setContentSize(size.width, size.height);
    window?.webContents.send('mode:update', miniMode);
  }
}

function trayMenu() {
  return Menu.buildFromTemplate([
    { label: 'Show usage', click: () => { window.show(); window.focus(); } },
    { label: 'Refresh', click: () => refresh() },
    { label: miniMode ? 'Exit mini mode' : 'Mini mode', click: () => {
      miniMode = !miniMode; saveMode(); applyMode();
      tray.setContextMenu(trayMenu());
    } },
    { type: 'separator' },
    { label: 'Quit', click: () => { app.isQuitting = true; app.quit(); } }
  ]);
}

function createTray() {
  // nativeImage cannot decode SVG data URLs, which left an empty (invisible)
  // tray icon that still occupied a slot. Use a real PNG from src/assets.
  const icon = nativeImage.createFromPath(path.join(__dirname, 'assets', 'tray.png'));
  if (icon.isEmpty()) console.warn('Tray icon missing: run npm run generate:icon');
  tray = new Tray(icon);
  tray.setToolTip('Codex Usage Widget');
  tray.setContextMenu(trayMenu());
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
ipcMain.handle('window:setMiniMode', (_event, value) => {
  miniMode = Boolean(value);
  saveMode();
  applyMode();
  return miniMode;
});
ipcMain.handle('window:setAlwaysOnTop', (_event, value) => {
  window.setAlwaysOnTop(Boolean(value), 'floating');
  return window.isAlwaysOnTop();
});
ipcMain.handle('app:setLoginItem', (_event, value) => {
  app.setLoginItemSettings({ openAtLogin: Boolean(value) });
  return app.getLoginItemSettings().openAtLogin;
});
ipcMain.handle('app:setTheme', (_event, value) => {
  themeSource = themeSources.includes(value) ? value : 'system';
  saveTheme();
  applyTheme();
  return themeSource;
});
ipcMain.handle('app:getSettings', () => ({
  alwaysOnTop: window?.isAlwaysOnTop() ?? false,
  openAtLogin: app.getLoginItemSettings().openAtLogin,
  mini: miniMode,
  theme: themeSource
}));
ipcMain.handle('app:openUsage', () => shell.openExternal('https://chatgpt.com/#settings/Usage'));

app.whenReady().then(() => {
  loadMode();
  loadTheme();
  applyTheme();
  nativeTheme.on('updated', () => window?.setBackgroundColor(backgroundForTheme()));
  createWindow();
  createTray();
  refresh();
  refreshTimer = setInterval(refresh, 60_000);
  app.on('activate', () => window?.show());
});
app.on('before-quit', () => { app.isQuitting = true; clearInterval(refreshTimer); client.close(); });
app.on('window-all-closed', () => { if (process.platform !== 'darwin' && !tray) app.quit(); });
