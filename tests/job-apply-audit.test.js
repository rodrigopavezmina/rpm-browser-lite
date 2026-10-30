const assert = require('assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const source = fs.readFileSync(path.join(__dirname, '..', 'src', 'ChatGPTLite', 'JobApplyAuditScript.js'), 'utf8');
const controls = [];
const ids = {};

function Node(tagName, attrs, parent) {
  this.tagName = tagName.toUpperCase();
  this.attrs = attrs || {};
  this.parentElement = parent || null;
  this.children = [];
  this.labels = [];
  this.options = [];
  this.disabled = !!this.attrs.disabled;
  this.type = this.attrs.type || '';
  this.required = !!this.attrs.required;
  this.checked = !!this.attrs.checked;
  this.selected = !!this.attrs.selected;
  this.value = this.attrs.value || '';
  this.textContent = this.attrs.textContent || '';
  this.innerText = this.textContent;
  this.validationMessage = this.attrs.validationMessage || '';
  if (parent) parent.children.push(this);
  if (this.attrs.id) ids[this.attrs.id] = this;
}
Node.prototype.getAttribute = function (name) {
  if (Object.prototype.hasOwnProperty.call(this.attrs, name)) return String(this.attrs[name]);
  return null;
};
Node.prototype.hasAttribute = function (name) {
  return Object.prototype.hasOwnProperty.call(this.attrs, name);
};
Node.prototype.getBoundingClientRect = function () {
  return this.attrs.hidden ? { width: 0, height: 0 } : { width: 100, height: 20 };
};
Node.prototype.querySelector = function (selector) {
  if (selector === 'legend') return this.children.find(x => x.tagName === 'LEGEND') || null;
  if (selector.indexOf('h1') >= 0 || selector.indexOf('[role=\'heading\']') >= 0) return this.children.find(x => /^H[1-6]$/.test(x.tagName)) || null;
  return null;
};
Node.prototype.querySelectorAll = function () { return []; };

const body = new Node('body', {});
const section = new Node('section', { 'aria-label': 'Datos personales' }, body);
new Node('h2', { textContent: 'Contacto' }, section);
const fieldset = new Node('fieldset', {}, section);
new Node('legend', { textContent: 'Identidad' }, fieldset);

function addControl(tag, attrs, parent = fieldset) {
  const node = new Node(tag, attrs, parent);
  controls.push(node);
  return node;
}

const email = addControl('input', { id: 'email', name: 'email', type: 'email', autocomplete: 'email', required: true, validationMessage: 'Correo inválido' });
const emailLabel = new Node('label', { for: 'email', textContent: 'Correo electrónico' }, body);
email.labels = [emailLabel];
const mode = addControl('select', { name: 'workMode', 'aria-label': 'Modalidad' });
mode.options = [
  new Node('option', { value: 'remote', textContent: 'Remoto' }),
  new Node('option', { value: 'hybrid', textContent: 'Híbrido', disabled: true })
];
mode.options[0].selected = true;
const remote = addControl('input', { type: 'radio', name: 'contract', value: 'full', id: 'full' });
const part = addControl('input', { type: 'radio', name: 'contract', value: 'part', id: 'part' });
remote.labels = [new Node('label', { textContent: 'Tiempo completo' }, body)];
part.labels = [new Node('label', { textContent: 'Tiempo parcial' }, body)];
const consent = addControl('input', { type: 'checkbox', name: 'terms', required: true, 'aria-label': 'Acepto términos' });
const resume = addControl('input', { type: 'file', name: 'resume', 'aria-label': 'CV' });
const notes = addControl('textarea', { name: 'notes', placeholder: 'Comentarios' });
const rich = addControl('div', { contenteditable: 'plaintext-only', 'aria-label': 'Resumen profesional' });
const hidden = addControl('input', { type: 'text', name: 'hidden', hidden: true });
const shadowHost = new Node('div', {}, body);
const shadowInput = new Node('input', { type: 'text', name: 'shadow-name', 'aria-label': 'Shadow name' });
shadowHost.shadowRoot = { querySelectorAll: selector => selector === "input,select,textarea,[contenteditable]" ? [shadowInput] : [] };
const frame = new Node('iframe', {}, body);
const frameInput = new Node('input', { type: 'text', name: 'frame-name', 'aria-label': 'Frame name' });
frame.contentDocument = { querySelectorAll: selector => selector === "input,select,textarea,[contenteditable]" ? [frameInput] : [] };

const document = {
  title: 'Formulario sintético',
  body,
  getElementById: id => ids[id] || null,
  querySelectorAll: selector => {
    if (selector === "input,select,textarea,[contenteditable]") return controls;
    if (selector === '*') return [shadowHost, frame];
    if (selector === 'iframe,frame') return [frame];
    if (selector.indexOf("label[for='") === 0) {
      const id = selector.slice(11, -2).replace(/\\'/g, "'");
      return [emailLabel].filter(x => x.attrs.for === id);
    }
    if (selector.indexOf("input[type='radio']") === 0) {
      const name = selector.match(/name='([^']*)'/)[1];
      return controls.filter(x => x.tagName === 'INPUT' && x.attrs.type === 'radio' && x.attrs.name === name);
    }
    return [];
  }
};
const context = { window: {}, document, setTimeout, clearTimeout, Event: function Event() {} };
context.window.location = { href: 'https://jobs.example.test/apply/123' };
vm.runInNewContext(source, context, { filename: 'JobApplyAuditScript.js' });

const first = context.window.RpmJobApplyAudit.inspect();
const second = context.window.RpmJobApplyAudit.inspect();
assert.strictEqual(first.schemaVersion, '1.0');
assert.strictEqual(first.fieldCount, controls.length + 2);
assert.strictEqual(JSON.stringify(first), JSON.stringify(second));
assert.deepStrictEqual(first.fields.map(x => x.fingerprint), second.fields.map(x => x.fingerprint));
const stableFingerprint = first.fields.find(x => x.name === 'frame-name').fingerprint;
controls.unshift(new Node('input', { type: 'text', name: 'inserted-before', 'aria-label': 'Inserted' }));
const afterInsertion = context.window.RpmJobApplyAudit.inspect();
assert.strictEqual(afterInsertion.fields.find(x => x.name === 'frame-name').fingerprint, stableFingerprint);
assert.ok(first.fields.some(x => x.type === 'contenteditable'));
assert.ok(first.fields.some(x => x.name === 'shadow-name'));
assert.ok(first.fields.some(x => x.name === 'frame-name'));
assert.ok(first.fields.some(x => x.type === 'file'));
assert.ok(first.fields.some(x => x.type === 'radio' && x.options.length === 2));
assert.ok(first.fields.some(x => x.type === 'checkbox' && x.options.length === 1));
assert.ok(first.fields.some(x => x.type === 'select' && x.options.length === 2));
assert.ok(first.fields.some(x => x.required && x.validation.messages.length > 0));
assert.ok(first.fields.some(x => x.visibility.visible === false));
assert.ok(first.fields.some(x => x.context.fieldset === 'Identidad'));
assert.ok(first.fields.every(x => !Object.prototype.hasOwnProperty.call(x, 'value')));
assert.doesNotMatch(source, /dispatchEvent/);
assert.doesNotMatch(source, /\.click\s*\(/);
assert.match(source, /ownerDocument/);
assert.match(source, /identityCounts/);
assert.strictEqual(email.value, '');

const engine = fs.readFileSync(path.join(__dirname, '..', 'src', 'ChatGPTLite', 'JobApplyAuditEngine.cs'), 'utf8');
assert.match(engine, /RpmJobApplyAudit/);
assert.match(engine, /JobFormAuditResult/);
const build = fs.readFileSync(path.join(__dirname, '..', 'build.ps1'), 'utf8');
assert.match(build, /JobApplyAuditScript\.js/);
assert.match(build, /JobApplyAuditEngine\.cs/);
const browser = fs.readFileSync(path.join(__dirname, '..', 'src', 'ChatGPTLite', 'BrowserWindow.cs'), 'utf8');
assert.match(browser, /AuditAndExportFormAsync/);
assert.match(browser, /Ctrl\+Shift\+I/);

console.log('job-apply-audit: ok');
