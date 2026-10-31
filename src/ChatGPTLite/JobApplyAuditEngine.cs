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
    internal static class JobApplyAuditEngine
    {
        private const string ScriptResourceName = "ChatGPTLite.JobApplyAuditScript.js";
        private static readonly object ScriptLock = new object();
        private static string _script;

        public static string Script
        {
            get
            {
                if (_script != null) return _script;
                lock (ScriptLock)
                {
                    if (_script == null) _script = ReadEmbeddedScript();
                }
                return _script;
            }
        }

        public static async Task<JobFormAuditResult> AuditAsync(
            CoreWebView2 browser,
            TimeSpan timeout)
        {
            if (browser == null) throw new ArgumentNullException("browser");
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException("timeout");

            DateTime deadline = DateTime.UtcNow.Add(timeout);
            await AwaitWithDeadline(
                browser.ExecuteScriptAsync(Script),
                deadline,
                "El portal tardó demasiado en preparar la auditoría.");

            string raw = await AwaitWithDeadline(
                browser.ExecuteScriptAsync(
                    "(function(){try{" +
                    "if(!window.RpmJobApplyAudit||typeof window.RpmJobApplyAudit.inspect!=='function')" +
                    "return JSON.stringify({schemaVersion:'1.0',fieldCount:0,fields:[],error:'audit-unavailable'});" +
                    "return JSON.stringify(window.RpmJobApplyAudit.inspect());" +
                    "}catch(error){return JSON.stringify({schemaVersion:'1.0',fieldCount:0,fields:[],error:'audit-failed'});}})()"),
                deadline,
                "El portal tardó demasiado en inspeccionar el formulario.");

            JavaScriptSerializer serializer = CreateSerializer();
            string json = ParseJsonString(raw, serializer);
            return JobFormAuditResult.Parse(json, serializer);
        }

        private static string ParseJsonString(string raw, JavaScriptSerializer serializer)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException("La auditoría no devolvió datos.");
            object parsed = serializer.DeserializeObject(raw);
            string nested = parsed as string;
            return nested ?? raw;
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = 16 * 1024 * 1024;
            serializer.RecursionLimit = 100;
            return serializer;
        }

        private static async Task<string> AwaitWithDeadline(Task<string> task, DateTime deadline, string message)
        {
            TimeSpan remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) throw new JobApplyTimeoutException(message);
            Task completed = await Task.WhenAny(task, Task.Delay(remaining));
            if (completed != task)
            {
                ObserveLateFailure(task);
                throw new JobApplyTimeoutException(message);
            }
            return await task;
        }

        private static void ObserveLateFailure(Task task)
        {
            if (task == null)
                return;
            task.ContinueWith(
                delegate(Task faulted) { Exception ignored = faulted.Exception; },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        private static string ReadEmbeddedScript()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (System.IO.Stream stream = assembly.GetManifestResourceStream(ScriptResourceName))
            {
                if (stream == null) throw new InvalidOperationException("No se encontró el auditor de formularios integrado.");
                using (System.IO.StreamReader reader = new System.IO.StreamReader(stream, Encoding.UTF8, true))
                    return reader.ReadToEnd();
            }
        }
    }

    internal sealed class JobFormAuditResult
    {
        private JobFormAuditResult()
        {
            Json = string.Empty;
            Error = string.Empty;
        }

        public string Json { get; private set; }
        public int FieldCount { get; private set; }
        public int RequiredCount { get; private set; }
        public int VisibleCount { get; private set; }
        public string Error { get; private set; }
        public bool HasError { get { return !string.IsNullOrWhiteSpace(Error); } }

        public static JobFormAuditResult Parse(string json, JavaScriptSerializer serializer)
        {
            JobFormAuditResult result = new JobFormAuditResult();
            try
            {
                Dictionary<string, object> root = serializer.DeserializeObject(json ?? string.Empty) as Dictionary<string, object>;
                if (root == null) throw new InvalidDataException("Audit root is not an object.");
                result.Json = json;
                result.Error = ReadString(root, "error");
                object fieldsValue;
                if (!root.TryGetValue("fields", out fieldsValue) || !(fieldsValue is IEnumerable))
                    throw new InvalidDataException("Audit fields are missing.");

                foreach (object item in (IEnumerable)fieldsValue)
                {
                    Dictionary<string, object> field = item as Dictionary<string, object>;
                    if (field == null) continue;
                    result.FieldCount++;
                    if (ReadBoolean(field, "required")) result.RequiredCount++;
                    Dictionary<string, object> visibility = GetDictionary(field, "visibility");
                    if (ReadBoolean(visibility, "visible")) result.VisibleCount++;
                }
                return result;
            }
            catch
            {
                result.Json = string.Empty;
                result.Error = "No se pudo interpretar el inventario del formulario.";
                return result;
            }
        }

        private static string ReadString(Dictionary<string, object> source, string key)
        {
            object value;
            return source != null && source.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static bool ReadBoolean(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value) || value == null) return false;
            try { return Convert.ToBoolean(value, CultureInfo.InvariantCulture); }
            catch { return false; }
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> source, string key)
        {
            object value;
            return source != null && source.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }
    }
}
