using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Mail;
using System.Runtime.Serialization;
using System.Text;

namespace ChatGPTLite
{
    [DataContract]
    internal sealed class JobApplicationFieldMetadata
    {
        // ApprovedAtUtc is an ISO-8601 UTC string. Keeping it textual preserves
        // compatibility with JavaScriptSerializer and makes absent dates safe.
        [DataMember(Order = 1)] public string NormalizedValue { get; set; }
        [DataMember(Order = 2)] public string Source { get; set; }
        [DataMember(Order = 3)] public double Confidence { get; set; }
        [DataMember(Order = 4)] public string ApprovedAtUtc { get; set; }
        [DataMember(Order = 5)] public string Sensitivity { get; set; }
        [DataMember(Order = 6)] public string OriginalEvidence { get; set; }

        public void Repair()
        {
            NormalizedValue = ValueOrEmpty(NormalizedValue);
            Source = ValueOrEmpty(Source);
            ApprovedAtUtc = ValueOrEmpty(ApprovedAtUtc);
            Sensitivity = ValueOrEmpty(Sensitivity).ToLowerInvariant();
            OriginalEvidence = ValueOrEmpty(OriginalEvidence);
            if (double.IsNaN(Confidence) || double.IsInfinity(Confidence)) Confidence = 0;
            if (Confidence < 0) Confidence = 0;
            if (Confidence > 1) Confidence = 1;
            if (Source.Length == 0) Source = "unknown";
            if (Sensitivity.Length == 0) Sensitivity = "unknown";
        }

        public JobApplicationFieldMetadata Clone()
        {
            Repair();
            return new JobApplicationFieldMetadata
            {
                NormalizedValue = NormalizedValue,
                Source = Source,
                Confidence = Confidence,
                ApprovedAtUtc = ApprovedAtUtc,
                Sensitivity = Sensitivity,
                OriginalEvidence = OriginalEvidence
            };
        }

        private static string ValueOrEmpty(string value)
        {
            return value ?? string.Empty;
        }
    }

    [DataContract]
    internal sealed class JobApplicationProfile
    {
        internal const int CurrentMetadataSchemaVersion = 2;

        // Legacy scalar properties remain the source of truth for the UI and
        // page matcher. FieldMetadata is additive and lazily migrated by Repair.

        [DataMember(Order = 1)] public string FirstName { get; set; }
        [DataMember(Order = 2)] public string MiddleName { get; set; }
        [DataMember(Order = 3)] public string LastName { get; set; }
        [DataMember(Order = 4)] public string PreferredName { get; set; }
        [DataMember(Order = 5)] public string Email { get; set; }
        [DataMember(Order = 6)] public string Phone { get; set; }
        [DataMember(Order = 7)] public string AddressLine1 { get; set; }
        [DataMember(Order = 8)] public string AddressLine2 { get; set; }
        [DataMember(Order = 9)] public string City { get; set; }
        [DataMember(Order = 10)] public string Region { get; set; }
        [DataMember(Order = 11)] public string PostalCode { get; set; }
        [DataMember(Order = 12)] public string Country { get; set; }
        [DataMember(Order = 13)] public string LinkedInUrl { get; set; }
        [DataMember(Order = 14)] public string PortfolioUrl { get; set; }
        [DataMember(Order = 15)] public string GitHubUrl { get; set; }
        [DataMember(Order = 16)] public string CurrentTitle { get; set; }
        [DataMember(Order = 17)] public int YearsExperience { get; set; }
        [DataMember(Order = 18)] public string WorkAuthorization { get; set; }
        [DataMember(Order = 19)] public bool RequiresSponsorship { get; set; }
        [DataMember(Order = 20)] public bool WillingToRelocate { get; set; }
        [DataMember(Order = 21)] public string AvailableStartDate { get; set; }
        [DataMember(Order = 22)] public string SalaryExpectation { get; set; }
        [DataMember(Order = 23)] public string Skills { get; set; }
        [DataMember(Order = 24)] public string ResumePath { get; set; }
        [DataMember(Order = 25)] public bool OverwriteExisting { get; set; }
        [DataMember(Order = 26)] public List<JobExperience> Experiences { get; set; }
        [DataMember(Order = 27)] public List<JobEducation> Education { get; set; }
        [DataMember(Order = 28)] public string DesiredRole { get; set; }
        [DataMember(Order = 29)] public string WorkMode { get; set; }
        [DataMember(Order = 30)] public string EmploymentType { get; set; }
        [DataMember(Order = 31)] public Dictionary<string, JobApplicationFieldMetadata> FieldMetadata { get; set; }
        [DataMember(Order = 32)] public int MetadataSchemaVersion { get; set; }

        public static JobApplicationProfile CreateDefault()
        {
            JobApplicationProfile profile = new JobApplicationProfile();
            profile.Country = "Chile";
            profile.WorkMode = "Sin preferencia";
            profile.EmploymentType = "Tiempo completo";
            profile.Experiences = new List<JobExperience>();
            profile.Education = new List<JobEducation>();
            profile.Repair();
            return profile;
        }

        public void Repair()
        {
            FirstName = ValueOrEmpty(FirstName);
            MiddleName = ValueOrEmpty(MiddleName);
            LastName = ValueOrEmpty(LastName);
            PreferredName = ValueOrEmpty(PreferredName);
            Email = ValueOrEmpty(Email);
            Phone = ValueOrEmpty(Phone);
            AddressLine1 = ValueOrEmpty(AddressLine1);
            AddressLine2 = ValueOrEmpty(AddressLine2);
            City = ValueOrEmpty(City);
            Region = ValueOrEmpty(Region);
            PostalCode = ValueOrEmpty(PostalCode);
            Country = ValueOrEmpty(Country);
            LinkedInUrl = ValueOrEmpty(LinkedInUrl);
            PortfolioUrl = ValueOrEmpty(PortfolioUrl);
            GitHubUrl = ValueOrEmpty(GitHubUrl);
            CurrentTitle = ValueOrEmpty(CurrentTitle);
            WorkAuthorization = ValueOrEmpty(WorkAuthorization);
            AvailableStartDate = ValueOrEmpty(AvailableStartDate);
            SalaryExpectation = ValueOrEmpty(SalaryExpectation);
            Skills = ValueOrEmpty(Skills);
            ResumePath = ValueOrEmpty(ResumePath);
            DesiredRole = ValueOrEmpty(DesiredRole);
            WorkMode = ValueOrEmpty(WorkMode);
            EmploymentType = ValueOrEmpty(EmploymentType);
            if (YearsExperience < 0) YearsExperience = 0;
            if (YearsExperience > 80) YearsExperience = 80;
            if (Experiences == null) Experiences = new List<JobExperience>();
            if (Education == null) Education = new List<JobEducation>();

            foreach (JobExperience experience in Experiences)
            {
                if (experience != null) experience.Repair();
            }
            foreach (JobEducation education in Education)
            {
                if (education != null) education.Repair();
            }

            RepairFieldMetadata();
        }

        private void RepairFieldMetadata()
        {
            if (FieldMetadata == null)
                FieldMetadata = new Dictionary<string, JobApplicationFieldMetadata>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, JobApplicationFieldMetadata> item in FieldMetadata)
            {
                if (item.Value != null) item.Value.Repair();
            }

            EnsureFieldMetadata("FirstName", FirstName, NormalizeWords(FirstName), "personal");
            EnsureFieldMetadata("MiddleName", MiddleName, NormalizeWords(MiddleName), "personal");
            EnsureFieldMetadata("LastName", LastName, NormalizeWords(LastName), "personal");
            EnsureFieldMetadata("PreferredName", PreferredName, NormalizeWords(PreferredName), "personal");
            EnsureFieldMetadata("Email", Email, Email.Trim().ToLowerInvariant(), "sensitive");
            EnsureFieldMetadata("Phone", Phone, NormalizeWords(Phone), "sensitive");
            EnsureFieldMetadata("AddressLine1", AddressLine1, NormalizeWords(AddressLine1), "high");
            EnsureFieldMetadata("AddressLine2", AddressLine2, NormalizeWords(AddressLine2), "high");
            EnsureFieldMetadata("City", City, NormalizeWords(City), "personal");
            EnsureFieldMetadata("Region", Region, NormalizeWords(Region), "personal");
            EnsureFieldMetadata("PostalCode", PostalCode, NormalizeWords(PostalCode), "sensitive");
            EnsureFieldMetadata("Country", Country, NormalizeWords(Country), "personal");
            EnsureFieldMetadata("LinkedInUrl", LinkedInUrl, NormalizeUrl(LinkedInUrl), "sensitive");
            EnsureFieldMetadata("PortfolioUrl", PortfolioUrl, NormalizeUrl(PortfolioUrl), "sensitive");
            EnsureFieldMetadata("GitHubUrl", GitHubUrl, NormalizeUrl(GitHubUrl), "sensitive");
            EnsureFieldMetadata("CurrentTitle", CurrentTitle, NormalizeWords(CurrentTitle), "professional");
            EnsureFieldMetadata("DesiredRole", DesiredRole, NormalizeWords(DesiredRole), "professional");
            if (YearsExperience > 0)
                EnsureFieldMetadata("YearsExperience", YearsExperience.ToString(CultureInfo.InvariantCulture), YearsExperience.ToString(CultureInfo.InvariantCulture), "professional");
            EnsureFieldMetadata("WorkAuthorization", WorkAuthorization, NormalizeWords(WorkAuthorization), "sensitive");
            EnsureFieldMetadata("AvailableStartDate", AvailableStartDate, NormalizeDate(AvailableStartDate), "personal");
            EnsureFieldMetadata("SalaryExpectation", SalaryExpectation, NormalizeWords(SalaryExpectation), "sensitive");
            EnsureFieldMetadata("Skills", Skills, NormalizeMultiline(Skills), "professional");
            EnsureFieldMetadata("ResumePath", ResumePath, ResumePath.Trim().Trim('"'), "high");
            EnsureFieldMetadata("WorkMode", WorkMode, NormalizeWords(WorkMode), "personal");
            EnsureFieldMetadata("EmploymentType", EmploymentType, NormalizeWords(EmploymentType), "professional");
            if (RequiresSponsorship)
                EnsureFieldMetadata("RequiresSponsorship", "true", "true", "sensitive");
            if (WillingToRelocate)
                EnsureFieldMetadata("WillingToRelocate", "true", "true", "personal");
            if (OverwriteExisting)
                EnsureFieldMetadata("OverwriteExisting", "true", "true", "normal");

            for (int index = 0; index < Experiences.Count; index++)
            {
                JobExperience experience = Experiences[index];
                if (experience == null) continue;
                string prefix = "Experiences[" + index.ToString(CultureInfo.InvariantCulture) + "].";
                EnsureFieldMetadata(prefix + "Company", experience.Company, NormalizeWords(experience.Company), "professional");
                EnsureFieldMetadata(prefix + "Title", experience.Title, NormalizeWords(experience.Title), "professional");
                EnsureFieldMetadata(prefix + "Location", experience.Location, NormalizeWords(experience.Location), "personal");
                EnsureFieldMetadata(prefix + "StartDate", experience.StartDate, NormalizeMonth(experience.StartDate), "professional");
                EnsureFieldMetadata(prefix + "EndDate", experience.EndDate, NormalizeMonth(experience.EndDate), "professional");
                EnsureFieldMetadata(prefix + "Description", experience.Description, NormalizeMultiline(experience.Description), "professional");
                if (experience.IsCurrent)
                    EnsureFieldMetadata(prefix + "IsCurrent", "true", "true", "professional");
            }
            for (int index = 0; index < Education.Count; index++)
            {
                JobEducation education = Education[index];
                if (education == null) continue;
                string prefix = "Education[" + index.ToString(CultureInfo.InvariantCulture) + "].";
                EnsureFieldMetadata(prefix + "Institution", education.Institution, NormalizeWords(education.Institution), "professional");
                EnsureFieldMetadata(prefix + "Degree", education.Degree, NormalizeWords(education.Degree), "professional");
                EnsureFieldMetadata(prefix + "FieldOfStudy", education.FieldOfStudy, NormalizeWords(education.FieldOfStudy), "professional");
                EnsureFieldMetadata(prefix + "StartDate", education.StartDate, NormalizeMonth(education.StartDate), "professional");
                EnsureFieldMetadata(prefix + "EndDate", education.EndDate, NormalizeMonth(education.EndDate), "professional");
            }

            MetadataSchemaVersion = CurrentMetadataSchemaVersion;
        }

        public JobApplicationFieldMetadata GetFieldMetadata(string fieldName)
        {
            Repair();
            if (string.IsNullOrWhiteSpace(fieldName)) return null;
            JobApplicationFieldMetadata metadata;
            return FieldMetadata.TryGetValue(fieldName.Trim(), out metadata) && metadata != null
                ? metadata.Clone()
                : null;
        }

        public void SetFieldMetadata(string fieldName, JobApplicationFieldMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("Se requiere el nombre del campo.", "fieldName");
            if (metadata == null)
                throw new ArgumentNullException("metadata");
            if (FieldMetadata == null)
                FieldMetadata = new Dictionary<string, JobApplicationFieldMetadata>(StringComparer.OrdinalIgnoreCase);
            metadata.Repair();
            FieldMetadata[fieldName.Trim()] = metadata.Clone();
            MetadataSchemaVersion = CurrentMetadataSchemaVersion;
        }

        private void EnsureFieldMetadata(string key, string original, string normalized, string sensitivity)
        {
            original = ValueOrEmpty(original);
            normalized = ValueOrEmpty(normalized);
            JobApplicationFieldMetadata metadata;
            if (!FieldMetadata.TryGetValue(key, out metadata) || metadata == null)
            {
                if (original.Length == 0) return;
                FieldMetadata[key] = new JobApplicationFieldMetadata
                {
                    NormalizedValue = normalized,
                    Source = "legacy-profile",
                    Confidence = 0.5,
                    ApprovedAtUtc = string.Empty,
                    Sensitivity = sensitivity,
                    OriginalEvidence = original
                };
                return;
            }

            metadata.Repair();
            if (metadata.Source != "legacy-profile" && metadata.Source != "unknown" &&
                !string.Equals(metadata.NormalizedValue, normalized, StringComparison.Ordinal))
            {
                // A user edit invalidates a previous approval without deleting
                // the profile value or the provenance record.
                metadata.Source = "edited-profile";
                metadata.Confidence = Math.Min(metadata.Confidence, 0.5);
                metadata.ApprovedAtUtc = string.Empty;
                metadata.OriginalEvidence = original;
                metadata.NormalizedValue = normalized;
            }
            if (metadata.Source == "legacy-profile" || metadata.Source == "unknown")
            {
                metadata.NormalizedValue = normalized;
                metadata.OriginalEvidence = original;
                metadata.Source = "legacy-profile";
            }
            if (metadata.Sensitivity == "unknown") metadata.Sensitivity = sensitivity;
        }

        public void Normalize()
        {
            Repair();
            FirstName = NormalizeWords(FirstName);
            MiddleName = NormalizeWords(MiddleName);
            LastName = NormalizeWords(LastName);
            PreferredName = NormalizeWords(PreferredName);
            Email = Email.Trim().ToLowerInvariant();
            Phone = NormalizeWords(Phone);
            AddressLine1 = NormalizeWords(AddressLine1);
            AddressLine2 = NormalizeWords(AddressLine2);
            City = NormalizeWords(City);
            Region = NormalizeWords(Region);
            PostalCode = NormalizeWords(PostalCode);
            Country = NormalizeWords(Country);
            LinkedInUrl = NormalizeUrl(LinkedInUrl);
            PortfolioUrl = NormalizeUrl(PortfolioUrl);
            GitHubUrl = NormalizeUrl(GitHubUrl);
            CurrentTitle = NormalizeWords(CurrentTitle);
            WorkAuthorization = NormalizeWords(WorkAuthorization);
            AvailableStartDate = NormalizeDate(AvailableStartDate);
            SalaryExpectation = NormalizeWords(SalaryExpectation);
            Skills = NormalizeMultiline(Skills);
            ResumePath = ResumePath.Trim().Trim('"');
            DesiredRole = NormalizeWords(DesiredRole);
            WorkMode = NormalizeWords(WorkMode);
            EmploymentType = NormalizeWords(EmploymentType);

            List<JobExperience> normalizedExperiences = new List<JobExperience>();
            foreach (JobExperience experience in Experiences)
            {
                if (experience == null) continue;
                experience.Normalize();
                if (!experience.IsEmpty()) normalizedExperiences.Add(experience);
            }
            Experiences = normalizedExperiences;

            List<JobEducation> normalizedEducation = new List<JobEducation>();
            foreach (JobEducation education in Education)
            {
                if (education == null) continue;
                education.Normalize();
                if (!education.IsEmpty()) normalizedEducation.Add(education);
            }
            Education = normalizedEducation;
        }

        public JobApplicationProfile Clone()
        {
            Repair();
            JobApplicationProfile copy = new JobApplicationProfile();
            copy.FirstName = FirstName;
            copy.MiddleName = MiddleName;
            copy.LastName = LastName;
            copy.PreferredName = PreferredName;
            copy.Email = Email;
            copy.Phone = Phone;
            copy.AddressLine1 = AddressLine1;
            copy.AddressLine2 = AddressLine2;
            copy.City = City;
            copy.Region = Region;
            copy.PostalCode = PostalCode;
            copy.Country = Country;
            copy.LinkedInUrl = LinkedInUrl;
            copy.PortfolioUrl = PortfolioUrl;
            copy.GitHubUrl = GitHubUrl;
            copy.CurrentTitle = CurrentTitle;
            copy.YearsExperience = YearsExperience;
            copy.WorkAuthorization = WorkAuthorization;
            copy.RequiresSponsorship = RequiresSponsorship;
            copy.WillingToRelocate = WillingToRelocate;
            copy.AvailableStartDate = AvailableStartDate;
            copy.SalaryExpectation = SalaryExpectation;
            copy.Skills = Skills;
            copy.ResumePath = ResumePath;
            copy.OverwriteExisting = OverwriteExisting;
            copy.DesiredRole = DesiredRole;
            copy.WorkMode = WorkMode;
            copy.EmploymentType = EmploymentType;
            copy.MetadataSchemaVersion = MetadataSchemaVersion;
            copy.FieldMetadata = new Dictionary<string, JobApplicationFieldMetadata>(StringComparer.OrdinalIgnoreCase);
            if (FieldMetadata != null)
            {
                foreach (KeyValuePair<string, JobApplicationFieldMetadata> item in FieldMetadata)
                {
                    if (string.IsNullOrWhiteSpace(item.Key) || item.Value == null) continue;
                    copy.FieldMetadata[item.Key] = item.Value.Clone();
                }
            }
            copy.Experiences = new List<JobExperience>();
            foreach (JobExperience experience in Experiences)
            {
                if (experience != null) copy.Experiences.Add(experience.Clone());
            }
            copy.Education = new List<JobEducation>();
            foreach (JobEducation education in Education)
            {
                if (education != null) copy.Education.Add(education.Clone());
            }
            return copy;
        }

        public List<string> Validate()
        {
            JobApplicationProfile candidate = Clone();
            candidate.Normalize();
            return candidate.ValidateNormalized();
        }

        public bool TryValidate(out string message)
        {
            List<string> errors = Validate();
            message = errors.Count == 0 ? string.Empty : string.Join(Environment.NewLine, errors.ToArray());
            return errors.Count == 0;
        }

        private List<string> ValidateNormalized()
        {
            List<string> errors = new List<string>();
            if (FirstName.Length == 0) errors.Add("Ingresa tu nombre.");
            if (LastName.Length == 0) errors.Add("Ingresa tu apellido.");
            if (Email.Length == 0)
                errors.Add("Ingresa tu correo electrónico.");
            else if (!IsValidEmail(Email))
                errors.Add("El correo electrónico no tiene un formato válido.");
            if (Phone.Length == 0) errors.Add("Ingresa tu teléfono, incluyendo el código de país.");
            if (Country.Length == 0) errors.Add("Ingresa tu país de residencia.");
            if (YearsExperience < 0 || YearsExperience > 80)
                errors.Add("Los años de experiencia deben estar entre 0 y 80.");
            ValidateUrl(LinkedInUrl, "LinkedIn", errors);
            ValidateUrl(PortfolioUrl, "portafolio", errors);
            ValidateUrl(GitHubUrl, "GitHub", errors);
            if (AvailableStartDate.Length > 0 && !IsExactDate(AvailableStartDate))
                errors.Add("La fecha de disponibilidad debe usar el formato AAAA-MM-DD.");
            if (ResumePath.Length == 0)
                errors.Add("Selecciona el archivo de tu CV.");
            else if (!File.Exists(ResumePath))
                errors.Add("No se encuentra el archivo de CV indicado.");

            int index;
            for (index = 0; index < Experiences.Count; index++)
                Experiences[index].Validate(index + 1, errors);
            for (index = 0; index < Education.Count; index++)
                Education[index].Validate(index + 1, errors);
            return errors;
        }

        internal static string NormalizeMonth(string value)
        {
            string text = ValueOrEmpty(value).Trim();
            if (text.Length == 0) return string.Empty;

            DateTime parsed;
            string[] formats =
            {
                "yyyy-MM", "yyyy/MM", "MM/yyyy", "M/yyyy", "MM-yyyy", "M-yyyy",
                "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "yyyy.MM", "MM.yyyy",
                "yyyyMMdd", "MMM yyyy", "MMMM yyyy"
            };
            if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out parsed))
                return parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            if (DateTime.TryParse(text, new CultureInfo("es-CL"), DateTimeStyles.AllowWhiteSpaces, out parsed))
                return parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            if (DateTime.TryParse(text, new CultureInfo("pt-BR"), DateTimeStyles.AllowWhiteSpaces, out parsed))
                return parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
                return parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            return text;
        }

        internal static bool IsExactMonth(string value)
        {
            DateTime parsed;
            return DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out parsed);
        }

        internal static bool TryGetMonth(string value, out DateTime date)
        {
            return DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date);
        }

        internal static string NormalizeWords(string value)
        {
            string text = ValueOrEmpty(value).Trim();
            if (text.Length == 0) return string.Empty;
            StringBuilder result = new StringBuilder(text.Length);
            bool previousWasWhiteSpace = false;
            foreach (char character in text)
            {
                if (char.IsWhiteSpace(character))
                {
                    if (!previousWasWhiteSpace) result.Append(' ');
                    previousWasWhiteSpace = true;
                }
                else
                {
                    result.Append(character);
                    previousWasWhiteSpace = false;
                }
            }
            return result.ToString();
        }

        private static string NormalizeDate(string value)
        {
            string text = ValueOrEmpty(value).Trim();
            if (text.Length == 0) return string.Empty;
            DateTime parsed;
            string[] formats =
            {
                "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "yyyy/MM/dd",
                "d-M-yyyy", "d/M/yyyy", "yyyy.MM.dd", "dd.MM.yyyy", "yyyyMMdd"
            };
            if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out parsed))
                return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (DateTime.TryParse(text, new CultureInfo("es-CL"), DateTimeStyles.AllowWhiteSpaces, out parsed))
                return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (DateTime.TryParse(text, new CultureInfo("pt-BR"), DateTimeStyles.AllowWhiteSpaces, out parsed))
                return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
                return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return text;
        }

        private static bool IsExactDate(string value)
        {
            DateTime parsed;
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out parsed);
        }

        private static string NormalizeUrl(string value)
        {
            string text = ValueOrEmpty(value).Trim();
            if (text.Length == 0) return string.Empty;
            if (text.IndexOf("://", StringComparison.Ordinal) < 0) text = "https://" + text;
            return text;
        }

        private static string NormalizeMultiline(string value)
        {
            string text = ValueOrEmpty(value).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (text.Length == 0) return string.Empty;
            string[] lines = text.Split(new char[] { '\n' });
            StringBuilder result = new StringBuilder();
            foreach (string line in lines)
            {
                string normalized = NormalizeWords(line);
                if (normalized.Length == 0) continue;
                if (result.Length > 0) result.Append(Environment.NewLine);
                result.Append(normalized);
            }
            return result.ToString();
        }

        private static bool IsValidEmail(string value)
        {
            try
            {
                MailAddress address = new MailAddress(value);
                return string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void ValidateUrl(string value, string label, List<string> errors)
        {
            if (value.Length == 0) return;
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                errors.Add("El enlace de " + label + " no es una dirección web válida.");
        }

        private static string ValueOrEmpty(string value)
        {
            return value ?? string.Empty;
        }
    }

    [DataContract]
    internal sealed class JobExperience
    {
        [DataMember(Order = 1)] public string Company { get; set; }
        [DataMember(Order = 2)] public string Title { get; set; }
        [DataMember(Order = 3)] public string Location { get; set; }
        [DataMember(Order = 4)] public string StartDate { get; set; }
        [DataMember(Order = 5)] public string EndDate { get; set; }
        [DataMember(Order = 6)] public bool IsCurrent { get; set; }
        [DataMember(Order = 7)] public string Description { get; set; }

        public void Repair()
        {
            Company = Company ?? string.Empty;
            Title = Title ?? string.Empty;
            Location = Location ?? string.Empty;
            StartDate = StartDate ?? string.Empty;
            EndDate = EndDate ?? string.Empty;
            Description = Description ?? string.Empty;
        }

        public void Normalize()
        {
            Repair();
            Company = JobApplicationProfile.NormalizeWords(Company);
            Title = JobApplicationProfile.NormalizeWords(Title);
            Location = JobApplicationProfile.NormalizeWords(Location);
            StartDate = JobApplicationProfile.NormalizeMonth(StartDate);
            EndDate = IsCurrent ? string.Empty : JobApplicationProfile.NormalizeMonth(EndDate);
            Description = Description.Trim();
        }

        public JobExperience Clone()
        {
            Repair();
            JobExperience copy = new JobExperience();
            copy.Company = Company;
            copy.Title = Title;
            copy.Location = Location;
            copy.StartDate = StartDate;
            copy.EndDate = EndDate;
            copy.IsCurrent = IsCurrent;
            copy.Description = Description;
            return copy;
        }

        internal bool IsEmpty()
        {
            Repair();
            return Company.Trim().Length == 0 && Title.Trim().Length == 0 &&
                Location.Trim().Length == 0 && StartDate.Trim().Length == 0 &&
                EndDate.Trim().Length == 0 && Description.Trim().Length == 0 && !IsCurrent;
        }

        internal void Validate(int number, List<string> errors)
        {
            string prefix = "Experiencia " + number + ": ";
            if (Company.Length == 0) errors.Add(prefix + "ingresa la empresa.");
            if (Title.Length == 0) errors.Add(prefix + "ingresa el cargo.");
            if (StartDate.Length == 0)
                errors.Add(prefix + "ingresa la fecha de inicio (AAAA-MM).");
            else if (!JobApplicationProfile.IsExactMonth(StartDate))
                errors.Add(prefix + "la fecha de inicio debe usar AAAA-MM.");
            if (!IsCurrent)
            {
                if (EndDate.Length == 0)
                    errors.Add(prefix + "ingresa la fecha de término o marca Actual.");
                else if (!JobApplicationProfile.IsExactMonth(EndDate))
                    errors.Add(prefix + "la fecha de término debe usar AAAA-MM.");
            }

            DateTime start;
            DateTime end;
            if (!IsCurrent && JobApplicationProfile.TryGetMonth(StartDate, out start) &&
                JobApplicationProfile.TryGetMonth(EndDate, out end) && end < start)
                errors.Add(prefix + "la fecha de término no puede ser anterior al inicio.");
        }
    }

    [DataContract]
    internal sealed class JobEducation
    {
        [DataMember(Order = 1)] public string Institution { get; set; }
        [DataMember(Order = 2)] public string Degree { get; set; }
        [DataMember(Order = 3)] public string FieldOfStudy { get; set; }
        [DataMember(Order = 4)] public string StartDate { get; set; }
        [DataMember(Order = 5)] public string EndDate { get; set; }

        public void Repair()
        {
            Institution = Institution ?? string.Empty;
            Degree = Degree ?? string.Empty;
            FieldOfStudy = FieldOfStudy ?? string.Empty;
            StartDate = StartDate ?? string.Empty;
            EndDate = EndDate ?? string.Empty;
        }

        public void Normalize()
        {
            Repair();
            Institution = JobApplicationProfile.NormalizeWords(Institution);
            Degree = JobApplicationProfile.NormalizeWords(Degree);
            FieldOfStudy = JobApplicationProfile.NormalizeWords(FieldOfStudy);
            StartDate = JobApplicationProfile.NormalizeMonth(StartDate);
            EndDate = JobApplicationProfile.NormalizeMonth(EndDate);
        }

        public JobEducation Clone()
        {
            Repair();
            JobEducation copy = new JobEducation();
            copy.Institution = Institution;
            copy.Degree = Degree;
            copy.FieldOfStudy = FieldOfStudy;
            copy.StartDate = StartDate;
            copy.EndDate = EndDate;
            return copy;
        }

        internal bool IsEmpty()
        {
            Repair();
            return Institution.Trim().Length == 0 && Degree.Trim().Length == 0 &&
                FieldOfStudy.Trim().Length == 0 && StartDate.Trim().Length == 0 &&
                EndDate.Trim().Length == 0;
        }

        internal void Validate(int number, List<string> errors)
        {
            string prefix = "Educación " + number + ": ";
            if (Institution.Length == 0) errors.Add(prefix + "ingresa la institución.");
            if (Degree.Length == 0) errors.Add(prefix + "ingresa el título o grado.");
            if (StartDate.Length > 0 && !JobApplicationProfile.IsExactMonth(StartDate))
                errors.Add(prefix + "la fecha de inicio debe usar AAAA-MM.");
            if (EndDate.Length > 0 && !JobApplicationProfile.IsExactMonth(EndDate))
                errors.Add(prefix + "la fecha de término debe usar AAAA-MM.");

            DateTime start;
            DateTime end;
            if (JobApplicationProfile.TryGetMonth(StartDate, out start) &&
                JobApplicationProfile.TryGetMonth(EndDate, out end) && end < start)
                errors.Add(prefix + "la fecha de término no puede ser anterior al inicio.");
        }
    }
}
