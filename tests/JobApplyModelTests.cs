using System;
using System.IO;
using System.Web.Script.Serialization;

namespace ChatGPTLite
{
    internal static class JobApplyModelTests
    {
        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public static int Main()
        {
            string directory = Path.Combine(Path.GetTempPath(), "rpm-job-apply-model-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                JobApplicationProfile profile = new JobApplicationProfile
                {
                    FirstName = "  Ana  ",
                    LastName = "Pérez",
                    Email = "ANA@example.com",
                    Experiences = new System.Collections.Generic.List<JobExperience>
                    {
                        new JobExperience { Company = "Acme", Title = "Engineer", StartDate = "2024-01" }
                    },
                    Education = null
                };
                profile.Repair();
                JobApplicationFieldMetadata migrated = profile.GetFieldMetadata("FirstName");
                Expect(migrated != null, "legacy field metadata was not created");
                Expect(migrated.NormalizedValue == "Ana", "normalized legacy value mismatch");
                Expect(migrated.OriginalEvidence == "  Ana  ", "original evidence was not preserved");
                Expect(migrated.Source == "legacy-profile", "legacy source mismatch");
                Expect(Math.Abs(migrated.Confidence - 0.5) < 0.0001, "legacy confidence mismatch");
                Expect(profile.GetFieldMetadata("Experiences[0].Company") != null, "nested experience metadata was not created");
                Expect(profile.MetadataSchemaVersion == JobApplicationProfile.CurrentMetadataSchemaVersion, "metadata schema was not repaired");

                profile.SetFieldMetadata("FirstName", new JobApplicationFieldMetadata
                {
                    NormalizedValue = "Ana",
                    Source = "approved-manual",
                    Confidence = 0.97,
                    ApprovedAtUtc = "2026-10-30T12:00:00Z",
                    Sensitivity = "personal",
                    OriginalEvidence = "Ana"
                });
                JobApplicationProfile approvedProfile = profile.Clone();
                profile.FirstName = "Beatriz";
                profile.Repair();
                JobApplicationFieldMetadata edited = profile.GetFieldMetadata("FirstName");
                Expect(edited.Source == "edited-profile", "profile edit did not invalidate approval");
                Expect(edited.ApprovedAtUtc == string.Empty, "profile edit retained approval date");
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                JobApplicationProfile roundTrip = serializer.Deserialize<JobApplicationProfile>(serializer.Serialize(approvedProfile));
                roundTrip.Repair();
                JobApplicationFieldMetadata approved = roundTrip.GetFieldMetadata("FirstName");
                Expect(approved != null && approved.Source == "approved-manual", "metadata source did not round-trip");
                Expect(Math.Abs(approved.Confidence - 0.97) < 0.0001, "metadata confidence did not round-trip");
                Expect(approved.ApprovedAtUtc == "2026-10-30T12:00:00Z", "approval date did not round-trip");

                JobApplicationProfile incomplete = new JobApplicationProfile();
                incomplete.Repair();
                Expect(incomplete.Experiences != null && incomplete.Education != null, "incomplete lists were not recovered");
                Expect(incomplete.FieldMetadata != null, "incomplete metadata dictionary was not recovered");
                Expect(incomplete.GetFieldMetadata("RequiresSponsorship") != null, "empty scalar metadata was not created");
                Expect(incomplete.FirstName == string.Empty && incomplete.ResumePath == string.Empty, "incomplete scalar values were not repaired");

                string diagnostic = JobApplyDiagnostics.Message(
                    "email=ana@example.com cv=C:\\Users\\Ana\\cv.pdf token=secret-value");
                Expect(diagnostic.IndexOf("ana@example.com", StringComparison.OrdinalIgnoreCase) < 0, "diagnostic leaked email");
                Expect(diagnostic.IndexOf("cv.pdf", StringComparison.OrdinalIgnoreCase) < 0, "diagnostic leaked CV path");
                Expect(diagnostic.IndexOf("secret-value", StringComparison.OrdinalIgnoreCase) < 0, "diagnostic leaked secret");
                Expect(JobApplyDiagnostics.Exception(new InvalidOperationException("sensitive page text")) == "InvalidOperationException", "diagnostic leaked exception details");

                string encryptedPath = Path.Combine(directory, "profile.dat");
                JobApplyStore.Save(encryptedPath, approvedProfile);
                JobApplicationProfile loaded = JobApplyStore.Load(encryptedPath);
                Expect(loaded.GetFieldMetadata("FirstName") != null, "DPAPI profile lost metadata");
                Expect(loaded.GetFieldMetadata("FirstName").Source == "approved-manual", "DPAPI profile changed metadata");

                string legacyPath = Path.Combine(directory, "legacy.dat");
                JobApplicationProfile legacy = new JobApplicationProfile { FirstName = "Legacy", LastName = "User" };
                JobApplyStore.Save(legacyPath, legacy);
                JobApplicationProfile migratedFromStore = JobApplyStore.Load(legacyPath);
                Expect(migratedFromStore.GetFieldMetadata("FirstName") != null, "store migration did not create metadata");

                string corruptPath = Path.Combine(directory, "corrupt.dat");
                File.WriteAllBytes(corruptPath, new byte[] { 1, 2, 3, 4, 5 });
                bool corruptionRaised = false;
                try { JobApplyStore.Load(corruptPath); }
                catch (JobApplyStoreException) { corruptionRaised = true; }
                Expect(corruptionRaised, "corrupt profile did not fail safely");
                Expect(File.Exists(corruptPath), "corrupt profile was deleted");
                Expect(Directory.GetFiles(directory, "corrupt.dat.corrupt-*.bak").Length == 1, "corrupt backup was not created");

                Console.WriteLine("job-apply-model: ok");
                return 0;
            }
            finally
            {
                try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
                catch { }
            }
        }
    }
}
