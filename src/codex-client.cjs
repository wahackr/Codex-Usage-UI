const { spawn } = require('node:child_process');
const { createInterface } = require('node:readline');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');

class CodexUsageClient {
  constructor({ command = process.env.CODEX_BIN || 'codex', timeoutMs = 15_000 } = {}) {
    this.command = command;
    this.timeoutMs = timeoutMs;
    this.process = null;
    this.pending = new Map();
    this.sequence = 0;
    this.ready = null;
  }

  async connect() {
    if (this.process && !this.process.killed) return this.ready;
    this.process = spawn(this.command, ['app-server', '--stdio'], {
      stdio: ['pipe', 'pipe', 'pipe'],
      windowsHide: true,
      env: process.env
    });
    this.process.stderr.on('data', () => {});
    createInterface({ input: this.process.stdout }).on('line', (line) => this.#handleLine(line));
    this.process.once('exit', (code) => this.#failAll(new Error(`Codex app-server exited (${code ?? 'unknown'})`)));
    this.process.once('error', (error) => this.#failAll(error));
    this.ready = this.#request('initialize', {
      clientInfo: { name: 'codex_usage_widget', title: 'Codex Usage Widget', version: '0.1.0' },
      capabilities: { experimentalApi: true }
    }).then(() => this.#notify('initialized'));
    return this.ready;
  }

  async readRateLimits() {
    await this.connect();
    return this.#request('account/rateLimits/read', {
      excludeResetCreditDetails: true,
      supportsLunaReserve: false
    });
  }

  close() {
    if (this.process && !this.process.killed) this.process.kill();
    this.process = null;
  }

  #send(message) {
    if (!this.process?.stdin.writable) throw new Error('Codex app-server is not available');
    this.process.stdin.write(`${JSON.stringify(message)}\n`);
  }

  #notify(method, params) {
    this.#send(params === undefined ? { method } : { method, params });
  }

  #request(method, params) {
    const id = ++this.sequence;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(id);
        reject(new Error(`Codex timed out while calling ${method}`));
      }, this.timeoutMs);
      this.pending.set(id, { resolve, reject, timer });
      try { this.#send({ id, method, params }); }
      catch (error) { clearTimeout(timer); this.pending.delete(id); reject(error); }
    });
  }

  #handleLine(line) {
    let message;
    try { message = JSON.parse(line); } catch { return; }
    if (message.id === undefined) return;
    const pending = this.pending.get(message.id);
    if (!pending) return;
    clearTimeout(pending.timer);
    this.pending.delete(message.id);
    if (message.error) pending.reject(new Error(message.error.message || 'Codex request failed'));
    else pending.resolve(message.result);
  }

  #failAll(error) {
    for (const pending of this.pending.values()) {
      clearTimeout(pending.timer);
      pending.reject(error);
    }
    this.pending.clear();
    this.process = null;
  }
}

function normalizeUsage(result, now = Date.now()) {
  const rate = result?.rateLimitsByLimitId?.codex || result?.rateLimits;
  if (!rate) throw new Error('Codex did not return a usage snapshot');
  const window = (value) => value ? {
    usedPercent: Math.max(0, Math.min(100, Number(value.usedPercent) || 0)),
    windowMinutes: value.windowDurationMins ?? null,
    resetsAt: value.resetsAt ? value.resetsAt * 1000 : null
  } : null;
  return {
    source: 'live',
    fetchedAt: now,
    accountId: result.accountId ?? null,
    planType: rate.planType ?? 'unknown',
    ordinaryUsageAllowed: result.ordinaryUsageAllowed ?? null,
    primary: window(rate.primary),
    secondary: window(rate.secondary),
    credits: rate.credits ?? null,
    resetCredits: result.rateLimitResetCredits?.availableCount ?? 0,
    reachedType: rate.rateLimitReachedType ?? null
  };
}

function findNewestJsonl(root) {
  let newest = null;
  const visit = (directory) => {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      const target = path.join(directory, entry.name);
      if (entry.isDirectory()) visit(target);
      else if (entry.name.endsWith('.jsonl')) {
        const mtimeMs = fs.statSync(target).mtimeMs;
        if (!newest || mtimeMs > newest.mtimeMs) newest = { path: target, mtimeMs };
      }
    }
  };
  visit(root);
  return newest;
}

function readLocalSnapshot(codexHome = process.env.CODEX_HOME || path.join(os.homedir(), '.codex')) {
  const newest = findNewestJsonl(path.join(codexHome, 'sessions'));
  if (!newest) throw new Error('No local Codex usage snapshot found');
  const stat = fs.statSync(newest.path);
  const bytes = Math.min(stat.size, 512 * 1024);
  const buffer = Buffer.alloc(bytes);
  const fd = fs.openSync(newest.path, 'r');
  try { fs.readSync(fd, buffer, 0, bytes, stat.size - bytes); } finally { fs.closeSync(fd); }
  const lines = buffer.toString('utf8').split('\n');
  for (let index = lines.length - 1; index >= 0; index--) {
    let event;
    try { event = JSON.parse(lines[index]); } catch { continue; }
    if (event.type !== 'event_msg' || event.payload?.type !== 'token_count' || !event.payload.rate_limits) continue;
    const raw = event.payload.rate_limits;
    const adaptWindow = (value) => value && ({
      usedPercent: value.used_percent,
      windowDurationMins: value.window_minutes,
      resetsAt: value.resets_at
    });
    const usage = normalizeUsage({ rateLimits: {
      planType: raw.plan_type,
      primary: adaptWindow(raw.primary),
      secondary: adaptWindow(raw.secondary),
      credits: raw.credits && {
        hasCredits: raw.credits.has_credits,
        unlimited: raw.credits.unlimited,
        balance: raw.credits.balance
      },
      rateLimitReachedType: raw.rate_limit_reached_type
    }}, newest.mtimeMs);
    usage.source = 'local-cache';
    return usage;
  }
  throw new Error('No local Codex usage snapshot found');
}

module.exports = { CodexUsageClient, normalizeUsage, readLocalSnapshot };
