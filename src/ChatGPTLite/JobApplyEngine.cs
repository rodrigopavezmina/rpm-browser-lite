using Microsoft.Web.WebView2.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ChatGPTLite
{
    /// <summary>
    /// Bridges the native, DPAPI-protected candidate profile with the page-local
    /// form matcher.  The JavaScript is embedded in the executable so job sites
    /// cannot replace the rules or alter the profile stored by the application.
    /// </summary>
    internal static class JobApplyEngine
    {
        private const string ScriptResourceName = "ChatGPTLite.JobApplyScript.js";
        private static readonly TimeSpan FillTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan AttachTimeout = TimeSpan.FromSeconds(20);
        private static readonly object ScriptLock = new object();
        private static string _script;

        public static string Script
        {
            get
            {
                if (_script != null)
                    return _script;
                lock (ScriptLock)
                {
                    if (_script == null)
                        _script = ReadEmbeddedScript();
                }
                return _script;
            }
        }

        public static async Task<JobApplyResult> FillAsync(CoreWebView2 browser, JobApplicationProfile profile)
        {
            return await FillAsync(browser, profile, FillTimeout);
        }

        public static async Task<JobApplyResult> FillAsync(
            CoreWebView2 browser,
            JobApplicationProfile profile,
            TimeSpan timeout)
        {
            if (browser == null)
                throw new ArgumentNullException("browser");
            if (profile == null)
                throw new ArgumentNullException("profile");
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("timeout");

            // AddScriptToExecuteOnDocumentCreatedAsync installs the engine for new
            // pages. Executing it here as well supports the page that was already
            // open when a user updated the application.
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            await AwaitWithDeadline(browser.ExecuteScriptAsync(Script), deadline, "El portal tardó demasiado en preparar el motor.");

            JavaScriptSerializer serializer = CreateSerializer();
            // Never expose the local CV path to page JavaScript. The native host
            // handles the file attachment separately through DevTools Protocol.
            JobApplicationProfile pageProfile = profile.Clone();
            pageProfile.ResumePath = string.Empty;
            // Field provenance can contain original CV text or other sensitive
            // evidence; the page matcher only needs the legacy scalar values.
            pageProfile.FieldMetadata = new Dictionary<string, JobApplicationFieldMetadata>();
            string profileJson = serializer.Serialize(pageProfile);
            string profileJsonLiteral = serializer.Serialize(profileJson);
            string invocation =
                "(function(){try{" +
                "if(!window.RpmJobApply||typeof window.RpmJobApply.fill!=='function')" +
                "return {error:'El motor de postulación no está disponible en esta página.'};" +
                "return window.RpmJobApply.fill(JSON.parse(" + profileJsonLiteral + "));" +
                "}catch(error){return {error:String(error&&error.message?error.message:error)};}})()";
            string rawResult = await AwaitWithDeadline(
                browser.ExecuteScriptAsync(invocation),
                deadline,
                "El portal tardó demasiado en responder al autollenado.");
            return JobApplyResult.Parse(rawResult, serializer);
        }

        /// <summary>
        /// Attaches the CV that the user explicitly selected in the local profile.
        /// Web pages cannot receive arbitrary local paths from JavaScript, so the
        /// trusted desktop host uses the DevTools DOM command for the input that the
        /// matcher marked as a resume field. This runs only after the user clicks
        /// the native Postular button.
        /// </summary>
        public static async Task<ResumeAttachResult> AttachResumeAsync(CoreWebView2 browser, string resumePath)
        {
            if (browser == null)
                return ResumeAttachResult.Failed("El navegador no está listo.");
            if (string.IsNullOrWhiteSpace(resumePath))
                return ResumeAttachResult.Failed("No hay un CV configurado.");

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(resumePath.Trim());
            }
            catch
            {
                return ResumeAttachResult.Failed("La ruta del CV no es válida.");
            }
            if (!File.Exists(fullPath))
                return ResumeAttachResult.Failed("No se encontró el CV configurado.");

            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (extension != ".pdf" && extension != ".doc" && extension != ".docx" && extension != ".rtf" && extension != ".txt")
                return ResumeAttachResult.Failed("El CV debe ser PDF, DOC, DOCX, RTF o TXT.");

            JavaScriptSerializer serializer = CreateSerializer();
            DateTime deadline = DateTime.UtcNow.Add(AttachTimeout);
            string searchId = null;
            try
            {
                Dictionary<string, object> searchParameters = new Dictionary<string, object>();
                searchParameters["query"] = "input[data-rpm-job-file=\"resume\"]";
                searchParameters["includeUserAgentShadowDOM"] = true;
                string searchRaw = await CallDevToolsAsync(
                    browser, "DOM.performSearch", serializer.Serialize(searchParameters), deadline,
                    "El portal tardó demasiado en buscar el campo de CV.");
                Dictionary<string, object> search = ParseDictionary(searchRaw, serializer);
                searchId = GetString(search, "searchId");
                int resultCount = GetInt(search, "resultCount");
                if (string.IsNullOrWhiteSpace(searchId) || resultCount < 1)
                    return ResumeAttachResult.Failed("La página no expone un campo de CV compatible.");

                Dictionary<string, object> resultParameters = new Dictionary<string, object>();
                resultParameters["searchId"] = searchId;
                resultParameters["fromIndex"] = 0;
                resultParameters["toIndex"] = 1;
                string resultsRaw = await CallDevToolsAsync(
                    browser, "DOM.getSearchResults", serializer.Serialize(resultParameters), deadline,
                    "El portal tardó demasiado en identificar el campo de CV.");
                Dictionary<string, object> results = ParseDictionary(resultsRaw, serializer);
                int nodeId = GetFirstInt(results, "nodeIds");
                if (nodeId <= 0)
                    return ResumeAttachResult.Failed("No se pudo identificar el campo de CV.");

                Dictionary<string, object> fileParameters = new Dictionary<string, object>();
                fileParameters["nodeId"] = nodeId;
                fileParameters["files"] = new string[] { fullPath };
                await CallDevToolsAsync(
                    browser, "DOM.setFileInputFiles", serializer.Serialize(fileParameters), deadline,
                    "El portal tardó demasiado en adjuntar el CV.");

                // Dispatch the same events generated by a normal file selection so
                // React/Angular/Vue validation observes the attachment.
                Dictionary<string, object> resolveParameters = new Dictionary<string, object>();
                resolveParameters["nodeId"] = nodeId;
                string resolvedRaw = await CallDevToolsAsync(
                    browser, "DOM.resolveNode", serializer.Serialize(resolveParameters), deadline,
                    "El portal tardó demasiado en validar el CV.");
                Dictionary<string, object> resolved = ParseDictionary(resolvedRaw, serializer);
                Dictionary<string, object> remoteObject = GetDictionary(resolved, "object");
                string objectId = GetString(remoteObject, "objectId");
                if (!string.IsNullOrWhiteSpace(objectId))
                {
                    Dictionary<string, object> callParameters = new Dictionary<string, object>();
                    callParameters["objectId"] = objectId;
                    callParameters["functionDeclaration"] =
                        "function(){this.dispatchEvent(new Event('input',{bubbles:true}));" +
                        "this.dispatchEvent(new Event('change',{bubbles:true}));" +
                        "return this.files?this.files.length:0;}";
                    callParameters["returnByValue"] = true;
                    string eventRaw = await CallDevToolsAsync(
                        browser, "Runtime.callFunctionOn", serializer.Serialize(callParameters), deadline,
                        "El portal tardó demasiado en confirmar el CV.");
                    Dictionary<string, object> eventResult = ParseDictionary(eventRaw, serializer);
                    int fileCount = GetInt(GetDictionary(eventResult, "result"), "value");
                    if (fileCount < 1)
                        return ResumeAttachResult.Failed(
                            "El portal no confirmó el CV. Adjúntalo manualmente y verifica el archivo.");
                }

                else
                {
                    return ResumeAttachResult.Failed(
                        "No se pudo validar el campo de CV. Adjúntalo manualmente y verifica el archivo.");
                }

                return ResumeAttachResult.Attached(Path.GetFileName(fullPath));
            }
            catch (JobApplyTimeoutException ex)
            {
                return ResumeAttachResult.FromTimeout(ex.Message);
            }
            catch (Exception ex)
            {
                return ResumeAttachResult.Failed(
                    "El portal rechazó la carga automática del CV. Adjunta el archivo manualmente.");
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(searchId))
                {
                    try
                    {
                        Dictionary<string, object> discardParameters = new Dictionary<string, object>();
                        discardParameters["searchId"] = searchId;
                        CallDevToolsAsync(
                            browser, "DOM.discardSearchResults", serializer.Serialize(discardParameters),
                            DateTime.UtcNow.AddSeconds(2), string.Empty)
                            .GetAwaiter().GetResult();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static string ReadEmbeddedScript()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(ScriptResourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException("No se encontró el motor de postulación integrado.");
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                    return reader.ReadToEnd();
            }
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = 16 * 1024 * 1024;
            serializer.RecursionLimit = 100;
            return serializer;
        }

        private static async Task<string> CallDevToolsAsync(
            CoreWebView2 browser,
            string method,
            string parameters,
            DateTime deadline,
            string timeoutMessage)
        {
            return await AwaitWithDeadline(
                browser.CallDevToolsProtocolMethodAsync(method, parameters),
                deadline,
                timeoutMessage);
        }

        private static async Task<T> AwaitWithDeadline<T>(Task<T> task, DateTime deadline, string message)
        {
            TimeSpan remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                throw new JobApplyTimeoutException(message);

            Task completed = await Task.WhenAny(task, Task.Delay(remaining));
            if (completed != task)
                throw new JobApplyTimeoutException(message);
            return await task;
        }

        private static Dictionary<string, object> ParseDictionary(string raw, JavaScriptSerializer serializer)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            object parsed = serializer.DeserializeObject(raw);
            string nested = parsed as string;
            if (nested != null)
                parsed = serializer.DeserializeObject(nested);
            Dictionary<string, object> dictionary = parsed as Dictionary<string, object>;
            return dictionary ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> source, string key)
        {
            if (source == null)
                return null;
            object value;
            if (!source.TryGetValue(key, out value))
                return null;
            return value as Dictionary<string, object>;
        }

        private static string GetString(Dictionary<string, object> source, string key)
        {
            if (source == null)
                return string.Empty;
            object value;
            if (!source.TryGetValue(key, out value) || value == null)
                return string.Empty;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt(Dictionary<string, object> source, string key)
        {
            if (source == null)
                return 0;
            object value;
            if (!source.TryGetValue(key, out value) || value == null)
                return 0;
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static int GetFirstInt(Dictionary<string, object> source, string key)
        {
            if (source == null)
                return 0;
            object value;
            if (!source.TryGetValue(key, out value) || value == null)
                return 0;
            IEnumerable sequence = value as IEnumerable;
            if (sequence == null || value is string)
                return 0;
            foreach (object item in sequence)
            {
                try { return Convert.ToInt32(item, CultureInfo.InvariantCulture); }
                catch { return 0; }
            }
            return 0;
        }
    }

    internal sealed class JobApplyTimeoutException : TimeoutException
    {
        public JobApplyTimeoutException(string message)
            : base(message)
        {
        }
    }

    internal sealed class JobApplyResult
    {
        public List<string> Filled { get; private set; }
        public List<string> Review { get; private set; }
        public List<string> Skipped { get; private set; }
        public bool ResumeFieldFound { get; private set; }
        public string Error { get; private set; }

        public int FilledCount { get { return Filled.Count; } }
        public int ReviewCount { get { return Review.Count; } }
        public int SkippedCount { get { return Skipped.Count; } }
        public bool HasError { get { return !string.IsNullOrWhiteSpace(Error); } }

        private JobApplyResult()
        {
            Filled = new List<string>();
            Review = new List<string>();
            Skipped = new List<string>();
            Error = string.Empty;
        }

        public static JobApplyResult Parse(string raw, JavaScriptSerializer serializer)
        {
            JobApplyResult result = new JobApplyResult();
            try
            {
                object parsed = serializer.DeserializeObject(raw ?? "null");
                string nested = parsed as string;
                if (nested != null)
                    parsed = serializer.DeserializeObject(nested);
                Dictionary<string, object> values = parsed as Dictionary<string, object>;
                if (values == null)
                {
                    result.Error = "El portal no devolvió un resultado de autollenado válido.";
                    return result;
                }
                string portalError = ReadString(values, "error");
                result.Error = string.IsNullOrWhiteSpace(portalError)
                    ? string.Empty
                    : "El portal no pudo preparar algunos campos. Revisa el formulario manualmente.";
                result.ResumeFieldFound = ReadBoolean(values, "resumeFieldFound");
                result.Filled.AddRange(ReadLabels(values, "filled"));
                result.Review.AddRange(ReadLabels(values, "review"));
                result.Skipped.AddRange(ReadLabels(values, "skipped"));
            }
            catch (Exception ex)
            {
                result.Error = "No se pudo interpretar el resultado del portal. Revisa los campos manualmente.";
            }
            return result;
        }

        private static string ReadString(Dictionary<string, object> values, string key)
        {
            object value;
            return values.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : string.Empty;
        }

        private static bool ReadBoolean(Dictionary<string, object> values, string key)
        {
            object value;
            if (!values.TryGetValue(key, out value) || value == null)
                return false;
            try { return Convert.ToBoolean(value, CultureInfo.InvariantCulture); }
            catch { return false; }
        }

        private static IEnumerable<string> ReadLabels(Dictionary<string, object> values, string key)
        {
            object raw;
            if (!values.TryGetValue(key, out raw) || raw == null)
                yield break;
            IEnumerable items = raw as IEnumerable;
            if (items == null || raw is string)
                yield break;
            foreach (object item in items)
            {
                string label = null;
                Dictionary<string, object> descriptor = item as Dictionary<string, object>;
                if (descriptor != null)
                {
                    object labelValue;
                    if (descriptor.TryGetValue("label", out labelValue) && labelValue != null)
                        label = Convert.ToString(labelValue);
                    if (string.IsNullOrWhiteSpace(label) && descriptor.TryGetValue("field", out labelValue) && labelValue != null)
                        label = Convert.ToString(labelValue);
                }
                else if (item != null)
                    label = Convert.ToString(item);
                if (!string.IsNullOrWhiteSpace(label))
                    yield return label.Trim();
            }
        }
    }

    internal sealed class ResumeAttachResult
    {
        public bool Success { get; private set; }
        public bool TimedOut { get; private set; }
        public string Message { get; private set; }

        private ResumeAttachResult(bool success, bool timedOut, string message)
        {
            Success = success;
            TimedOut = timedOut;
            Message = message ?? string.Empty;
        }

        public static ResumeAttachResult Attached(string fileName)
        {
            return new ResumeAttachResult(true, false, "CV adjuntado: " + fileName);
        }

        public static ResumeAttachResult TimedOutResult(string message)
        {
            return new ResumeAttachResult(false, true, message);
        }

        public static ResumeAttachResult Failed(string message)
        {
            return new ResumeAttachResult(false, false, message);
        }

        public static ResumeAttachResult FromTimeout(string message)
        {
            return TimedOutResult(message);
        }
    }
}
