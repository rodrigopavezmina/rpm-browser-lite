using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace ChatGPTLite
{
    [DataContract]
    internal sealed class AppState
    {
        [DataMember(Order = 1)]
        public AppSettings Settings { get; set; }

        [DataMember(Order = 2)]
        public List<WindowRecord> Windows { get; set; }

        [DataMember(Order = 3)]
        public List<RecentRecord> Recents { get; set; }

        [DataMember(Order = 4)]
        public List<string> KnownIdentifiers { get; set; }

        [DataMember(Order = 5)]
        public List<BookmarkRecord> Bookmarks { get; set; }

        [DataMember(Order = 6)]
        public List<DeletedWindowRecord> RecycleBin { get; set; }

        [DataMember(Order = 7)]
        public List<DomainProfile> DomainProfiles { get; set; }

        public static AppState CreateDefault()
        {
            AppState state = new AppState();
            state.Settings = AppSettings.CreateDefault();
            state.Windows = new List<WindowRecord>();
            state.Recents = new List<RecentRecord>();
            state.KnownIdentifiers = CreateDefaultIdentifiers();
            state.Bookmarks = new List<BookmarkRecord>();
            state.RecycleBin = new List<DeletedWindowRecord>();
            state.DomainProfiles = new List<DomainProfile>();
            return state;
        }

        public void RepairDefaults()
        {
            if (Settings == null)
                Settings = AppSettings.CreateDefault();
            Settings.RepairDefaults();
            if (Windows == null)
                Windows = new List<WindowRecord>();
            if (Recents == null)
                Recents = new List<RecentRecord>();
            foreach (RecentRecord recent in Recents)
            {
                if (recent != null && recent.VisitCount < 0)
                    recent.VisitCount = 0;
            }
            if (KnownIdentifiers == null)
                KnownIdentifiers = new List<string>();
            if (Bookmarks == null)
                Bookmarks = new List<BookmarkRecord>();
            foreach (BookmarkRecord bookmark in Bookmarks)
            {
                if (bookmark != null && bookmark.FolderPath == null)
                    bookmark.FolderPath = string.Empty;
            }
            if (RecycleBin == null)
                RecycleBin = new List<DeletedWindowRecord>();
            if (DomainProfiles == null)
                DomainProfiles = new List<DomainProfile>();
            DomainProfiles.RemoveAll(delegate(DomainProfile item)
            {
                return item == null || string.IsNullOrWhiteSpace(item.Domain);
            });
            RecycleBin.RemoveAll(delegate(DeletedWindowRecord item)
            {
                return item == null || item.Record == null || item.DeletedUtc < DateTime.UtcNow.AddDays(-30);
            });
            KnownIdentifiers.RemoveAll(IsPreviousDefaultIdentifier);
            foreach (string identifier in CreateDefaultIdentifiers())
            {
                if (!KnownIdentifiers.Any(delegate(string known) { return string.Equals(known, identifier, StringComparison.OrdinalIgnoreCase); }))
                    KnownIdentifiers.Add(identifier);
            }
            foreach (WindowRecord window in Windows)
            {
                if (window != null)
                    window.RepairPresentationDefaults();
            }
        }

        private static List<string> CreateDefaultIdentifiers()
        {
            List<string> identifiers = new List<string>();
            for (int number = 1; number <= 50; number++)
                identifiers.Add("TD-D" + number.ToString("00"));
            return identifiers;
        }

        private static bool IsPreviousDefaultIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return false;
            string value = identifier.Trim();
            for (int number = 1; number <= 50; number++)
            {
                if (string.Equals(value, "TD-" + number.ToString("000"), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }

    [DataContract]
    internal sealed class AppSettings
    {
        [DataMember(Order = 1)]
        public int SuspendAfterSeconds { get; set; }

        [DataMember(Order = 2)]
        public int HibernateAfterMinutes { get; set; }

        [DataMember(Order = 3)]
        public bool RestoreWindows { get; set; }

        [DataMember(Order = 4)]
        public int CacheSizeMb { get; set; }

        [DataMember(Order = 5)]
        public string UpdateManifestUrl { get; set; }

        [DataMember(Order = 6)]
        public bool MinimizeToTray { get; set; }

        [DataMember(Order = 7)]
        public bool DarkMode { get; set; }

        [DataMember(Order = 8)]
        public bool CapturePreviewOnHibernate { get; set; }

        [DataMember(Order = 9)]
        public bool? AutomaticUpdates { get; set; }

        [DataMember(Order = 10)]
        public bool? RestoreAllOnStart { get; set; }

        [DataMember(Order = 11)]
        public bool? RestoreHibernatedOnlyOnClick { get; set; }

        [DataMember(Order = 12)]
        public int QuickConsultCloseAfterMinutes { get; set; }

        [DataMember(Order = 13)]
        public string QuickConsultUrl { get; set; }

        [DataMember(Order = 14)]
        public bool? ShowStartDashboard { get; set; }

        [DataMember(Order = 15)]
        public int StartDashboardTimeoutSeconds { get; set; }

        // 0 disables the hibernation frame; 1-5 select the visual style.
        [DataMember(Order = 16)]
        public int HibernationIndicatorStyle { get; set; }

        public static AppSettings CreateDefault()
        {
            AppSettings settings = new AppSettings();
            settings.SuspendAfterSeconds = 30;
            settings.HibernateAfterMinutes = 10;
            settings.RestoreWindows = true;
            settings.CacheSizeMb = 512;
            settings.UpdateManifestUrl = string.Empty;
            settings.MinimizeToTray = true;
            settings.DarkMode = false;
            settings.CapturePreviewOnHibernate = true;
            settings.AutomaticUpdates = true;
            settings.RestoreAllOnStart = null;
            settings.RestoreHibernatedOnlyOnClick = true;
            settings.QuickConsultCloseAfterMinutes = 15;
            settings.QuickConsultUrl = "https://gemini.google.com/app";
            settings.ShowStartDashboard = true;
            settings.StartDashboardTimeoutSeconds = 10;
            settings.HibernationIndicatorStyle = 1;
            return settings;
        }

        public void RepairDefaults()
        {
            if (SuspendAfterSeconds < 0)
                SuspendAfterSeconds = 30;
            if (HibernateAfterMinutes < 0)
                HibernateAfterMinutes = 10;
            if (CacheSizeMb < 64)
                CacheSizeMb = 512;
            if (UpdateManifestUrl == null)
                UpdateManifestUrl = string.Empty;
            if (!AutomaticUpdates.HasValue)
                AutomaticUpdates = true;
            if (!RestoreHibernatedOnlyOnClick.HasValue)
                RestoreHibernatedOnlyOnClick = true;
            if (QuickConsultCloseAfterMinutes < 0 || QuickConsultCloseAfterMinutes > 240)
                QuickConsultCloseAfterMinutes = 15;
            if (string.IsNullOrWhiteSpace(QuickConsultUrl))
                QuickConsultUrl = "https://gemini.google.com/app";
            if (!ShowStartDashboard.HasValue)
                ShowStartDashboard = true;
            if (StartDashboardTimeoutSeconds < 5 || StartDashboardTimeoutSeconds > 60)
                StartDashboardTimeoutSeconds = 10;
            if (HibernationIndicatorStyle < 0 || HibernationIndicatorStyle > 5)
                HibernationIndicatorStyle = 1;
        }
    }

    [DataContract]
    internal sealed class WindowRecord
    {
        [DataMember(Order = 1)]
        public string Id { get; set; }

        [DataMember(Order = 2)]
        public string Url { get; set; }

        [DataMember(Order = 3)]
        public string Title { get; set; }

        [DataMember(Order = 4)]
        public int X { get; set; }

        [DataMember(Order = 5)]
        public int Y { get; set; }

        [DataMember(Order = 6)]
        public int Width { get; set; }

        [DataMember(Order = 7)]
        public int Height { get; set; }

        [DataMember(Order = 8)]
        public bool Maximized { get; set; }

        [DataMember(Order = 9)]
        public bool Hibernated { get; set; }

        [DataMember(Order = 10)]
        public DateTime LastActiveUtc { get; set; }

            // These fields describe the native window only. They do not modify web content.
        [DataMember(Order = 11)]
        public string CustomTitle { get; set; }

        [DataMember(Order = 12)]
        public string Identifier { get; set; }

        [DataMember(Order = 13)]
        public string IconGlyph { get; set; }

        [DataMember(Order = 14)]
        public int IconColorArgb { get; set; }

        [DataMember(Order = 15)]
        public string NoteText { get; set; }

        [DataMember(Order = 16)]
        public bool NotesVisible { get; set; }

        [DataMember(Order = 17)]
        public List<NoteImageRecord> NoteImages { get; set; }

        [DataMember(Order = 18)]
        public DateTime CreatedUtc { get; set; }

        [DataMember(Order = 19)]
        public bool CaffeineMode { get; set; }

        [DataMember(Order = 20)]
        public bool KeepHibernated { get; set; }

        [DataMember(Order = 21)]
        public DateTime HibernationPreviewUtc { get; set; }

        [DataMember(Order = 22)]
        public bool BookmarksVisible { get; set; }

        // Quick consultations are intentionally transient and are never written to the saved-window list.
        [DataMember(Order = 23)]
        public bool IsQuickConsult { get; set; }

        [DataMember(Order = 24)]
        public string Category { get; set; }

        [DataMember(Order = 25)]
        public bool? UseFavicon { get; set; }

        [DataMember(Order = 26)]
        public string FaviconPath { get; set; }

        [DataMember(Order = 27)]
        public string CustomIconPath { get; set; }

        [DataMember(Order = 28)]
        public bool TrayShortcut { get; set; }

        [DataMember(Order = 29)]
        public bool TraySingleInstance { get; set; }

        public static WindowRecord Create(string url)
        {
            WindowRecord record = new WindowRecord();
            record.Id = Guid.NewGuid().ToString("N");
            record.Url = string.IsNullOrWhiteSpace(url) ? AppController.HomeUrl : url;
            record.Title = "New browser window";
            record.Width = 1120;
            record.Height = 760;
            record.CreatedUtc = DateTime.UtcNow;
            record.LastActiveUtc = DateTime.UtcNow;
            record.IconGlyph = "✦";
            record.IconColorArgb = unchecked((int)0xFF4B73B8);
            record.NoteText = string.Empty;
            record.NoteImages = new List<NoteImageRecord>();
            record.Category = string.Empty;
            record.UseFavicon = true;
            record.FaviconPath = string.Empty;
            record.CustomIconPath = string.Empty;
            return record;
        }

        public void RepairPresentationDefaults()
        {
            if (CustomTitle == null)
                CustomTitle = string.Empty;
            if (Identifier == null)
                Identifier = string.Empty;
            if (string.IsNullOrWhiteSpace(IconGlyph))
                IconGlyph = "✦";
            if ((IconColorArgb & unchecked((int)0xFF000000)) == 0)
                IconColorArgb = unchecked((int)0xFF4B73B8);
            if (NoteText == null)
                NoteText = string.Empty;
            if (NoteImages == null)
                NoteImages = new List<NoteImageRecord>();
            if (Category == null)
                Category = string.Empty;
            if (!UseFavicon.HasValue)
                UseFavicon = true;
            if (FaviconPath == null)
                FaviconPath = string.Empty;
            if (CustomIconPath == null)
                CustomIconPath = string.Empty;
            if (CreatedUtc == DateTime.MinValue)
                CreatedUtc = LastActiveUtc == DateTime.MinValue ? DateTime.UtcNow : LastActiveUtc;
        }

        public string DisplayTitle(string pageTitle)
        {
            if (!string.IsNullOrWhiteSpace(CustomTitle))
                return CustomTitle.Trim();
            if (!string.IsNullOrWhiteSpace(pageTitle))
                return pageTitle.Trim();
            return "Browser window";
        }
    }

    [DataContract]
    internal sealed class DomainProfile
    {
        [DataMember(Order = 1)]
        public string Domain { get; set; }

        [DataMember(Order = 2)]
        public int IconColorArgb { get; set; }

        [DataMember(Order = 3)]
        public string IconGlyph { get; set; }
    }

    [DataContract]
    internal sealed class NoteImageRecord
    {
        [DataMember(Order = 1)]
        public string Id { get; set; }

        [DataMember(Order = 2)]
        public string FileName { get; set; }

        [DataMember(Order = 3)]
        public string OriginalFileName { get; set; }

        [DataMember(Order = 4)]
        public DateTime AddedUtc { get; set; }
    }

    [DataContract]
    internal sealed class RecentRecord
    {
        [DataMember(Order = 1)]
        public string Url { get; set; }

        [DataMember(Order = 2)]
        public string Title { get; set; }

        [DataMember(Order = 3)]
        public DateTime LastOpenedUtc { get; set; }

        [DataMember(Order = 4)]
        public int VisitCount { get; set; }
    }

    [DataContract]
    internal sealed class BookmarkRecord
    {
        [DataMember(Order = 1)]
        public string Url { get; set; }

        [DataMember(Order = 2)]
        public string Title { get; set; }

        [DataMember(Order = 3)]
        public DateTime CreatedUtc { get; set; }

        [DataMember(Order = 4)]
        public string FolderPath { get; set; }
    }

    [DataContract]
    internal sealed class DeletedWindowRecord
    {
        [DataMember(Order = 1)]
        public WindowRecord Record { get; set; }

        [DataMember(Order = 2)]
        public DateTime DeletedUtc { get; set; }
    }

    [DataContract]
    internal sealed class UpdateManifest
    {
        [DataMember(Name = "version", Order = 1)]
        public string Version { get; set; }

        [DataMember(Name = "downloadUrl", Order = 2)]
        public string DownloadUrl { get; set; }

        [DataMember(Name = "notes", Order = 3)]
        public string Notes { get; set; }

        [DataMember(Name = "sha256", Order = 4)]
        public string Sha256 { get; set; }
    }
}
