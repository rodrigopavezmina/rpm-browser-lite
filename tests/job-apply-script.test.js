const assert = require('assert');
const fs = require('fs');
const vm = require('vm');

const source = fs.readFileSync(require('path').join(__dirname, '..', 'src', 'ChatGPTLite', 'JobApplyScript.js'), 'utf8');
const window = {};
const context = {
  window,
  document: { querySelectorAll: () => [], body: null },
  setTimeout,
  clearTimeout,
  MutationObserver: undefined,
  Event: function Event() {}
};
vm.runInNewContext(source, context, { filename: 'JobApplyScript.js' });

assert.strictEqual(window.RpmJobApply.version, '1.0.0');
assert.strictEqual(window.RpmJobApply.normalize('  Región / Chile  '), 'region chile');
assert.strictEqual(window.RpmJobApply.normalize('Correo electrónico'), 'correo electronico');
assert.deepStrictEqual(JSON.parse(JSON.stringify(window.RpmJobApply.fill({ FirstName: 'Ana' }))), {
  filled: [], review: [], skipped: [], resumeFieldFound: false
});

function FakeInput(type, attrs) {
  this.type = type;
  this.tagName = 'INPUT';
  this.disabled = false;
  this.style = {};
  this.dataset = {};
  this.value = '';
  this.attrs = attrs || {};
  this.labels = [];
}
FakeInput.prototype.getAttribute = function (name) { return this.attrs[name] || null; };
FakeInput.prototype.getBoundingClientRect = function () { return { width: 100, height: 20 }; };
FakeInput.prototype.dispatchEvent = function () {};
Object.defineProperty(FakeInput.prototype, 'value', { get: function () { return this._value || ''; }, set: function (v) { this._value = String(v); } });

const email = new FakeInput('email', { name: 'email' });
const first = new FakeInput('text', { autocomplete: 'given-name' });
context.HTMLInputElement = FakeInput;
window.HTMLInputElement = FakeInput;
window.Event = function Event() {};
context.document.querySelectorAll = () => [email, first];
const filled = window.RpmJobApply.fill({ FirstName: 'Ana', Email: 'ana@example.com' });
assert.strictEqual(email.value, 'ana@example.com');
assert.strictEqual(first.value, 'Ana');
assert.strictEqual(filled.filled.length, 2);
console.log('job-apply-script: ok');
