const $ = (id) => document.getElementById(id);
let refreshing = false;

function relativeReset(timestamp) {
  if (!timestamp) return 'Reset time unavailable';
  const diff = timestamp - Date.now();
  if (diff <= 0) return 'Reset due now';
  const minutes = Math.ceil(diff / 60_000);
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  const mins = minutes % 60;
  const relative = days ? `${days}d ${hours}h` : hours ? `${hours}h ${mins}m` : `${mins}m`;
  const exact = new Intl.DateTimeFormat(undefined, { weekday:'short', hour:'numeric', minute:'2-digit' }).format(timestamp);
  return `Resets in ${relative} · ${exact}`;
}

function renderWindow(key, value) {
  if (!value) return;
  const used = value.usedPercent;
  const remaining = Math.max(0, 100 - used);
  $(`${key}Remaining`).textContent = `${remaining}% left`;
  $(`${key}Left`).textContent = `${remaining}% left`;
  const bar = $(`${key}Bar`);
  bar.style.width = `${remaining}%`;
  bar.className = `bar${remaining <= 10 ? ' danger' : remaining <= 30 ? ' warn' : ''}`;
  $(`${key}Reset`).textContent = relativeReset(value.resetsAt);
}

function render(result) {
  const status = $('status');
  if (!result.ok) {
    status.className = 'status error';
    $('statusText').textContent = result.error || 'Unable to read Codex usage';
    $('updated').textContent = 'Install Codex CLI and sign in with your ChatGPT account.';
    return;
  }
  const usage = result.usage;
  status.className = usage.source === 'live' ? 'status live' : 'status';
  $('statusText').textContent = usage.source === 'live'
    ? (usage.ordinaryUsageAllowed === false ? 'Included usage paused' : 'Live account usage')
    : 'Offline snapshot · may be stale';
  renderWindow('primary', usage.primary);
  renderWindow('secondary', usage.secondary);
  $('plan').textContent = usage.planType;
  $('credits').textContent = usage.credits?.unlimited ? 'Unlimited' : usage.credits?.hasCredits ? (usage.credits.balance || 'Available') : 'None';
  $('resetCredits').textContent = String(usage.resetCredits);
  $('updated').textContent = `Updated ${new Intl.DateTimeFormat(undefined, { hour:'numeric', minute:'2-digit', second:'2-digit' }).format(usage.fetchedAt)} · ${usage.source === 'live' ? 'live from OpenAI' : 'local Codex cache'}`;
}

async function refresh() {
  if (refreshing) return;
  refreshing = true; $('refresh').classList.add('loading');
  try { render(await window.codexUsage.refresh()); }
  finally { refreshing = false; $('refresh').classList.remove('loading'); }
}

$('refresh').addEventListener('click', refresh);
$('openUsage').addEventListener('click', () => window.codexUsage.openUsage());
$('alwaysOnTop').addEventListener('change', (event) => window.codexUsage.setAlwaysOnTop(event.target.checked));
$('openAtLogin').addEventListener('change', (event) => window.codexUsage.setOpenAtLogin(event.target.checked));
window.codexUsage.onUpdate(render);
window.codexUsage.getSettings().then((settings) => {
  $('alwaysOnTop').checked = settings.alwaysOnTop;
  $('openAtLogin').checked = settings.openAtLogin;
});
refresh();
