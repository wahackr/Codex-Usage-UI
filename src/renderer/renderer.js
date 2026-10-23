const $ = (id) => document.getElementById(id);
let refreshing = false;
let mini = false;
const themes = ['system', 'light', 'dark'];

// Measure the natural content height, including padding and wrapped footer
// text. Watching main avoids a resize loop when the window itself grows.
const contentObserver = new ResizeObserver(([entry]) => {
  if (!mini) window.codexUsage.fitContent(entry.target.getBoundingClientRect().height);
});
contentObserver.observe(document.querySelector('main'));

function setMini(value) {
  mini = Boolean(value);
  document.body.classList.toggle('mini', mini);
  $('strip').setAttribute('aria-hidden', String(!mini));
  $('mini').title = mini ? 'Exit mini mode' : 'Mini mode';
}

function renderTheme(active) {
  for (const button of $('themePicker').children) {
    button.classList.toggle('active', button.dataset.theme === active);
  }
}

function relativeReset(timestamp) {
  if (!timestamp) return 'Reset time unavailable';
  const diff = timestamp - Date.now();
  if (diff <= 0) return 'Reset due now';
  const minutes = Math.ceil(diff / 60_000);
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  const mins = minutes % 60;
  const relative = days ? `${days}d ${hours}h` : hours ? `${hours}h ${mins}m` : `${mins}m`;
  const exact = new Intl.DateTimeFormat(undefined, { weekday: 'short', hour: 'numeric', minute: '2-digit' }).format(timestamp);
  return `Resets in ${relative} · ${exact}`;
}

function renderWindow(key, value) {
  const chip = document.querySelector(`.stripChip[data-key="${key}"]`);
  if (!value) {
    chip.querySelector('b').textContent = '—';
    chip.className = 'stripChip';
    return;
  }
  const used = value.usedPercent;
  const remaining = Math.max(0, 100 - used);
  $(`${key}Remaining`).textContent = `${remaining}% left`;
  $(`${key}Left`).textContent = `${remaining}% left`;
  const bar = $(`${key}Bar`);
  bar.style.width = `${remaining}%`;
  bar.className = `bar${remaining <= 10 ? ' danger' : remaining <= 30 ? ' warn' : ''}`;
  $(`${key}Reset`).textContent = relativeReset(value.resetsAt);
  chip.querySelector('b').textContent = `${remaining}%`;
  chip.className = `stripChip${remaining <= 10 ? ' danger' : remaining <= 30 ? ' warn' : ''}`;
}

function render(result) {
  const status = $('status');
  const dot = $('stripDot');
  if (!result.ok) {
    status.className = 'status error';
    dot.className = 'stripDot error';
    $('statusText').textContent = result.error || 'Unable to read Codex usage';
    $('updated').textContent = 'Install Codex CLI and sign in with your ChatGPT account.';
    $('stripReset').textContent = 'No data';
    return;
  }
  const usage = result.usage;
  status.className = usage.source === 'live' ? 'status live' : 'status';
  dot.className = usage.source === 'live' ? 'stripDot live' : 'stripDot';
  $('statusText').textContent = usage.source === 'live'
    ? (usage.ordinaryUsageAllowed === false ? 'Included usage paused' : 'Live account usage')
    : 'Offline snapshot · may be stale';
  renderWindow('primary', usage.primary);
  renderWindow('secondary', usage.secondary);
  $('plan').textContent = usage.planType;
  $('credits').textContent = usage.credits?.unlimited ? 'Unlimited' : usage.credits?.hasCredits ? (usage.credits.balance || 'Available') : 'None';
  $('resetCredits').textContent = String(usage.resetCredits);
  $('updated').textContent = `Updated ${new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit', second: '2-digit' }).format(usage.fetchedAt)} · ${usage.source === 'live' ? 'live from OpenAI' : 'local Codex cache'}`;
  const resets = [usage.primary?.resetsAt, usage.secondary?.resetsAt].filter(Boolean).sort((a, b) => a - b);
  $('stripReset').textContent = shortReset(resets[0]);
}

function shortReset(timestamp) {
  if (!timestamp) return '—';
  const diff = timestamp - Date.now();
  if (diff <= 0) return 'reset now';
  const minutes = Math.ceil(diff / 60_000);
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  return days ? `${days}d ${hours}h` : hours ? `${hours}h ${minutes % 60}m` : `${minutes}m`;
}

async function refresh() {
  if (refreshing) return;
  refreshing = true; $('refresh').classList.add('loading');
  try { render(await window.codexUsage.refresh()); }
  finally { refreshing = false; $('refresh').classList.remove('loading'); }
}

$('refresh').addEventListener('click', refresh);
$('mini').addEventListener('click', () => window.codexUsage.setMiniMode(!mini));
$('expand').addEventListener('click', () => window.codexUsage.setMiniMode(false));
$('openUsage').addEventListener('click', () => window.codexUsage.openUsage());
$('alwaysOnTop').addEventListener('change', (event) => window.codexUsage.setAlwaysOnTop(event.target.checked));
$('openAtLogin').addEventListener('change', (event) => window.codexUsage.setOpenAtLogin(event.target.checked));
$('themePicker').addEventListener('click', async (event) => {
  const theme = event.target.dataset.theme;
  if (!themes.includes(theme)) return;
  renderTheme(await window.codexUsage.setTheme(theme));
});
window.codexUsage.onUpdate(render);
window.codexUsage.onModeUpdate(setMini);
window.codexUsage.onSettingsUpdate((settings) => {
  $('alwaysOnTop').checked = settings.alwaysOnTop;
  renderTheme(settings.theme);
});
window.codexUsage.getSettings().then((settings) => {
  $('alwaysOnTop').checked = settings.alwaysOnTop;
  $('openAtLogin').checked = settings.openAtLogin;
  setMini(settings.mini);
  renderTheme(settings.theme || 'system');
});
refresh();
