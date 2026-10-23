const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { normalizeUsage } = require('../src/codex-client.cjs');

function startApp(platform = 'linux') {
  const handlers = new Map();
  const state = { tooltips: [], updates: [], raw: { rateLimits: {
    primary: { usedPercent: 30 }, secondary: { usedPercent: 9 }
  } } };
  const electron = {
    app: {
      whenReady: () => ({ then: (callback) => { state.ready = callback; } }),
      on() {},
      getPath: () => { throw new Error('No saved settings'); }
    },
    BrowserWindow: class {
      constructor(options) {
        state.window = this;
        this.contentSize = [options.width, options.height];
        this.frameHeight = options.frame ? 30 : 0;
      }
      getContentSize() { return this.contentSize; }
      getSize() { return [this.contentSize[0], this.contentSize[1] + this.frameHeight]; }
      getBounds() { return { x: 0, y: 0, width: this.getSize()[0], height: this.getSize()[1] }; }
      setContentSize(width, height) { this.contentSize = [width, height]; }
      webContents = { send: (channel, result) => state.updates.push({ channel, result }) };
      alwaysOnTop = false;
      isAlwaysOnTop() { return this.alwaysOnTop; }
      setAlwaysOnTop(value) { this.alwaysOnTop = value; }
      setBackgroundColor(value) { state.background = value; }
      isVisible() { return !state.hidden; }
      removeAllListeners() {}
      destroy() { state.destroyed = true; }
      loadFile() {}
      on() {}
      once() {}
      hide() { state.hidden = true; }
    },
    ipcMain: { handle: (name, callback) => handlers.set(name, callback) },
    Menu: { buildFromTemplate: (items) => items },
    Tray: class {
      setToolTip(value) { state.tooltips.push(value); }
      setContextMenu(menu) { state.menu = menu; }
      on() {}
    },
    nativeImage: { createFromPath: () => ({ isEmpty: () => false }) },
    nativeTheme: {
      shouldUseDarkColors: true,
      set themeSource(value) { this.shouldUseDarkColors = value !== 'light'; },
      on() {}
    },
    shell: { openExternal: (url) => { state.openedUrl = url; } },
    screen: { getDisplayMatching: () => ({ workAreaSize: { height: state.availableHeight ?? 1080 } }) }
  };
  const context = vm.createContext({
    require: (name) => {
      if (name === 'electron') return electron;
      if (name === './codex-client.cjs') return {
        normalizeUsage,
        CodexUsageClient: class {
          async readRateLimits() {
            if (state.liveError) throw new Error('Live read failed');
            return state.raw;
          }
        },
        readLocalSnapshot: () => {
          if (!state.snapshot) throw new Error('No snapshot');
          return state.snapshot;
        }
      };
      return require(name);
    },
    __dirname: path.join(__dirname, '../src'),
    process: { platform },
    console,
    setInterval: (callback, delay) => { state.tick = callback; state.delay = delay; }
  });
  vm.runInContext(fs.readFileSync(path.join(__dirname, '../src/main.cjs'), 'utf8'), context);
  state.ready();
  state.handlers = handlers;
  state.manualRefresh = handlers.get('usage:refresh');
  state.tooltip = () => state.tooltips.at(-1);
  return state;
}

test('tray starts unavailable and refreshes on startup, hidden timer, and manual actions', async () => {
  const state = startApp();
  assert.equal(state.tooltip(), 'Codex Usage Widget\nUsage unavailable');
  // Allow the startup read to complete.
  await new Promise(setImmediate);
  assert.equal(state.tooltip(), 'Codex Usage Widget\n5-hour: 70% left\nWeekly: 91% left');
  state.window.hide();
  state.raw.rateLimits.primary.usedPercent = 42;
  assert.equal(state.delay, 60_000);
  await state.tick();
  assert.equal(state.hidden, true);
  assert.match(state.tooltip(), /5-hour: 58% left/);
  state.raw.rateLimits.secondary.usedPercent = 100;
  await state.manualRefresh();
  assert.match(state.tooltip(), /Weekly: 0% left/);
  state.raw.rateLimits.primary.usedPercent = 0;
  await state.manualRefresh();
  assert.match(state.tooltip(), /5-hour: 100% left/);
  assert.equal(state.updates.at(-1).channel, 'usage:update');
});

test('offline fallback is labeled stale, failure clears values, and recovery clears stale state', async () => {
  const state = startApp();
  await new Promise(setImmediate);
  state.liveError = true;
  state.snapshot = { source: 'local-cache', primary: { usedPercent: 44 }, secondary: null };
  const fallback = await state.manualRefresh();
  assert.equal(fallback.ok, true);
  assert.equal(state.tooltip(), 'Codex Usage Widget\nOffline snapshot — may be stale\n5-hour: 56% left\nWeekly: unavailable');
  state.snapshot = null;
  const failure = await state.tick();
  assert.equal(failure.ok, false);
  assert.equal(state.tooltip(), 'Codex Usage Widget\nUsage unavailable');
  state.liveError = false;
  await state.manualRefresh();
  assert.equal(state.tooltip(), 'Codex Usage Widget\n5-hour: 70% left\nWeekly: 91% left');
});

test('missing live windows are individually unavailable and replace previous percentages', async () => {
  const state = startApp();
  await new Promise(setImmediate);
  state.raw = { rateLimits: { secondary: { usedPercent: 15 } } };
  await state.manualRefresh();
  assert.equal(state.tooltip(), 'Codex Usage Widget\n5-hour: unavailable\nWeekly: 85% left');
  state.raw = { rateLimits: {} };
  await state.tick();
  assert.equal(state.tooltip(), 'Codex Usage Widget\n5-hour: unavailable\nWeekly: unavailable');
});

test('Linux menu exposes the latest usage when hover tooltips are unsupported', async () => {
  const state = startApp();
  const summary = () => Array.from(state.menu).filter((item) => item.enabled === false).map((item) => item.label);
  assert.deepEqual(summary(), ['Usage unavailable']);
  await new Promise(setImmediate);
  assert.deepEqual(summary(), ['5-hour: 70% left', 'Weekly: 91% left']);
  state.window.hide();
  state.raw.rateLimits.primary.usedPercent = 60;
  await state.tick();
  assert.deepEqual(summary(), ['5-hour: 40% left', 'Weekly: 91% left']);
  state.liveError = true;
  state.snapshot = { source: 'local-cache', primary: { usedPercent: 44 } };
  await state.manualRefresh();
  assert.deepEqual(summary(), ['Offline snapshot — may be stale', '5-hour: 56% left', 'Weekly: unavailable']);
  state.snapshot = null;
  await state.tick();
  assert.deepEqual(summary(), ['Usage unavailable']);
  state.liveError = false;
  await state.manualRefresh();
  assert.deepEqual(summary(), ['5-hour: 40% left', 'Weekly: 91% left']);
  assert.ok(!state.menu.some((item) => item.label === 'Show usage' || item.label === 'Refresh'));
  assert.ok(state.menu.some((item) => item.label === 'Quit'));
});

test('all platforms expose the requested controls followed by the current usage', async () => {
  for (const platform of ['linux', 'win32', 'darwin']) {
    const state = startApp(platform);
    await new Promise(setImmediate);
    assert.deepEqual(Array.from(state.menu.slice(0, 7), (item) => item.label || item.type), [
      'Always on top', 'Light theme', 'Compact mode', 'Open Codex Settings',
      'separator', '5-hour: 70% left', 'Weekly: 91% left'
    ]);
    state.raw.rateLimits.primary.usedPercent = 60;
    await state.tick();
    assert.equal(state.menu[5].label, '5-hour: 40% left');
  }
});

test('tray controls apply settings and stay synchronized with widget controls', async () => {
  const state = startApp();
  await new Promise(setImmediate);
  const item = (label) => state.menu.find((entry) => entry.label === label);
  for (const label of ['Always on top', 'Light theme', 'Compact mode']) {
    assert.equal(item(label).type, 'checkbox');
    assert.equal(item(label).checked, false);
  }
  item('Always on top').click({ checked: true });
  assert.equal(state.window.isAlwaysOnTop(), true);
  assert.equal(item('Always on top').checked, true);
  assert.equal(state.updates.at(-1).channel, 'settings:update');
  assert.equal(state.updates.at(-1).result.alwaysOnTop, true);
  item('Light theme').click({ checked: true });
  assert.equal(item('Light theme').checked, true);
  assert.equal(state.background, '#f5f7f0');
  assert.equal(state.updates.at(-1).result.theme, 'light');
  item('Light theme').click({ checked: false });
  assert.equal(item('Light theme').checked, false);
  assert.equal(state.background, '#10120f');
  state.window.hide();
  item('Compact mode').click({ checked: true });
  assert.equal(state.destroyed, true);
  assert.equal(item('Compact mode').checked, true);
  assert.equal(state.hidden, true);
  assert.equal(state.window.isAlwaysOnTop(), true);
  state.handlers.get('window:setMiniMode')(null, false);
  assert.equal(item('Compact mode').checked, false);
  state.handlers.get('window:setAlwaysOnTop')(null, false);
  assert.equal(item('Always on top').checked, false);
  state.handlers.get('app:setTheme')(null, 'light');
  assert.equal(item('Light theme').checked, true);
  item('Open Codex Settings').click();
  assert.equal(state.openedUrl, 'https://chatgpt.com/#settings/Usage');
});

test('full window grows to fit content and preserves larger user sizes', () => {
  const state = startApp();
  const fit = state.handlers.get('window:fitContent');
  fit(null, 659.5);
  assert.deepEqual(state.window.getContentSize(), [390, 660]);
  fit(null, 630);
  assert.deepEqual(state.window.getContentSize(), [390, 660]);
  state.window.setContentSize(450, 750);
  fit(null, 800);
  assert.deepEqual(state.window.getContentSize(), [450, 800]);
  for (const height of [NaN, Infinity, -10, 0, '900', null]) fit(null, height);
  assert.deepEqual(state.window.getContentSize(), [450, 800]);
});

test('content fitting respects the display work area and compact mode height', () => {
  const state = startApp();
  const fit = state.handlers.get('window:fitContent');
  state.availableHeight = 740;
  fit(null, 900);
  assert.deepEqual(state.window.getContentSize(), [390, 710]);
  state.handlers.get('window:setMiniMode')(null, true);
  fit(null, 900);
  assert.deepEqual(state.window.getContentSize(), [390, 46]);
  state.handlers.get('window:setMiniMode')(null, false);
  fit(null, 680);
  assert.deepEqual(state.window.getContentSize(), [390, 680]);
});
