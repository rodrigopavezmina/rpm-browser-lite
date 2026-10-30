(function (global) {
    "use strict";
    if (global.RpmJobApplyAudit && global.RpmJobApplyAudit.__rpmJobApplyAuditOwned) return;

    var VERSION = "1.0.0";
    var MAX_TEXT = 300;

    function text(value) { return value == null ? "" : String(value); }
    function clean(value) {
        return text(value).replace(/[\r\n\t]+/g, " ").replace(/\s+/g, " ").trim().slice(0, MAX_TEXT);
    }
    function attr(element, name) {
        try { return element && element.getAttribute ? clean(element.getAttribute(name)) : ""; }
        catch (error) { return ""; }
    }
    function nodeText(node) {
        if (!node) return "";
        return clean(node.innerText || node.textContent || "");
    }
    function addUnique(list, value) {
        value = clean(value);
        if (!value || list.indexOf(value) >= 0) return;
        list.push(value);
    }
    function byId(id) {
        if (!id || !global.document || !document.getElementById) return null;
        try { return document.getElementById(id); } catch (error) { return null; }
    }
    function associatedLabels(element) {
        var labels = [], i, node, id;
        try {
            if (element.labels) for (i = 0; i < element.labels.length; i++) addUnique(labels, nodeText(element.labels[i]));
        } catch (error) { }
        var labelledBy = attr(element, "aria-labelledby").split(/\s+/);
        for (i = 0; i < labelledBy.length; i++) {
            node = byId(labelledBy[i]);
            if (node) addUnique(labels, nodeText(node));
        }
        id = attr(element, "id");
        if (id && document.querySelectorAll) {
            try {
                var explicit = document.querySelectorAll("label[for='" + id.replace(/'/g, "\\'") + "']");
                for (i = 0; i < explicit.length; i++) addUnique(labels, nodeText(explicit[i]));
            } catch (error2) { }
        }
        var parent = element.parentElement;
        for (i = 0; parent && i < 3; i++, parent = parent.parentElement) {
            if (String(parent.tagName).toLowerCase() === "label") addUnique(labels, nodeText(parent));
        }
        return labels;
    }
    function fieldLabels(element) {
        var labels = associatedLabels(element);
        var ariaLabel = attr(element, "aria-label");
        var placeholder = attr(element, "placeholder");
        var name = attr(element, "name");
        var id = attr(element, "id");
        var title = attr(element, "title");
        var label = labels.length ? labels[0] : (ariaLabel || placeholder || name || id || title);
        return {
            label: clean(label),
            labels: labels,
            ariaLabel: ariaLabel,
            placeholder: placeholder,
            name: name,
            id: id,
            autocomplete: attr(element, "autocomplete"),
            title: title
        };
    }
    function visibility(element) {
        if (!element) return { visible: false, reason: "missing" };
        if (element.hidden || attr(element, "aria-hidden") === "true") return { visible: false, reason: "hidden-attribute" };
        if (String(element.type).toLowerCase() === "hidden") return { visible: false, reason: "input-hidden" };
        try {
            var style = global.getComputedStyle ? global.getComputedStyle(element) : null;
            if (style && style.display === "none") return { visible: false, reason: "display-none" };
            if (style && style.visibility === "hidden") return { visible: false, reason: "visibility-hidden" };
            var rect = element.getBoundingClientRect ? element.getBoundingClientRect() : null;
            if (rect && (rect.width <= 0 || rect.height <= 0)) return { visible: false, reason: "zero-size" };
        } catch (error) {
            return { visible: false, reason: "visibility-check-failed" };
        }
        return { visible: true, reason: "visible" };
    }
    function sectionContext(element) {
        var path = [], landmark = "", fieldset = "", node = element && element.parentElement;
        for (var level = 0; node && level < 8; level++, node = node.parentElement) {
            var tag = String(node.tagName || "").toLowerCase();
            var role = attr(node, "role");
            if (tag === "fieldset") {
                var legend = node.querySelector ? node.querySelector("legend") : null;
                if (!fieldset) fieldset = nodeText(legend) || attr(node, "aria-label");
            }
            if (tag === "section" || tag === "article" || tag === "main" || role === "group" || role === "region" || role === "form") {
                var heading = node.querySelector ? node.querySelector("h1,h2,h3,h4,h5,h6,[role='heading']") : null;
                var sectionName = nodeText(heading) || attr(node, "aria-label") || attr(node, "data-testid");
                addUnique(path, sectionName || tag);
                if (!landmark) landmark = role || tag;
            }
        }
        if (fieldset) path.unshift(fieldset);
        return { section: path.reverse().join(" > "), landmark: landmark, fieldset: fieldset };
    }
    function validation(element) {
        var messages = [], describedBy = [], ids = attr(element, "aria-describedby").split(/\s+/), i, node;
        try { if (element.validationMessage) addUnique(messages, element.validationMessage); } catch (error) { }
        if (attr(element, "aria-invalid") === "true") addUnique(messages, "aria-invalid");
        var errorId = attr(element, "aria-errormessage");
        node = byId(errorId);
        if (node) addUnique(messages, nodeText(node));
        for (i = 0; i < ids.length; i++) {
            node = byId(ids[i]);
            if (node) addUnique(describedBy, nodeText(node));
        }
        var parent = element.parentElement;
        for (i = 0; parent && i < 3; i++, parent = parent.parentElement) {
            try {
                var alerts = parent.querySelectorAll ? parent.querySelectorAll("[role='alert'],.error,.invalid,[aria-live]") : [];
                for (var j = 0; j < alerts.length; j++) addUnique(messages, nodeText(alerts[j]));
            } catch (error2) { }
        }
        return { messages: messages, describedBy: describedBy };
    }
    function isRequired(element) {
        var requiredAttribute = false;
        try { requiredAttribute = !!(element.hasAttribute && element.hasAttribute("required")); } catch (error) { }
        return !!(element.required || requiredAttribute || attr(element, "aria-required").toLowerCase() === "true");
    }
    function optionList(element) {
        var options = [], i, option;
        if (String(element.tagName).toLowerCase() === "select") {
            for (i = 0; element.options && i < element.options.length; i++) {
                option = element.options[i];
                options.push({ label: clean(option.text || option.label), value: attr(option, "value") || text(option.value), disabled: !!option.disabled, selected: !!option.selected });
            }
        } else if (String(element.type).toLowerCase() === "radio" && attr(element, "name") && document.querySelectorAll) {
            try {
                var radios = document.querySelectorAll("input[type='radio'][name='" + attr(element, "name").replace(/'/g, "\\'") + "']");
                for (i = 0; i < radios.length; i++) {
                    option = radios[i];
                    var radioLabels = fieldLabels(option);
                    options.push({ label: radioLabels.label, value: attr(option, "value") || text(option.value), disabled: !!option.disabled, selected: !!option.checked });
                }
            } catch (error) { }
        } else if (String(element.type).toLowerCase() === "checkbox") {
            var checkboxLabels = fieldLabels(element);
            options.push({ label: checkboxLabels.label, value: attr(element, "value") || text(element.value) || "on", disabled: !!element.disabled, selected: !!element.checked });
        }
        return options;
    }
    function fnv1a(value) {
        var hash = 2166136261, i;
        for (i = 0; i < value.length; i++) {
            hash ^= value.charCodeAt(i);
            hash += (hash << 1) + (hash << 4) + (hash << 7) + (hash << 8) + (hash << 24);
        }
        return (hash >>> 0).toString(16).padStart ? (hash >>> 0).toString(16).padStart(8, "0") : ("00000000" + (hash >>> 0).toString(16)).slice(-8);
    }
    function collectControls(root, output) {
        if (!root || !root.querySelectorAll) return;
        var nodes, i, all, frame, shadow;
        try {
            nodes = root.querySelectorAll("input,select,textarea,[contenteditable]");
            for (i = 0; i < nodes.length; i++) if (output.indexOf(nodes[i]) < 0) output.push(nodes[i]);
            all = root.querySelectorAll("*");
            for (i = 0; i < all.length; i++) {
                shadow = all[i].shadowRoot;
                if (shadow) collectControls(shadow, output);
            }
            var frames = root.querySelectorAll("iframe,frame");
            for (i = 0; i < frames.length; i++) {
                frame = frames[i];
                try { if (frame.contentDocument) collectControls(frame.contentDocument, output); } catch (error) { }
            }
        } catch (error2) { }
    }
    function inspect() {
        var nodes = [], fields = [], i;
        collectControls(document, nodes);
        for (i = 0; i < nodes.length; i++) {
            var element = nodes[i], labels = fieldLabels(element), context = sectionContext(element), type;
            var editable = attr(element, "contenteditable").toLowerCase();
            var hasEditableAttribute = false;
            try { hasEditableAttribute = !!(element.hasAttribute && element.hasAttribute("contenteditable")); } catch (error) { }
            if (hasEditableAttribute && editable === "false") continue;
            if (hasEditableAttribute) type = "contenteditable";
            else type = String(element.tagName).toLowerCase() === "input" ? (attr(element, "type").toLowerCase() || "text") : String(element.tagName).toLowerCase();
            var visibilityInfo = visibility(element), validationInfo = validation(element), options = optionList(element);
            var ordinal = i + 1;
            var canonical = [ordinal, type, labels.label, labels.name, labels.id, labels.autocomplete, context.section, context.fieldset].join("|");
            fields.push({
                fingerprint: "f-" + fnv1a(canonical),
                ordinal: ordinal,
                type: type,
                label: labels.label,
                labels: labels.labels,
                ariaLabel: labels.ariaLabel,
                placeholder: labels.placeholder,
                autocomplete: labels.autocomplete,
                name: labels.name,
                id: labels.id,
                title: labels.title,
                accept: attr(element, "accept"),
                multiple: !!element.multiple,
                options: options,
                required: isRequired(element),
                visibility: visibilityInfo,
                disabled: !!element.disabled,
                validation: validationInfo,
                context: context
            });
        }
        var locationValue = global.location ? text(global.location.href) : "";
        return {
            schemaVersion: "1.0",
            page: { url: locationValue, title: clean(global.document && document.title) },
            fieldCount: fields.length,
            fields: fields
        };
    }
    var api = { version: VERSION, __rpmJobApplyAuditOwned: true, inspect: inspect };
    try {
        Object.defineProperty(global, "RpmJobApplyAudit", {
            value: Object.freeze ? Object.freeze(api) : api,
            configurable: false,
            enumerable: false,
            writable: false
        });
    } catch (error) { global.RpmJobApplyAudit = api; }
})(window);
