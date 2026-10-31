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

function FakeSelect(attrs, options) {
  this.type = 'select-one';
  this.tagName = 'SELECT';
  this.disabled = false;
  this.style = {};
  this.dataset = {};
  this.attrs = attrs || {};
  this.labels = [];
  this.options = options || [];
  this.value = '';
}
FakeSelect.prototype.getAttribute = FakeInput.prototype.getAttribute;
FakeSelect.prototype.getBoundingClientRect = FakeInput.prototype.getBoundingClientRect;
FakeSelect.prototype.dispatchEvent = function () {};

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

function groupedBlock(kind, textValue) {
  const block = {
    innerText: textValue,
    textContent: textValue,
    attrs: { class: `${kind}-block` },
    parentElement: null,
    getAttribute: function (name) { return this.attrs[name] || null; },
    closest: function () { return this; },
    querySelector: function () { return null; }
  };
  return block;
}

function groupedInput(label, block) {
  const input = new FakeInput('text', { name: label });
  input.parentElement = block;
  input.closest = function () { return block; };
  return input;
}

const experienceOne = groupedBlock('experience', 'Work experience');
const experienceTwo = groupedBlock('experience', 'Work experience');
const educationOne = groupedBlock('education', 'Education');
const experienceList = { children: [experienceOne, experienceTwo], parentElement: null };
const educationList = { children: [educationOne], parentElement: null };
experienceOne.parentElement = experienceList;
experienceTwo.parentElement = experienceList;
educationOne.parentElement = educationList;
const groupedCompanyOne = groupedInput('Company', experienceOne);
const groupedCompanyTwo = groupedInput('Company', experienceTwo);
const groupedWrongCompany = groupedInput('Company', educationOne);
const groupedSchool = groupedInput('Institution', educationOne);
const groupedFirstName = new FakeInput('text', { name: 'First name' });
const groupedLastName = new FakeInput('text', { name: 'Last name' });
const groupedFullName = new FakeInput('text', { name: 'Full name' });
context.document.querySelectorAll = () => [groupedCompanyOne, groupedCompanyTwo, groupedWrongCompany, groupedSchool, groupedFirstName, groupedLastName, groupedFullName];
const grouped = window.RpmJobApply.fill({
  FirstName: 'Ana', LastName: 'Pérez', Experiences: [
    { Company: 'Acme' }, { Company: 'Globex' }
  ], Education: [{ Institution: 'Universidad de Chile' }]
});
assert.strictEqual(groupedCompanyOne.value, 'Acme');
assert.strictEqual(groupedCompanyTwo.value, 'Globex');
assert.strictEqual(groupedWrongCompany.value, '');
assert.strictEqual(groupedSchool.value, 'Universidad de Chile');
assert.strictEqual(groupedFirstName.value, 'Ana');
assert.strictEqual(groupedLastName.value, 'Pérez');
assert.strictEqual(groupedFullName.value, 'Ana Pérez');
assert.ok(grouped.matches.some(item => item.label === 'Full name' && item.canonicalField === 'identity.fullName'));
assert.ok(grouped.skipped.some(item => item.label === 'Company' && item.blockKind === 'education' && item.reason === 'context-conflict'));
assert.strictEqual(grouped.matches.filter(item => item.canonicalField === 'experience.company' && item.blockKind === 'experience').length, 2);
assert.strictEqual(JSON.stringify(grouped.matches.filter(item => item.canonicalField === 'experience.company' && item.blockKind === 'experience').map(item => item.blockIndex)), JSON.stringify([0, 1]));

const genericName = new FakeInput('text', { name: 'Name' });
context.document.querySelectorAll = () => [genericName];
const genericNameResult = window.RpmJobApply.fill({ FirstName: 'Ana', LastName: 'Pérez' });
assert.strictEqual(genericName.value, '');
assert.ok(genericNameResult.review.some(item => item.canonicalField === 'identity.ambiguousName' && item.reason === 'manual-review-required'));

const repeatedPortfolioA = new FakeInput('text', { name: 'Website' });
const repeatedPortfolioB = new FakeInput('text', { name: 'Personal website' });
context.document.querySelectorAll = () => [repeatedPortfolioA, repeatedPortfolioB];
const firstGlobal = window.RpmJobApply.fill({ PortfolioUrl: 'https://portfolio.example' });
const repeatedPortfolioA2 = new FakeInput('text', { name: 'Website' });
const repeatedPortfolioB2 = new FakeInput('text', { name: 'Personal website' });
context.document.querySelectorAll = () => [repeatedPortfolioA2, repeatedPortfolioB2];
const secondGlobal = window.RpmJobApply.fill({ PortfolioUrl: 'https://portfolio.example' });
assert.strictEqual(repeatedPortfolioA.value, '');
assert.strictEqual(repeatedPortfolioB.value, 'https://portfolio.example');
assert.strictEqual(repeatedPortfolioA2.value, '');
assert.strictEqual(repeatedPortfolioB2.value, 'https://portfolio.example');
assert.ok(firstGlobal.skipped.some(item => item.label === 'Website' && (item.reason === 'ambiguous-near-tie' || item.reason === 'global-resource-conflict')));
assert.deepStrictEqual(
  firstGlobal.matches.map(item => [item.label, item.canonicalField, item.reason, item.blockKey]),
  secondGlobal.matches.map(item => [item.label, item.canonicalField, item.reason, item.blockKey])
);

const monthlyDate = new FakeInput('date', { name: 'Start date' });
context.document.querySelectorAll = () => [monthlyDate];
const monthlyDateResult = window.RpmJobApply.fill({ Experiences: [{ StartDate: '2024-03' }] });
assert.strictEqual(monthlyDate.value, '');
assert.ok(monthlyDateResult.review.some(item => item.reason === 'month-precision-derived-day' && item.derivedValue === '2024-03-01'));

const fullDate = new FakeInput('date', { name: 'Start date' });
context.document.querySelectorAll = () => [fullDate];
const fullDateResult = window.RpmJobApply.fill({ Experiences: [{ StartDate: '2024-03-15' }] });
assert.strictEqual(fullDate.value, '2024-03-15');
assert.ok(fullDateResult.filled.some(item => item.canonicalField === 'experience.startDate'));

const startDay = new FakeInput('text', { name: 'Start day' });
const startMonth = new FakeInput('text', { name: 'Start month' });
const startYear = new FakeInput('text', { name: 'Start year' });
context.document.querySelectorAll = () => [startDay, startMonth, startYear];
const splitDateResult = window.RpmJobApply.fill({ Experiences: [{ StartDate: '2024-03-15' }] });
assert.strictEqual(startDay.value, '15');
assert.strictEqual(startMonth.value, '03');
assert.strictEqual(startYear.value, '2024');
assert.ok(splitDateResult.filled.some(item => item.canonicalField === 'experience.startDate.day'));
assert.ok(splitDateResult.filled.some(item => item.canonicalField === 'experience.startDate.month'));
assert.ok(splitDateResult.filled.some(item => item.canonicalField === 'experience.startDate.year'));

const monthSelect = new FakeSelect({ name: 'Start month' }, [
  { text: 'January', value: '01', disabled: false },
  { text: 'March', value: '03', disabled: false }
]);
context.document.querySelectorAll = () => [monthSelect];
window.RpmJobApply.fill({ Experiences: [{ StartDate: '2024-03' }] });
assert.strictEqual(monthSelect.value, '03');

const current = new FakeInput('checkbox', { name: 'Currently working here' });
context.document.querySelectorAll = () => [current];
const currentResult = window.RpmJobApply.fill({ Experiences: [{ IsCurrent: true }] });
assert.strictEqual(current.checked, true);
assert.ok(currentResult.review.some(item => item.reason === 'current-employment-filled-confirmation'));
console.log('job-apply-script: ok');
