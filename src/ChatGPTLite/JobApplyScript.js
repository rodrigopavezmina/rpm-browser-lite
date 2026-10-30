(function (global) {
    "use strict";
    if (global.RpmJobApply && global.RpmJobApply.__rpmJobApplyOwned) return;

    var VERSION = "1.0.0";
    var MAX_DYNAMIC_RETRIES = 12;
    var state = { profile: null, observer: null, filling: false, timer: null, retryCount: 0 };
    var protectedWords = [
        "password", "captcha", "recaptcha", "security code", "social security", "ssn",
        "date of birth", "birth date", "fecha de nacimiento", "ethnicity", "race", "gender",
        "disability", "veteran", "demographic", "diversity", "pronoun", "consent", "privacy",
        "terms", "agreement", "signature", "firma", "autorizo", "authorize", "acknowledge",
        "sexual orientation", "nationality", "criminal record", "background check",
        "senha", "security code", "date of birth", "data de nascimento",
        "sexualidade", "nacionalidade", "registro criminal", "verificação de antecedentes"
    ];

    function text(value) { return value == null ? "" : String(value); }
    function get(object, name) {
        if (!object) return "";
        var camel = name.charAt(0).toLowerCase() + name.slice(1);
        return object[name] != null ? object[name] : (object[camel] != null ? object[camel] : "");
    }
    function normalize(value) {
        return text(value).toLowerCase().normalize ? text(value).toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/[^a-z0-9]+/g, " ").trim() : text(value).toLowerCase().replace(/[^a-z0-9áéíóúñü]+/g, " ").trim();
    }
    function hasAny(haystack, words) {
        var value = normalize(haystack);
        for (var i = 0; i < words.length; i++) if (value.indexOf(normalize(words[i])) >= 0) return true;
        return false;
    }
    function visible(element) {
        if (!element || element.disabled || element.type === "hidden") return false;
        var style = global.getComputedStyle ? global.getComputedStyle(element) : null;
        if (style && (style.display === "none" || style.visibility === "hidden")) return false;
        var rect = element.getBoundingClientRect ? element.getBoundingClientRect() : null;
        return !rect || (rect.width > 0 && rect.height > 0);
    }
    function controlText(element) {
        var bits = [];
        ["aria-label", "placeholder", "name", "id", "autocomplete", "title"].forEach(function (attribute) {
            if (element.getAttribute && element.getAttribute(attribute)) bits.push(element.getAttribute(attribute));
        });
        if (element.labels) for (var i = 0; i < element.labels.length; i++) bits.push(element.labels[i].innerText || element.labels[i].textContent);
        var labelled = element.getAttribute && element.getAttribute("aria-labelledby");
        if (labelled) labelled.split(/\s+/).forEach(function (id) { var node = document.getElementById(id); if (node) bits.push(node.innerText || node.textContent); });
        var parent = element.parentElement;
        for (var level = 0; parent && level < 2; level++, parent = parent.parentElement) {
            var own = parent.querySelector ? parent.querySelector("label,legend") : null;
            if (own) bits.push(own.innerText || own.textContent);
        }
        return bits.join(" ").replace(/\s+/g, " ").trim();
    }
    function isProtected(label) { return hasAny(label, protectedWords); }
    function firstNonEmpty(values) { for (var i = 0; i < values.length; i++) if (text(values[i]).trim()) return text(values[i]).trim(); return ""; }
    function joinName(profile) { return [get(profile, "FirstName"), get(profile, "MiddleName"), get(profile, "LastName")].filter(function (x) { return text(x).trim(); }).join(" "); }
    function isoDate(value, kind) {
        var source = text(value).trim();
        if (!source) return "";
        var match = /^(\d{4})-(\d{2})(?:-(\d{2}))?$/.exec(source);
        if (match) return kind === "month" ? match[1] + "-" + match[2] : (match[3] ? source : match[1] + "-" + match[2] + "-01");
        return source;
    }
    function profileValue(profile, key, index) {
        if (key === "fullName") return joinName(profile);
        var experiences = get(profile, "Experiences") || [];
        var education = get(profile, "Education") || [];
        var item = index == null ? null : (experiences[index] || education[index]);
        if (key === "company") return item ? firstNonEmpty([get(item, "Company")]) : "";
        if (key === "jobTitle") return item ? firstNonEmpty([get(item, "Title")]) : firstNonEmpty([get(profile, "CurrentTitle"), get(profile, "DesiredRole")]);
        if (key === "location") return item ? get(item, "Location") : "";
        if (key === "startDate") return item ? get(item, "StartDate") : get(profile, "AvailableStartDate");
        if (key === "endDate") return item ? (get(item, "IsCurrent") ? "Present" : get(item, "EndDate")) : "";
        if (key === "description") return item ? get(item, "Description") : "";
        if (key === "degree") return item ? get(item, "Degree") : "";
        if (key === "fieldOfStudy") return item ? get(item, "FieldOfStudy") : "";
        var map = {
            firstName: "FirstName", lastName: "LastName", preferredName: "PreferredName", email: "Email", phone: "Phone",
            address: "AddressLine1", address2: "AddressLine2", city: "City", region: "Region", postal: "PostalCode", country: "Country",
            linkedin: "LinkedInUrl", portfolio: "PortfolioUrl", github: "GitHubUrl", years: "YearsExperience", authorization: "WorkAuthorization",
            sponsorship: "RequiresSponsorship", relocation: "WillingToRelocate", salary: "SalaryExpectation", skills: "Skills",
            workMode: "WorkMode", employmentType: "EmploymentType"
        };
        return map[key] ? get(profile, map[key]) : "";
    }

    var definitions = [
        { key: "email", terms: ["email", "e mail", "correo", "correo electronico"], type: "email" },
        { key: "phone", terms: ["phone", "telephone", "mobile", "cell", "telefono", "celular"], type: "tel" },
        { key: "firstName", terms: ["first name", "given name", "forename", "nombre"], auto: ["given-name"] },
        { key: "lastName", terms: ["last name", "family name", "surname", "apellido"], auto: ["family-name"] },
        { key: "preferredName", terms: ["preferred name", "display name", "nombre preferido"] },
        { key: "fullName", terms: ["full name", "complete name", "nombre completo"] },
        { key: "address", terms: ["address", "street", "direccion", "calle"], auto: ["street-address"] },
        { key: "address2", terms: ["address line 2", "apartment", "unit", "suite", "complemento"] },
        { key: "city", terms: ["city", "town", "ciudad"], auto: ["address-level2"] },
        { key: "region", terms: ["state", "province", "region", "región", "estado"], auto: ["address-level1"] },
        { key: "postal", terms: ["postal code", "zip code", "zip", "codigo postal"], auto: ["postal-code"] },
        { key: "country", terms: ["country", "pais", "país"], auto: ["country"] },
        { key: "linkedin", terms: ["linkedin", "linked in"] },
        { key: "portfolio", terms: ["portfolio", "personal website", "sitio personal", "web personal"] },
        { key: "github", terms: ["github", "git hub"] },
        { key: "jobTitle", terms: ["job title", "current title", "position", "role", "cargo", "puesto", "titulo profesional"] },
        { key: "company", terms: ["company", "employer", "organization", "empresa", "empleador"] },
        { key: "location", terms: ["location", "ubicacion", "ubicación"] },
        { key: "startDate", terms: ["start date", "start month", "start year", "from", "date started", "fecha de inicio", "mes de inicio", "ano de inicio", "año de inicio", "desde"] },
        { key: "endDate", terms: ["end date", "end month", "end year", "to", "date ended", "fecha de termino", "fecha de término", "mes de termino", "mes de término", "ano de termino", "año de término", "hasta"] },
        { key: "description", terms: ["responsibilities", "description", "duties", "responsabilidades", "descripcion", "descripción"] },
        { key: "degree", terms: ["degree", "education level", "qualification", "grado", "titulo", "título"] },
        { key: "fieldOfStudy", terms: ["field of study", "major", "specialization", "carrera", "especialidad"] },
        { key: "years", terms: ["years of experience", "years experience", "anos de experiencia", "años de experiencia"] },
        { key: "authorization", terms: ["work authorization", "authorized to work", "right to work", "autorizacion de trabajo", "autorización de trabajo"], review: true },
        { key: "sponsorship", terms: ["sponsorship", "visa sponsorship", "visa", "patrocinio"], review: true },
        { key: "relocation", terms: ["relocate", "relocation", "move", "reubicacion", "reubicación"], review: true },
        { key: "salary", terms: ["salary", "compensation", "pay expectation", "pretension de renta", "pretensión de renta", "remuneracion", "remuneración"], review: true },
        { key: "skills", terms: ["skills", "technical skills", "habilidades", "competencias"] },
        { key: "workMode", terms: ["work mode", "work arrangement", "remote", "hybrid", "onsite", "modalidad", "remoto", "hibrido", "presencial", "modelo de trabalho"] },
        { key: "employmentType", terms: ["employment type", "job type", "contract type", "tipo de empleo", "tipo de contrato", "full time", "part time", "tempo integral", "tempo parcial"] }
    ];

    function score(definition, label, element) {
        var value = normalize(label), scoreValue = 0;
        if (definition.type && element.type === definition.type) scoreValue += 0.65;
        var autocomplete = normalize(element.getAttribute && element.getAttribute("autocomplete"));
        if (definition.auto && definition.auto.indexOf(autocomplete) >= 0) scoreValue += 0.95;
        for (var i = 0; i < definition.terms.length; i++) {
            var term = normalize(definition.terms[i]);
            if (!term) continue;
            if (value === term) scoreValue = Math.max(scoreValue, 1.0);
            else if (value.indexOf(term) >= 0) scoreValue = Math.max(scoreValue, 0.82);
            else {
                var words = term.split(" "), found = 0;
                for (var j = 0; j < words.length; j++) if (value.indexOf(words[j]) >= 0) found++;
                if (found === words.length && words.length > 1) scoreValue = Math.max(scoreValue, 0.74);
            }
        }
        return Math.min(1, scoreValue);
    }
    function bestDefinition(element, label) {
        var best = null, bestScore = 0;
        for (var i = 0; i < definitions.length; i++) {
            var candidate = score(definitions[i], label, element);
            if (candidate > bestScore) { bestScore = candidate; best = definitions[i]; }
        }
        if (element.type === "email") best = definitions[0], bestScore = Math.max(bestScore, 0.98);
        if (element.type === "tel") best = definitions[1], bestScore = Math.max(bestScore, 0.96);
        return best ? { definition: best, confidence: bestScore } : null;
    }
    function optionValue(select, wanted) {
        var target = normalize(wanted), options = select.options || [], best = null, bestScore = 0;
        for (var i = 0; i < options.length; i++) {
            var option = options[i], candidate = normalize((option.text || "") + " " + (option.value || ""));
            if (!candidate || option.disabled) continue;
            var current = candidate === target ? 1 : (candidate.indexOf(target) >= 0 || target.indexOf(candidate) >= 0 ? 0.85 : 0);
            if ((target === "chile" || target === "cl" || target === "chl") && hasAny(candidate, ["chile", "cl", "chl"])) current = 1;
            if ((target === "true" || target === "yes" || target === "si" || target === "sí") && hasAny(candidate, ["yes", "si", "sí", "true"])) current = Math.max(current, 0.8);
            if ((target === "false" || target === "no") && hasAny(candidate, ["no", "false"])) current = Math.max(current, 0.8);
            if (current > bestScore) best = option, bestScore = current;
        }
        return bestScore >= 0.78 ? best : null;
    }
    function setValue(element, value) {
        var stringValue = text(value);
        if (element.tagName.toLowerCase() === "select") {
            var option = optionValue(element, stringValue);
            if (!option) return false;
            element.value = option.value;
        } else {
            var prototype = element.tagName.toLowerCase() === "textarea" ? global.HTMLTextAreaElement.prototype : global.HTMLInputElement.prototype;
            var setter = Object.getOwnPropertyDescriptor(prototype, "value");
            if (setter && setter.set) setter.set.call(element, stringValue); else element.value = stringValue;
        }
        ["input", "change", "blur"].forEach(function (name) {
            try { element.dispatchEvent(new Event(name, { bubbles: true })); } catch (error) { }
        });
        return text(element.value) === stringValue || element.tagName.toLowerCase() === "select";
    }
    function formatValue(element, key, value, label) {
        if (key === "relocation" || key === "sponsorship") value = value ? "Yes" : "No";
        if (key === "startDate" || key === "endDate") {
            var normalizedLabel = normalize(label);
            var match = /^(\d{4})-(\d{2})/.exec(text(value));
            if (match && hasAny(normalizedLabel, ["year", "ano", "año"]) && !hasAny(normalizedLabel, ["date", "fecha"])) return match[1];
            if (match && hasAny(normalizedLabel, ["month", "mes"]) && !hasAny(normalizedLabel, ["date", "fecha"])) return match[2];
            var kind = element.type === "month" ? "month" : "date";
            return isoDate(value, kind);
        }
        return value;
    }
    function highlight(element, review) {
        if (!element.dataset.rpmJobOriginalBorder) element.dataset.rpmJobOriginalBorder = element.style.border;
        if (!element.dataset.rpmJobOriginalBackground) element.dataset.rpmJobOriginalBackground = element.style.backgroundColor;
        element.style.border = review ? "2px solid #c58b00" : "2px solid #2d8a56";
        if (review) element.style.backgroundColor = "#fff8dc";
        element.dataset.rpmJobApply = review ? "review" : "filled";
    }
    function sectionIndex(element, key) {
        var container = element.closest ? element.closest("fieldset, section, li, [data-testid], [class*='experience'], [class*='education']") : null;
        var textValue = container ? normalize(container.innerText || container.textContent) : "";
        var index = 0;
        if (container && container.parentElement) {
            var siblings = container.parentElement.children;
            for (var i = 0; i < siblings.length; i++) if (siblings[i] === container) break; else if (normalize(siblings[i].innerText || siblings[i].textContent).indexOf(textValue.slice(0, 16)) >= 0) index++;
        }
        return hasAny(textValue, ["education", "educacion", "educación"]) && !hasAny(textValue, ["experience", "employment", "experiencia", "empleo"]) ? { kind: "education", index: index } : { kind: "experience", index: index };
    }
    function fill(profile, options) {
        options = options || {};
        if (!profile) return { filled: [], review: [], skipped: [], resumeFieldFound: false, error: "Se requiere una sesión de llenado explícita." };
        state.profile = profile;
        if (state.filling) return { filled: [], review: [], skipped: [], resumeFieldFound: false, busy: true };
        state.filling = true;
        var output = { filled: [], review: [], skipped: [], resumeFieldFound: false };
        try {
            var controls = document.querySelectorAll("input, select, textarea");
            if (!controls || controls.length === 0) {
                installObserver();
                scheduleDynamicRetry();
                return output;
            }
            state.retryCount = 0;
            for (var i = 0; i < controls.length; i++) {
                var element = controls[i], type = normalize(element.type || element.tagName);
                var label = controlText(element);
                if (element.type === "file") {
                    if (!visible(element)) continue;
                    if (hasAny(label, ["resume", "cv", "curriculum", "curriculo", "currículum"])) { element.setAttribute("data-rpm-job-file", "resume"); output.resumeFieldFound = true; }
                    continue;
                }
                if (!visible(element)) continue;
                if (element.type === "password" || type === "button" || type === "submit") continue;
                if (isProtected(label)) {
                    output.skipped.push({ label: label || "Campo protegido", reason: "manual confirmation" });
                    continue;
                }
                if (element.type === "checkbox" || element.type === "radio") {
                    if (hasAny(label, ["work authorization", "sponsorship", "visa", "relocate", "autorizacion", "patrocinio", "reubicar"]))
                        output.review.push({ label: label || "Respuesta sí/no", reason: "manual confirmation" });
                    continue;
                }
                var match = bestDefinition(element, label);
                if (!match || match.confidence < 0.72) continue;
                var section = sectionIndex(element, match.definition.key), index = section.index;
                var value = profileValue(profile, match.definition.key, index);
                if (match.definition.key === "company" || match.definition.key === "jobTitle" || match.definition.key === "location" || match.definition.key === "startDate" || match.definition.key === "endDate" || match.definition.key === "description") {
                    var experiences = get(profile, "Experiences") || [];
                    if (!experiences[index]) value = profileValue(profile, match.definition.key, null);
                }
                if (match.definition.key === "degree" || match.definition.key === "fieldOfStudy") {
                    var education = get(profile, "Education") || [];
                    if (!education[index]) value = "";
                }
                if (value === "" || value == null) continue;
                value = formatValue(element, match.definition.key, value, label);
                if (!value) continue;
                if (text(element.value).trim() && !get(profile, "OverwriteExisting")) { output.skipped.push({ label: label || match.definition.key, field: match.definition.key, reason: "existing" }); continue; }
                var review = !!match.definition.review || match.confidence < 0.88;
                if (element.dataset && element.dataset.rpmJobApplyValue === text(value) &&
                    text(element.value) === text(value)) continue;
                if (setValue(element, value)) {
                    highlight(element, review);
                    if (element.dataset) element.dataset.rpmJobApplyValue = text(value);
                    (review ? output.review : output.filled).push({ label: label || match.definition.key, field: match.definition.key, confidence: Math.round(match.confidence * 100) / 100 });
                } else output.review.push({ label: label || match.definition.key, field: match.definition.key, reason: "verification" });
            }
            installObserver();
            return output;
        } finally { state.filling = false; }
    }
    function installObserver() {
        if (!global.MutationObserver && !document.body) return;
        if (state.observer || !global.MutationObserver || !document.body) {
            if (!document.body) scheduleDynamicRetry();
            return;
        }
        state.observer = new MutationObserver(function () {
            if (state.timer) global.clearTimeout(state.timer);
            state.timer = global.setTimeout(function () { if (state.profile && !state.filling) fill(state.profile, { silent: true }); }, 350);
        });
        state.observer.observe(document.body, { childList: true, subtree: true });
    }
    function scheduleDynamicRetry() {
        if (!global.MutationObserver && !document.body) return;
        if (state.timer || !state.profile || state.retryCount >= MAX_DYNAMIC_RETRIES) return;
        state.retryCount++;
        state.timer = global.setTimeout(function () {
            state.timer = null;
            if (state.profile && !state.filling) fill(state.profile, { silent: true, retry: true });
        }, 500);
    }
    var api = { version: VERSION, __rpmJobApplyOwned: true, fill: fill, normalize: normalize };
    try {
        Object.defineProperty(global, "RpmJobApply", {
            value: Object.freeze ? Object.freeze(api) : api,
            configurable: false,
            enumerable: false,
            writable: false
        });
    } catch (error) {
        global.RpmJobApply = api;
    }
})(window);
