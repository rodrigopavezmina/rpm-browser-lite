const assert = require('assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const source = fs.readFileSync(path.join(__dirname, '..', 'src', 'ChatGPTLite', 'JobApplyScript.js'), 'utf8');
const controls = [];
const window = {};
const context = {
  window,
  document: {
    body: {},
    querySelectorAll: () => controls
  },
  setTimeout,
  clearTimeout,
  MutationObserver: undefined,
  Event: function Event() {}
};

vm.runInNewContext(source, context, { filename: 'JobApplyScript.js' });

function FakeInput(type, attrs, visible = true) {
  this.type = type;
  this.tagName = 'INPUT';
  this.disabled = false;
  this.style = visible ? {} : { display: 'none' };
  this.dataset = {};
  this.value = '';
  this.attrs = attrs || {};
  this.labels = [];
}
FakeInput.prototype.getAttribute = function (name) { return this.attrs[name] || null; };
FakeInput.prototype.setAttribute = function (name, value) { this.attrs[name] = value; };
FakeInput.prototype.getBoundingClientRect = function () {
  return this.style.display === 'none' ? { width: 0, height: 0 } : { width: 100, height: 20 };
};
FakeInput.prototype.dispatchEvent = function () {};
Object.defineProperty(FakeInput.prototype, 'value', {
  get: function () { return this._value || ''; },
  set: function (value) { this._value = String(value); }
});

function FakeSelect(attrs, options) {
  this.type = 'select-one';
  this.tagName = 'SELECT';
  this.disabled = false;
  this.style = {};
  this.dataset = {};
  this.attrs = attrs || {};
  this.labels = [];
  this.options = options;
  this.value = '';
}
FakeSelect.prototype.getAttribute = FakeInput.prototype.getAttribute;
FakeSelect.prototype.getBoundingClientRect = FakeInput.prototype.getBoundingClientRect;
FakeSelect.prototype.dispatchEvent = function () {};

const firstName = new FakeInput('text', { autocomplete: 'given-name' });
const mode = new FakeSelect(
  { name: 'work mode' },
  [{ text: 'Remote', value: 'remote', disabled: false }, { text: 'On-site', value: 'onsite', disabled: false }]
);
const protectedField = new FakeInput('text', { name: 'ethnicity' });
const hiddenResume = new FakeInput('file', { name: 'resume', 'aria-label': 'Resume' }, false);
controls.push(firstName, mode, protectedField, hiddenResume);
context.HTMLInputElement = FakeInput;
context.HTMLTextAreaElement = FakeInput;
window.HTMLInputElement = FakeInput;
window.HTMLTextAreaElement = FakeInput;

const profile = {
  FirstName: 'Ana',
  WorkMode: 'Remote',
  OverwriteExisting: false
};

const first = window.RpmJobApply.fill(profile);
assert.strictEqual(firstName.value, 'Ana');
assert.strictEqual(mode.value, 'remote');
assert.strictEqual(first.resumeFieldFound, false);
assert.ok(first.skipped.some(item => item.canonicalField === 'sensitive.ethnicity' && item.reason === 'sensitive-manual-confirmation'));
assert.ok(first.matches.every(item => item.canonicalField && typeof item.score === 'number' && Array.isArray(item.signals) && Array.isArray(item.conflicts) && item.reason));
assert.strictEqual(first.filled.length, 2);

const hiddenEmail = new FakeInput('email', { name: 'Email' }, false);
const visibleEmail = new FakeInput('email', { name: 'Email' });
context.document.querySelectorAll = () => [hiddenEmail, visibleEmail];
const visibilityGlobal = window.RpmJobApply.fill({ Email: 'ana@example.com' });
assert.strictEqual(hiddenEmail.value, '');
assert.strictEqual(visibleEmail.value, 'ana@example.com');
assert.ok(visibilityGlobal.filled.some(item => item.label === 'Email'));

const second = window.RpmJobApply.fill(profile);
assert.strictEqual(second.filled.length, 0);
assert.ok(second.skipped.length >= 2);
assert.match(source, /MAX_DYNAMIC_RETRIES/);
assert.match(source, /scheduleDynamicRetry/);
assert.match(source, /function cancel\(\)/);
assert.match(source, /observer\.disconnect/);
assert.match(source, /solveGlobalAssignments/);
assert.match(source, /profileResourceKey/);
assert.match(source, /global-resource-conflict/);
assert.match(source, /blockInfo/);
assert.doesNotMatch(source, /\.submit\s*\(/);
assert.doesNotMatch(source, /\.click\s*\(/);

const engine = fs.readFileSync(path.join(__dirname, '..', 'src', 'ChatGPTLite', 'JobApplyEngine.cs'), 'utf8');
assert.match(engine, /pageProfile\.ResumePath = string\.Empty/);
assert.match(engine, /pageProfile\.FieldMetadata = new Dictionary<string, JobApplicationFieldMetadata>\(\)/);
assert.match(engine, /JobApplyTimeoutException/);
assert.match(engine, /CancelAsync/);
assert.match(engine, /fileCount/);
assert.match(engine, /List<JobApplyMatch> Matches/);
assert.match(engine, /CanonicalField/);
assert.match(engine, /BlockKey/);
assert.match(engine, /GlobalScore/);

const browser = fs.readFileSync(path.join(__dirname, '..', 'src', 'ChatGPTLite', 'BrowserWindow.cs'), 'utf8');
assert.match(browser, /_jobApplyPreparedPages\.Clear\(\)/);
assert.match(browser, /JobApplyEngine\.CancelAsync\(CoreWebView2\)/);
assert.match(browser, /initial-failed/);
assert.match(browser, /manualmente si es necesario/);
const program = fs.readFileSync(path.join(__dirname, '..', 'src', 'ChatGPTLite', 'Program.cs'), 'utf8');
assert.match(program, /JobApplyDiagnostics\.Exception\(exception\)/);
assert.doesNotMatch(program, /exception\.ToString\(\)/);

console.log('job-apply-robustness: ok');
