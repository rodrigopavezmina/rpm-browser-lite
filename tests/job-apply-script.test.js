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

assert.strictEqual(window.RpmJobApply.version, '2.0.0');
assert.strictEqual(window.RpmJobApply.ontologyVersion, '2.0');
const ontologyCategories = new Set(window.RpmJobApply.getOntology().map(item => item.category));
['identity', 'contact', 'address', 'links', 'experience', 'education', 'authorization', 'availability', 'salary', 'skills', 'files', 'questions', 'sensitive'].forEach(category => assert.ok(ontologyCategories.has(category), category));
assert.strictEqual(window.RpmJobApply.normalize('  Región / Chile  '), 'region chile');
assert.strictEqual(window.RpmJobApply.normalize('Correo electrónico'), 'correo electronico');
assert.deepStrictEqual(JSON.parse(JSON.stringify(window.RpmJobApply.fill({ FirstName: 'Ana' }))), {
  filled: [], review: [], skipped: [], matches: [], resumeFieldFound: false
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
const ambiguousWebsite = new FakeInput('text', { name: 'website' });
context.document.querySelectorAll = () => [ambiguousWebsite];
const ambiguous = window.RpmJobApply.fill({ PortfolioUrl: 'https://portfolio.example' });
assert.strictEqual(ambiguousWebsite.value, '');
assert.ok(ambiguous.skipped.some(item => item.reason === 'ambiguous-near-tie'));
assert.ok(ambiguous.matches.some(item =>
  (item.canonicalField === 'links.website' || item.canonicalField === 'links.portfolio') &&
  item.conflicts.some(conflict => conflict.canonicalField === 'links.website' || conflict.canonicalField === 'links.portfolio')));
console.log('job-apply-script: ok');
