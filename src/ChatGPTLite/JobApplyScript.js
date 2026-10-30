(function (global) {
    "use strict";
    if (global.RpmJobApply && global.RpmJobApply.__rpmJobApplyOwned) return;

    var VERSION = "2.0.0";
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
    function profileValue(profile, key, index, kind) {
        if (key === "fullName") return joinName(profile);
        var experiences = get(profile, "Experiences") || [];
        var education = get(profile, "Education") || [];
        var item = index == null ? null : (kind === "education" ? education[index] : experiences[index]);
        if (key === "company") return item ? firstNonEmpty([get(item, "Company")]) : "";
        if (key === "jobTitle") return item ? firstNonEmpty([get(item, "Title")]) : firstNonEmpty([get(profile, "CurrentTitle"), get(profile, "DesiredRole")]);
        if (key === "location") return item ? get(item, "Location") : "";
        if (key === "startDate") return item ? get(item, "StartDate") : get(profile, "AvailableStartDate");
        if (key === "endDate") return item ? (get(item, "IsCurrent") ? "Present" : get(item, "EndDate")) : "";
        if (key === "description") return item ? get(item, "Description") : "";
        if (key === "institution") return item ? get(item, "Institution") : "";
        if (key === "degree") return item ? get(item, "Degree") : "";
        if (key === "fieldOfStudy") return item ? get(item, "FieldOfStudy") : "";
        var map = {
            firstName: "FirstName", middleName: "MiddleName", lastName: "LastName", preferredName: "PreferredName", email: "Email", phone: "Phone",
            address: "AddressLine1", address2: "AddressLine2", city: "City", region: "Region", postal: "PostalCode", country: "Country",
            linkedin: "LinkedInUrl", portfolio: "PortfolioUrl", website: "PortfolioUrl", github: "GitHubUrl", years: "YearsExperience", authorization: "WorkAuthorization",
            sponsorship: "RequiresSponsorship", relocation: "WillingToRelocate", salary: "SalaryExpectation", skills: "Skills",
            workMode: "WorkMode", employmentType: "EmploymentType", availableStartDate: "AvailableStartDate"
        };
        return map[key] ? get(profile, map[key]) : "";
    }

    var definitions; /* legacy matcher table retained only as historical source:
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
    */
    function ontologyDefinition(canonicalField, key, category, terms, options) {
        options = options || {};
        return {
            canonicalField: canonicalField, key: key, category: category, terms: terms || [],
            auto: options.auto || [], inputType: options.inputType || "", context: options.context || "",
            contextTerms: options.contextTerms || [], negativeTerms: options.negativeTerms || [],
            review: !!options.review, sensitive: !!options.sensitive, file: !!options.file,
            manual: !!options.manual
        };
    }

    // Bilingual canonical employment ontology. The legacy definitions above are
    // replaced here at runtime so old embedded callers remain compatible.
    definitions = [
        ontologyDefinition("identity.firstName", "firstName", "identity", ["first name", "given name", "forename", "nombre", "nombre de pila", "nombres"], { auto: ["given-name"] }),
        ontologyDefinition("identity.middleName", "middleName", "identity", ["middle name", "second name", "segundo nombre"]),
        ontologyDefinition("identity.lastName", "lastName", "identity", ["last name", "family name", "surname", "apellido", "apellidos"], { auto: ["family-name"] }),
        ontologyDefinition("identity.preferredName", "preferredName", "identity", ["preferred name", "display name", "nombre preferido"]),
        ontologyDefinition("identity.fullName", "fullName", "identity", ["full name", "complete name", "nombre completo"]),
        ontologyDefinition("contact.email", "email", "contact", ["email", "e mail", "email address", "correo", "correo electronico"], { inputType: "email", auto: ["email"] }),
        ontologyDefinition("contact.phone", "phone", "contact", ["phone", "telephone", "mobile", "cell", "phone number", "telefono", "celular", "numero telefonico"], { inputType: "tel", auto: ["tel", "tel-national"] }),
        ontologyDefinition("address.line1", "address", "address", ["address", "street address", "street", "direccion", "direccion de calle", "calle"], { auto: ["street-address"] }),
        ontologyDefinition("address.line2", "address2", "address", ["address line 2", "apartment", "unit", "suite", "address complement", "complemento", "departamento"]),
        ontologyDefinition("address.city", "city", "address", ["city", "town", "municipality", "ciudad", "comuna"], { auto: ["address-level2"] }),
        ontologyDefinition("address.region", "region", "address", ["state", "province", "region", "estado", "provincia", "region administrativa"], { auto: ["address-level1"] }),
        ontologyDefinition("address.postalCode", "postal", "address", ["postal code", "zip code", "zip", "postcode", "codigo postal"], { auto: ["postal-code"] }),
        ontologyDefinition("address.country", "country", "address", ["country", "pais", "país", "country of residence"], { auto: ["country"] }),
        ontologyDefinition("links.linkedin", "linkedin", "links", ["linkedin", "linked in", "linkedin profile"]),
        ontologyDefinition("links.github", "github", "links", ["github", "git hub", "github profile"]),
        ontologyDefinition("links.portfolio", "portfolio", "links", ["portfolio", "website", "personal website", "professional website", "sitio personal", "web personal"]),
        ontologyDefinition("links.website", "website", "links", ["website", "web site", "personal url", "url personal", "pagina web"]),
        ontologyDefinition("experience.jobTitle", "jobTitle", "experience", ["job title", "current title", "position", "role", "cargo", "puesto", "titulo profesional", "titulo del cargo"], { context: "experience", contextTerms: ["experience", "employment", "work history", "experiencia", "empleo", "historial laboral"] }),
        ontologyDefinition("experience.company", "company", "experience", ["company", "employer", "organization", "employer name", "empresa", "empleador", "organizacion"], { context: "experience", contextTerms: ["experience", "employment", "work history", "experiencia", "empleo", "historial laboral"] }),
        ontologyDefinition("experience.location", "location", "experience", ["work location", "job location", "location", "ubicacion", "ubicacion laboral"], { context: "experience", contextTerms: ["experience", "employment", "work history", "experiencia", "empleo"] }),
        ontologyDefinition("experience.startDate", "startDate", "experience", ["start date", "start month", "start year", "date started", "fecha de inicio", "mes de inicio", "ano de inicio", "desde"], { context: "experience", contextTerms: ["experience", "employment", "work history", "experiencia", "empleo"] }),
        ontologyDefinition("experience.endDate", "endDate", "experience", ["end date", "end month", "end year", "date ended", "fecha de termino", "mes de termino", "ano de termino", "hasta"], { context: "experience", contextTerms: ["experience", "employment", "work history", "experiencia", "empleo"] }),
        ontologyDefinition("experience.description", "description", "experience", ["responsibilities", "description", "duties", "achievements", "responsabilidades", "descripcion", "logros"], { context: "experience", contextTerms: ["experience", "employment", "work history", "experiencia", "empleo"] }),
        ontologyDefinition("experience.years", "years", "experience", ["years of experience", "years experience", "professional experience", "anos de experiencia", "aÃ±os de experiencia"], { review: true }),
        ontologyDefinition("education.institution", "institution", "education", ["school", "university", "college", "institution", "educational institution", "universidad", "institucion", "instituto"], { context: "education", contextTerms: ["education", "academic", "educacion", "formacion", "estudios"] }),
        ontologyDefinition("education.degree", "degree", "education", ["degree", "education level", "qualification", "academic degree", "grado", "titulo", "nivel educacional"], { context: "education", contextTerms: ["education", "academic", "educacion", "formacion", "estudios"] }),
        ontologyDefinition("education.fieldOfStudy", "fieldOfStudy", "education", ["field of study", "major", "specialization", "area of study", "carrera", "especialidad", "area de estudio"], { context: "education", contextTerms: ["education", "academic", "educacion", "formacion", "estudios"] }),
        ontologyDefinition("education.startDate", "startDate", "education", ["education start date", "study start", "start of studies", "inicio de estudios"], { context: "education", contextTerms: ["education", "academic", "educacion", "formacion", "estudios"] }),
        ontologyDefinition("education.endDate", "endDate", "education", ["education end date", "graduation date", "study end", "fin de estudios", "fecha de graduacion"], { context: "education", contextTerms: ["education", "academic", "educacion", "formacion", "estudios"] }),
        ontologyDefinition("authorization.work", "authorization", "authorization", ["work authorization", "authorized to work", "right to work", "work permit", "permiso de trabajo", "autorizacion laboral", "autorizacion de trabajo"], { review: true }),
        ontologyDefinition("authorization.sponsorship", "sponsorship", "authorization", ["visa sponsorship", "sponsorship", "employer sponsorship", "sponsor", "patrocinio", "auspicio de visa"], { review: true }),
        ontologyDefinition("availability.startDate", "availableStartDate", "availability", ["available start date", "availability date", "when can you start", "start availability", "fecha de disponibilidad", "disponibilidad para comenzar"], { review: true }),
        ontologyDefinition("availability.relocation", "relocation", "availability", ["relocate", "relocation", "willing to move", "move for work", "reubicacion", "dispuesto a trasladarse"], { review: true }),
        ontologyDefinition("availability.workMode", "workMode", "availability", ["work mode", "work arrangement", "remote", "hybrid", "onsite", "modalidad", "remoto", "hibrido", "presencial"]),
        ontologyDefinition("availability.employmentType", "employmentType", "availability", ["employment type", "job type", "contract type", "full time", "part time", "tipo de empleo", "tipo de contrato", "tiempo completo", "media jornada"]),
        ontologyDefinition("availability.noticePeriod", "noticePeriod", "availability", ["notice period", "start notice", "availability notice", "periodo de aviso", "preaviso"], { review: true, manual: true }),
        ontologyDefinition("salary.expectation", "salary", "salary", ["salary", "compensation", "pay expectation", "expected salary", "salary expectation", "pretension de renta", "remuneracion", "expectativa salarial"], { review: true }),
        ontologyDefinition("skills.list", "skills", "skills", ["skills", "technical skills", "key skills", "competencies", "habilidades", "competencias", "conocimientos tecnicos"]),
        ontologyDefinition("files.resume", "resume", "files", ["resume", "cv", "curriculum", "curriculum vitae", "curriculo", "hoja de vida"], { file: true, manual: true }),
        ontologyDefinition("files.coverLetter", "coverLetter", "files", ["cover letter", "motivation letter", "carta de presentacion", "carta de motivacion"], { file: true, manual: true }),
        ontologyDefinition("files.portfolio", "portfolioFile", "files", ["portfolio file", "work sample", "writing sample", "muestra de trabajo"], { file: true, manual: true }),
        ontologyDefinition("questions.open", "openQuestion", "questions", ["open question", "additional information", "other information", "comments", "tell us about yourself", "informacion adicional", "comentarios"], { manual: true }),
        ontologyDefinition("questions.motivation", "motivation", "questions", ["why do you want", "why interested", "motivation", "why this role", "por que te interesa", "motivacion"], { manual: true }),
        ontologyDefinition("questions.whyCompany", "whyCompany", "questions", ["why this company", "why us", "por que esta empresa", "por que nosotros"], { manual: true }),
        ontologyDefinition("sensitive.birthDate", "birthDate", "sensitive", ["date of birth", "birth date", "dob", "fecha de nacimiento"], { sensitive: true, manual: true }),
        ontologyDefinition("sensitive.gender", "gender", "sensitive", ["gender", "sex", "genero", "sexo"], { sensitive: true, manual: true }),
        ontologyDefinition("sensitive.ethnicity", "ethnicity", "sensitive", ["ethnicity", "race", "etnia", "raza"], { sensitive: true, manual: true }),
        ontologyDefinition("sensitive.disability", "disability", "sensitive", ["disability", "discapacidad"], { sensitive: true, manual: true }),
        ontologyDefinition("sensitive.veteran", "veteran", "sensitive", ["veteran", "military service", "veterano", "servicio militar"], { sensitive: true, manual: true }),
        ontologyDefinition("sensitive.criminalRecord", "criminalRecord", "sensitive", ["criminal record", "background check", "antecedentes penales", "registro criminal"], { sensitive: true, manual: true }),
        ontologyDefinition("sensitive.consent", "consent", "sensitive", ["consent", "terms", "agreement", "privacy", "consentimiento", "terminos", "acuerdo", "privacidad"], { sensitive: true, manual: true })
    ];

    function contextText(element) {
        var container = element && element.closest ? element.closest("fieldset, section, li, [data-testid], [class*='experience'], [class*='education']") : null;
        return container ? normalize(container.innerText || container.textContent) : "";
    }
    function signalValues(element, label) {
        return {
            label: normalize(label), name: normalize(element.getAttribute && element.getAttribute("name")),
            id: normalize(element.getAttribute && element.getAttribute("id")),
            placeholder: normalize(element.getAttribute && element.getAttribute("placeholder")),
            title: normalize(element.getAttribute && element.getAttribute("title")),
            autocomplete: normalize(element.getAttribute && element.getAttribute("autocomplete")),
            type: normalize(element.type || element.tagName), context: contextText(element)
        };
    }
    function termScore(value, terms, source, signals) {
        if (!value) return 0;
        var best = 0, bestKind = "", bestTerm = "", words, found, i, j, term, current;
        for (i = 0; i < terms.length; i++) {
            term = normalize(terms[i]);
            if (!term) continue;
            current = 0;
            if (value === term) { current = source === "label" ? 0.9 : 0.78; if (term.length <= 3) current -= 0.06; }
            else if (value.indexOf(term) >= 0) current = source === "label" ? 0.62 : 0.54;
            else {
                words = term.split(" "); found = 0;
                for (j = 0; j < words.length; j++) if (value.indexOf(words[j]) >= 0) found++;
                if (words.length > 1 && found === words.length) current = source === "label" ? 0.55 : 0.48;
            }
            if (current > best) { best = current; bestKind = current >= 0.7 ? "exact" : (current >= 0.6 ? "contains" : "words"); bestTerm = term; }
        }
        if (best > 0) signals.push(source + ":" + bestKind + ":" + bestTerm);
        return best;
    }
    function ontologyScore(definitionValue, label, element) {
        var signals = [], conflicts = [], values = signalValues(element, label), base = 0, support = 0, local, i;
        ["label", "name", "id", "placeholder", "title"].forEach(function (key) {
            local = termScore(values[key], definitionValue.terms, key === "label" ? "label" : "attribute", signals);
            if (local > base) base = local; else if (local >= 0.48) support += 0.07;
        });
        var autoMatch = false;
        if (definitionValue.auto) for (i = 0; i < definitionValue.auto.length; i++) if (normalize(definitionValue.auto[i]) === values.autocomplete) autoMatch = true;
        if (autoMatch) { base = Math.max(base, 0.9); signals.push("autocomplete:" + values.autocomplete); }
        if (definitionValue.inputType && values.type === definitionValue.inputType) { base = Math.max(base, base > 0 ? base + 0.1 : 0.88); signals.push("type:" + values.type); }
        if (definitionValue.contextTerms && hasAny(values.context, definitionValue.contextTerms)) { base += 0.18; signals.push("context:" + definitionValue.context); }
        if (definitionValue.context === "experience" && hasAny(values.context, ["education", "academic", "educacion", "estudios"])) { conflicts.push({ canonicalField: "education", reason: "education-context" }); base -= 0.18; }
        if (definitionValue.context === "education" && hasAny(values.context, ["experience", "employment", "work history", "experiencia", "empleo"])) { conflicts.push({ canonicalField: "experience", reason: "experience-context" }); base -= 0.18; }
        for (i = 0; i < definitionValue.negativeTerms.length; i++) if (hasAny(values.label, [definitionValue.negativeTerms[i]])) { conflicts.push({ canonicalField: definitionValue.canonicalField, reason: "negative-signal:" + normalize(definitionValue.negativeTerms[i]) }); base -= 0.2; }
        return { definition: definitionValue, score: Math.max(0, Math.min(1, base + Math.min(0.14, support))), signals: signals, conflicts: conflicts };
    }
    function bestDefinition(element, label) {
        var candidates = [], i, candidate, best, second, ambiguous;
        for (i = 0; i < definitions.length; i++) { candidate = ontologyScore(definitions[i], label, element); if (candidate.score > 0) candidates.push(candidate); }
        candidates.sort(function (a, b) { return b.score - a.score; });
        best = candidates.length ? candidates[0] : null; second = candidates.length > 1 ? candidates[1] : null;
        if (!best) return null;
        ambiguous = best.score >= 0.72 && second && second.score >= 0.72 && (best.score - second.score) <= 0.08;
        if (second && second.score >= 0.72 && (best.score - second.score) <= 0.12) best.conflicts.push({ canonicalField: second.definition.canonicalField, score: Math.round(second.score * 100) / 100, reason: "near-tie" });
        var contextConflict = best.conflicts.some(function (conflict) {
            return conflict.reason === "education-context" || conflict.reason === "experience-context";
        });
        best.ambiguous = !!ambiguous || contextConflict;
        best.confidence = best.score;
        best.reason = ambiguous ? "ambiguous-near-tie" : (contextConflict ? "context-conflict" : "best-ontology-match");
        return best;
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
    function matchDecision(match, label, reason, action) {
        var definitionValue = match && match.definition ? match.definition : null;
        return {
            label: label || (definitionValue ? definitionValue.key : "Campo"),
            field: definitionValue ? definitionValue.key : "",
            canonicalField: definitionValue ? definitionValue.canonicalField : "",
            category: definitionValue ? definitionValue.category : "",
            score: match ? Math.round(match.score * 100) / 100 : 0,
            signals: match && match.signals ? match.signals.slice(0) : [],
            conflicts: match && match.conflicts ? match.conflicts.slice(0) : [],
            reason: reason || (match ? match.reason : "no-match"),
            action: action || "skipped"
        };
    }
    function addDecision(output, bucket, match, label, reason, action) {
        var decision = matchDecision(match, label, reason, action);
        output.matches.push(decision);
        if (bucket) bucket.push(decision);
        return decision;
    }
    function fill(profile, options) {
        options = options || {};
        if (!profile) return { filled: [], review: [], skipped: [], resumeFieldFound: false, error: "Se requiere una sesión de llenado explícita." };
        state.profile = profile;
        if (!options.retry) {
            if (state.timer) global.clearTimeout(state.timer);
            state.timer = null;
            state.retryCount = 0;
        }
        if (state.filling) return { filled: [], review: [], skipped: [], matches: [], resumeFieldFound: false, busy: true };
        state.filling = true;
        var output = { filled: [], review: [], skipped: [], matches: [], resumeFieldFound: false };
        var resumeDetectedCount = 0;
        try {
            var controls = document.querySelectorAll("input, select, textarea");
            if (!controls || controls.length === 0) {
                installObserver();
                scheduleDynamicRetry();
                return output;
            }
            for (var i = 0; i < controls.length; i++) {
                var element = controls[i], type = normalize(element.type || element.tagName);
                var label = controlText(element);
                var match = bestDefinition(element, label);
                if (element.type === "file") {
                    if (match && match.score >= 0.72) {
                        if (!visible(element)) addDecision(output, output.skipped, match, label, "hidden-or-disabled", "skipped");
                        else if (!match.ambiguous && match.definition.key === "resume") {
                            element.setAttribute("data-rpm-job-file", "resume");
                            resumeDetectedCount++;
                            addDecision(output, null, match, label, "resume-field-detected-manual-attachment", "detected");
                        } else {
                            if (element.removeAttribute) element.removeAttribute("data-rpm-job-file");
                            addDecision(output, output.skipped, match, label, match.ambiguous ? match.reason : "manual-file-attachment", "skipped");
                        }
                        continue;
                    }
                    if (!visible(element)) {
                        if (match && match.score >= 0.72) addDecision(output, output.skipped, match, label, "hidden-or-disabled", "skipped");
                        continue;
                    }
                    if (hasAny(label, ["resume", "cv", "curriculum", "curriculo", "currículum"])) { element.setAttribute("data-rpm-job-file", "resume"); output.resumeFieldFound = true; }
                    continue;
                }
                if (!visible(element)) {
                    if (match && match.score >= 0.72) addDecision(output, output.skipped, match, label, "hidden-or-disabled", "skipped");
                    continue;
                }
                if (element.type === "password" || type === "button" || type === "submit") {
                    if (match && match.score >= 0.72) addDecision(output, output.skipped, match, label, "protected-control", "skipped");
                    continue;
                }
                if (isProtected(label)) {
                    if (match && match.score >= 0.72) addDecision(output, output.skipped, match, label, "sensitive-manual-confirmation", "skipped");
                    continue;
                }
                if ((element.type === "checkbox" || element.type === "radio") && match && match.score >= 0.72) {
                    addDecision(output, output.review, match, label || "Respuesta si/no", "choice-manual-confirmation", "review");
                    continue;
                }
                if (element.type === "checkbox" || element.type === "radio") {
                    if (hasAny(label, ["work authorization", "sponsorship", "visa", "relocate", "autorizacion", "patrocinio", "reubicar"]))
                        output.review.push({ label: label || "Respuesta sí/no", reason: "manual confirmation" });
                    continue;
                }
                if (!match || match.score < 0.72) {
                    if (match) addDecision(output, output.skipped, match, label, "below-confidence-threshold", "skipped");
                    continue;
                }
                if (match.ambiguous) {
                    addDecision(output, output.skipped, match, label, match.reason || "ambiguous-near-tie", "skipped");
                    continue;
                }
                if (match.definition.sensitive) {
                    addDecision(output, output.skipped, match, label, "sensitive-manual-confirmation", "skipped");
                    continue;
                }
                var section = sectionIndex(element, match.definition.key), index = section.index;
                var kind = match.definition.context === "education" ? "education" : (match.definition.context === "experience" ? "experience" : section.kind);
                var value = profileValue(profile, match.definition.key, index, kind);
                if (match.definition.category === "experience") {
                    var experiences = get(profile, "Experiences") || [];
                    if (!experiences[index]) value = profileValue(profile, match.definition.key, null, kind);
                }
                if (match.definition.category === "education") {
                    var education = get(profile, "Education") || [];
                    if (!education[index]) value = "";
                }
                if (match.definition.manual) {
                    addDecision(output, output.review, match, label, "manual-review-required", "review");
                    continue;
                }
                if (value === "" || value == null) {
                    addDecision(output, output.skipped, match, label, "profile-value-missing", "skipped");
                    continue;
                }
                value = formatValue(element, match.definition.key, value, label);
                if (!value) {
                    addDecision(output, output.skipped, match, label, "formatted-value-empty", "skipped");
                    continue;
                }
                if (text(element.value).trim() && !get(profile, "OverwriteExisting")) {
                    addDecision(output, output.skipped, match, label, "existing-value", "skipped");
                    continue;
                }
                var review = !!match.definition.review || match.confidence < 0.88;
                if (element.dataset && element.dataset.rpmJobApplyValue === text(value) &&
                    text(element.value) === text(value)) {
                    addDecision(output, null, match, label, "already-filled", "unchanged");
                    continue;
                }
                if (setValue(element, value)) {
                    highlight(element, review);
                    if (element.dataset) element.dataset.rpmJobApplyValue = text(value);
                    addDecision(output, review ? output.review : output.filled, match, label, review ? "filled-needs-review" : "filled", review ? "review" : "filled");
                } else addDecision(output, output.review, match, label, "verification-failed", "review");
            }
            output.resumeFieldFound = resumeDetectedCount > 0;
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
            state.timer = null;
            scheduleDynamicRetry();
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
    function getOntology() {
        return definitions.map(function (item) {
            return {
                canonicalField: item.canonicalField,
                key: item.key,
                category: item.category,
                sensitive: !!item.sensitive,
                manual: !!item.manual
            };
        });
    }
    var api = { version: VERSION, ontologyVersion: "2.0", __rpmJobApplyOwned: true, fill: fill, normalize: normalize, getOntology: getOntology };
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
