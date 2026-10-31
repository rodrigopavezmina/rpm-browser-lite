const assert = require('assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const fixture = fs.readFileSync(path.join(__dirname, 'fixtures', 'job-widgets.html'), 'utf8');
['country', 'region', 'combobox', 'listbox', 'aria-autocomplete="remote"', 'consent', 'gender'].forEach(token => assert.ok(fixture.includes(token), token));

function Option(text, value, id) {
  this.text = text;
  this.textContent = text;
  this.innerText = text;
  this.value = value;
  this.id = id || '';
  this.disabled = false;
  this.attrs = { 'data-value': value, value };
}
Option.prototype.getAttribute = function (name) { return this.attrs[name] || null; };
Option.prototype.setAttribute = function (name, value) { this.attrs[name] = String(value); };

function Base(attrs) {
  this.attrs = attrs || {};
  this.disabled = false;
  this.style = {};
  this.dataset = {};
  this.labels = [];
  this.parentElement = null;
  this.events = [];
}
Base.prototype.getAttribute = function (name) { return this.attrs[name] || null; };
Base.prototype.setAttribute = function (name, value) { this.attrs[name] = String(value); };
Base.prototype.getBoundingClientRect = function () { return { width: 100, height: 20 }; };
Base.prototype.closest = function () { return null; };

function Input(type, attrs, documentRef) {
  Base.call(this, attrs);
  this.type = type;
  this.tagName = 'INPUT';
  this.value = attrs && attrs.value || '';
  this.checked = false;
  this.documentRef = documentRef;
}
Input.prototype = Object.create(Base.prototype);
Input.prototype.constructor = Input;
Input.prototype.dispatchEvent = function (event) {
  this.events.push(event && event.type || 'event');
  if (this.attrs.id === 'remote-search' && event && event.type === 'input' && !this.documentRef.remoteOption) {
    this.documentRef.remoteOption = new Option('Santiago', 'SCL', 'remote-santiago');
    this.documentRef.remoteResults.options = [this.documentRef.remoteOption];
  }
};

function Select(attrs, options, documentRef) {
  Base.call(this, attrs);
  this.type = 'select-one';
  this.tagName = 'SELECT';
  this.options = options || [];
  this.value = '';
  this.documentRef = documentRef;
}
Select.prototype = Object.create(Base.prototype);
Select.prototype.constructor = Select;
Select.prototype.dispatchEvent = function (event) {
  this.events.push(event && event.type || 'event');
  if (this.attrs.id === 'country' && event && event.type === 'change' && this.value === 'CL') {
    const dependentRegion = this.documentRef.getElementById('region');
    dependentRegion.options = [new Option('Región Metropolitana', 'RM')];
    dependentRegion.disabled = false;
  }
};

function WidgetRoot(attrs, options) {
  Base.call(this, attrs);
  this.options = options || [];
  this.tagName = 'DIV';
  this.role = attrs.role;
  this.value = '';
}
WidgetRoot.prototype = Object.create(Base.prototype);
WidgetRoot.prototype.constructor = WidgetRoot;
WidgetRoot.prototype.querySelectorAll = function () { return this.options; };
WidgetRoot.prototype.dispatchEvent = function () {};

function buildContext(controls, roots) {
  const documentRef = {
    body: {},
    remoteOption: null,
    remoteResults: roots['remote-results'],
    querySelectorAll: () => controls,
    getElementById: id => roots[id] || null
  };
  controls.forEach(control => { control.documentRef = documentRef; });
  return documentRef;
}

const window = {};
const context = {
  window,
  document: { querySelectorAll: () => [], body: null },
  setTimeout,
  clearTimeout,
  MutationObserver: undefined,
  Event: function Event(type) { this.type = type; }
};
vm.runInNewContext(fs.readFileSync(path.join(__dirname, '..', 'src', 'ChatGPTLite', 'JobApplyScript.js'), 'utf8'), context, { filename: 'JobApplyScript.js' });

const roots = {};
const region = new Select({ id: 'region', name: 'region', autocomplete: 'address-level1' }, [], null);
const country = new Select({ id: 'country', name: 'country', autocomplete: 'country' }, [new Option('Chile', 'CL'), new Option('United States', 'US')], null);
roots.region = region;
const employmentFull = new Input('radio', { name: 'employment type', value: 'full_time', 'aria-label': 'Full time employment type' }, null);
const employmentPart = new Input('radio', { name: 'employment type', value: 'part_time', 'aria-label': 'Part time employment type' }, null);
const workModeRoot = new WidgetRoot({ id: 'work-mode-list', role: 'listbox' }, [new Option('Remote', 'remote', 'mode-remote'), new Option('Hybrid', 'hybrid', 'mode-hybrid')]);
roots['work-mode-list'] = workModeRoot;
const workMode = new Input('text', { id: 'framework-combobox', name: 'work mode', role: 'combobox', 'aria-autocomplete': 'list', 'aria-controls': 'work-mode-list' }, null);
const remoteResults = new WidgetRoot({ id: 'remote-results', role: 'listbox' }, []);
roots['remote-results'] = remoteResults;
const remote = new Input('text', { id: 'remote-search', name: 'city', role: 'combobox', 'aria-autocomplete': 'remote', 'aria-controls': 'remote-results' }, null);
const legal = new Input('radio', { name: 'workAuthorization', value: 'yes', 'aria-label': 'Authorized to work legally' }, null);
const consent = new Input('checkbox', { name: 'consent', value: 'yes', 'aria-label': 'I agree to terms and privacy' }, null);
const gender = new Select({ id: 'gender', name: 'gender', 'aria-label': 'Gender' }, [new Option('Prefer not to say', '')], null);

const controls = [region, country, employmentFull, employmentPart, workMode, remote, legal, consent, gender];
context.document = buildContext(controls, roots);
const profile = { Country: 'CL', Region: 'RM', EmploymentType: 'Tiempo completo', WorkMode: 'Remote', City: 'Santiago', WorkAuthorization: true, Gender: 'Prefer not to say' };
const first = window.RpmJobApply.fill(profile);
assert.strictEqual(country.value, 'CL', 'country is selected before dependent region');
assert.strictEqual(region.value, 'RM', 'region options are rendered after country');
assert.strictEqual(employmentFull.checked, true, 'matching radio is selected');
assert.strictEqual(employmentPart.checked, false, 'nonmatching radio is untouched');
assert.strictEqual(workMode.value, 'Remote', 'React-like combobox is verified');
assert.ok(first.review.some(item => item.reason === 'legal-or-eliminatory-manual-confirmation'), 'legal answer requires review');
assert.strictEqual(legal.checked, false, 'legal radio is never selected');
assert.strictEqual(consent.checked, false, 'consent is never selected');
assert.strictEqual(gender.value, '', 'demographic select is never filled');

const remoteFirst = first.review.find(item => item.label.indexOf('city') >= 0 || item.canonicalField === 'address.city');
assert.ok(remoteFirst, 'remote autocomplete is recorded for retry/manual review');
const second = window.RpmJobApply.fill(profile, { retry: true });
assert.strictEqual(remote.value, 'Santiago', 'remote autocomplete selects the returned option');
assert.ok(second.matches.some(item => item.canonicalField === 'address.city' && item.reason === 'filled-needs-review'), 'remote value is verified after options arrive');
assert.ok(!/\.click\s*\(/.test(fs.readFileSync(path.join(__dirname, '..', 'src', 'ChatGPTLite', 'JobApplyScript.js'), 'utf8')));

// A listbox with dynamic option nodes is handled without synthetic clicks.
const listOption = new Option('Chile', 'CL', 'list-chile');
const listRoot = new WidgetRoot({ id: 'country-list', role: 'listbox' }, [listOption]);
const listbox = new WidgetRoot({ id: 'country-listbox', name: 'country', role: 'listbox', 'aria-label': 'Country' }, [listOption]);
roots['country-list'] = listRoot;
context.document = buildContext([listbox], roots);
const listResult = window.RpmJobApply.fill({ Country: 'Chile' });
assert.strictEqual(listbox.value, 'CL', 'dynamic listbox value is verified');
assert.ok(listResult.matches.some(item => item.canonicalField === 'address.country' && (item.action === 'filled' || item.action === 'review')));

const rejected = new Input('text', { id: 'rejected-combobox', name: 'work mode', role: 'combobox', 'aria-controls': 'work-mode-list' }, null);
rejected.dispatchEvent = function () { return false; };
context.document = buildContext([rejected], roots);
const rejectedResult = window.RpmJobApply.fill({ WorkMode: 'Remote' });
assert.ok(rejectedResult.review.some(item => item.reason === 'synthetic-event-rejected'), 'rejected synthetic events remain manual');

console.log('job-apply-widgets: ok');
