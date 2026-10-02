const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { normalizeUsage, readLocalSnapshot } = require('../src/codex-client.cjs');

test('normalizes the live Codex quota snapshot', () => {
  const result = normalizeUsage({
    ordinaryUsageAllowed: true,
    accountId: 'acct',
    rateLimits: {
      planType: 'plus',
      primary: { usedPercent: 30, windowDurationMins: 300, resetsAt: 2000 },
      secondary: { usedPercent: 9, windowDurationMins: 10080, resetsAt: 3000 },
      credits: { hasCredits: false, unlimited: false, balance: '0' }
    },
    rateLimitResetCredits: { availableCount: 1 }
  }, 1000);
  assert.equal(result.planType, 'plus');
  assert.equal(result.primary.usedPercent, 30);
  assert.equal(result.primary.resetsAt, 2_000_000);
  assert.equal(result.secondary.windowMinutes, 10080);
  assert.equal(result.resetCredits, 1);
  assert.equal(result.fetchedAt, 1000);
});

test('prefers the codex bucket and clamps percentages', () => {
  const result = normalizeUsage({
    rateLimits: { primary: { usedPercent: 1 } },
    rateLimitsByLimitId: { codex: { primary: { usedPercent: 140 } } }
  });
  assert.equal(result.primary.usedPercent, 100);
});

test('rejects an empty response', () => {
  assert.throws(() => normalizeUsage({}), /usage snapshot/);
});

test('reads a local session snapshot as an offline fallback', () => {
  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'codex-usage-'));
  const sessions = path.join(home, 'sessions', '2026', '10', '02');
  fs.mkdirSync(sessions, { recursive: true });
  fs.writeFileSync(path.join(sessions, 'rollout.jsonl'), `${JSON.stringify({
    type: 'event_msg', payload: { type: 'token_count', rate_limits: {
      plan_type: 'plus', primary: { used_percent: 44, window_minutes: 300, resets_at: 20 },
      secondary: { used_percent: 12, window_minutes: 10080, resets_at: 30 },
      credits: { has_credits: false, unlimited: false, balance: '0' }
    }}
  })}\n`);
  const usage = readLocalSnapshot(home);
  assert.equal(usage.source, 'local-cache');
  assert.equal(usage.primary.usedPercent, 44);
  fs.rmSync(home, { recursive: true });
});
