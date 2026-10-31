using System;
using System.Text.RegularExpressions;

namespace ChatGPTLite
{
    /// <summary>
    /// Builds job-application diagnostics without copying profile values, CV names,
    /// URLs, cookies or exception stack traces into the local log.
    /// </summary>
    internal static class JobApplyDiagnostics
    {
        private static readonly Regex Email = new Regex(
            @"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Url = new Regex(
            @"https?://[^\s""'<>]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex WindowsPath = new Regex(
            @"(?:[A-Z]:\\|\\\\)[^\r\n""']+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Secret = new Regex(
            @"(?i)\b(password|passwd|token|secret|cookie|set-cookie|authorization|api[-_ ]?key)\s*[:=]\s*(?:bearer\s+)?[^\s;]+",
            RegexOptions.Compiled);
        private static readonly Regex Bearer = new Regex(
            @"(?i)\bbearer\s+[A-Za-z0-9._~+\-/=]+",
            RegexOptions.Compiled);

        public static string Host(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return "unknown";
            return uri.Scheme + "://" + uri.Host + (uri.IsDefaultPort ? string.Empty : ":" + uri.Port.ToString());
        }

        public static string Label(string value)
        {
            string safe = value ?? string.Empty;
            safe = safe.Replace('\r', ' ').Replace('\n', ' ');
            safe = Regex.Replace(safe, @"[^A-Za-z0-9_.:-]+", "-");
            return Limit(safe, 80);
        }

        public static string Exception(Exception exception)
        {
            if (exception == null)
                return string.Empty;
            // Exception messages can contain page text, file names or form values.
            // Keep only the type; event/state fields remain useful for recovery.
            return Limit(exception.GetType().Name, 80);
        }

        public static string Message(string value)
        {
            return SafeText(value);
        }

        private static string SafeText(string value)
        {
            string safe = value ?? string.Empty;
            safe = Url.Replace(safe, "<url>");
            safe = Email.Replace(safe, "<email>");
            safe = WindowsPath.Replace(safe, "<path>");
            safe = Secret.Replace(safe, "$1=<redacted>");
            safe = Bearer.Replace(safe, "Bearer <redacted>");
            safe = safe.Replace('\r', ' ').Replace('\n', ' ');
            return Limit(safe, 200);
        }

        private static string Limit(string value, int length)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= length)
                return value ?? string.Empty;
            return value.Substring(0, Math.Max(0, length - 1)) + "…";
        }
    }
}
