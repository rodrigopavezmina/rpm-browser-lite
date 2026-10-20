using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace ChatGPTLite
{
    [DataContract]
    internal sealed class SalesWorkspace
    {
        [DataMember(Order = 1)] public List<Opportunity> Opportunities { get; set; }
        [DataMember(Order = 2)] public List<WarRoomSession> WarRooms { get; set; }

        public static SalesWorkspace Create()
        {
            return new SalesWorkspace
            {
                Opportunities = new List<Opportunity>(),
                WarRooms = new List<WarRoomSession>()
            };
        }

        public void Repair()
        {
            if (Opportunities == null) Opportunities = new List<Opportunity>();
            if (WarRooms == null) WarRooms = new List<WarRoomSession>();
            foreach (Opportunity item in Opportunities)
                if (item != null) item.Repair();
        }
    }

    [DataContract]
    internal sealed class Opportunity
    {
        [DataMember(Order = 1)] public string Id { get; set; }
        [DataMember(Order = 2)] public string Name { get; set; }
        [DataMember(Order = 3)] public string Customer { get; set; }
        [DataMember(Order = 4)] public string Industry { get; set; }
        [DataMember(Order = 5)] public string Stage { get; set; }
        [DataMember(Order = 6)] public string Health { get; set; }
        [DataMember(Order = 7)] public string Owner { get; set; }
        [DataMember(Order = 8)] public string NextMilestone { get; set; }
        [DataMember(Order = 9)] public DateTime? NextMilestoneDate { get; set; }
        [DataMember(Order = 10)] public string Blocker { get; set; }
        [DataMember(Order = 11)] public string Probability { get; set; }
        [DataMember(Order = 12)] public string ProbabilityEvidence { get; set; }
        [DataMember(Order = 13)] public string PipelineValue { get; set; }
        [DataMember(Order = 14)] public string ProposalValue { get; set; }
        [DataMember(Order = 15)] public string BookingValue { get; set; }
        [DataMember(Order = 16)] public string ContractValue { get; set; }
        [DataMember(Order = 17)] public string RevenueValue { get; set; }
        [DataMember(Order = 18)] public string OperationalProblem { get; set; }
        [DataMember(Order = 19)] public string AffectedUsers { get; set; }
        [DataMember(Order = 20)] public string Impact { get; set; }
        [DataMember(Order = 21)] public string Stakeholders { get; set; }
        [DataMember(Order = 22)] public string DecisionProcess { get; set; }
        [DataMember(Order = 23)] public string KnownBudget { get; set; }
        [DataMember(Order = 24)] public string Timeframe { get; set; }
        [DataMember(Order = 25)] public string SuccessCriteria { get; set; }
        [DataMember(Order = 26)] public string ConfirmedFacts { get; set; }
        [DataMember(Order = 27)] public string Assumptions { get; set; }
        [DataMember(Order = 28)] public string MissingInformation { get; set; }
        [DataMember(Order = 29)] public string Competitors { get; set; }
        [DataMember(Order = 30)] public string Solution { get; set; }
        [DataMember(Order = 31)] public string Specialties { get; set; }
        [DataMember(Order = 32)] public string Architecture { get; set; }
        [DataMember(Order = 33)] public string Scope { get; set; }
        [DataMember(Order = 34)] public string Interfaces { get; set; }
        [DataMember(Order = 35)] public string Exclusions { get; set; }
        [DataMember(Order = 36)] public string Risks { get; set; }
        [DataMember(Order = 37)] public string Mitigations { get; set; }
        [DataMember(Order = 38)] public string CostInputs { get; set; }
        [DataMember(Order = 39)] public string DeliveryPlan { get; set; }
        [DataMember(Order = 40)] public string HandoverNotes { get; set; }
        [DataMember(Order = 41)] public string LessonsLearned { get; set; }
        [DataMember(Order = 42)] public DateTime CreatedUtc { get; set; }
        [DataMember(Order = 43)] public DateTime UpdatedUtc { get; set; }

        public static Opportunity Create()
        {
            Opportunity opportunity = new Opportunity();
            opportunity.Id = Guid.NewGuid().ToString("N");
            opportunity.Stage = "Identificación";
            opportunity.Health = "Sin evaluar";
            opportunity.CreatedUtc = DateTime.UtcNow;
            opportunity.UpdatedUtc = DateTime.UtcNow;
            opportunity.Repair();
            return opportunity;
        }

        public void Repair()
        {
            if (string.IsNullOrWhiteSpace(Id)) Id = Guid.NewGuid().ToString("N");
            string[] values = { "Name", "Customer", "Industry", "Stage", "Health", "Owner", "NextMilestone", "Blocker", "Probability", "ProbabilityEvidence", "PipelineValue", "ProposalValue", "BookingValue", "ContractValue", "RevenueValue", "OperationalProblem", "AffectedUsers", "Impact", "Stakeholders", "DecisionProcess", "KnownBudget", "Timeframe", "SuccessCriteria", "ConfirmedFacts", "Assumptions", "MissingInformation", "Competitors", "Solution", "Specialties", "Architecture", "Scope", "Interfaces", "Exclusions", "Risks", "Mitigations", "CostInputs", "DeliveryPlan", "HandoverNotes", "LessonsLearned" };
            foreach (string property in values)
            {
                var info = GetType().GetProperty(property);
                if (info.GetValue(this, null) == null) info.SetValue(this, string.Empty, null);
            }
            if (string.IsNullOrWhiteSpace(Stage)) Stage = "Identificación";
            if (string.IsNullOrWhiteSpace(Health)) Health = "Sin evaluar";
            if (CreatedUtc == DateTime.MinValue) CreatedUtc = DateTime.UtcNow;
            if (UpdatedUtc == DateTime.MinValue) UpdatedUtc = CreatedUtc;
        }
    }

    [DataContract]
    internal sealed class WarRoomSession
    {
        [DataMember(Order = 1)] public string Id { get; set; }
        [DataMember(Order = 2)] public string OpportunityId { get; set; }
        [DataMember(Order = 3)] public DateTime CreatedUtc { get; set; }
        [DataMember(Order = 4)] public string Objective { get; set; }
        [DataMember(Order = 5)] public string Deadline { get; set; }
        [DataMember(Order = 6)] public string ConfirmedFacts { get; set; }
        [DataMember(Order = 7)] public string Gaps { get; set; }
        [DataMember(Order = 8)] public string PendingDecisions { get; set; }
        [DataMember(Order = 9)] public string NextAction { get; set; }
        [DataMember(Order = 10)] public string Responsible { get; set; }
        [DataMember(Order = 11)] public string DueDate { get; set; }
        [DataMember(Order = 12)] public string SupportRequired { get; set; }
        [DataMember(Order = 13)] public string EscalationPath { get; set; }
        [DataMember(Order = 14)] public string ExecutiveSummary { get; set; }
    }

    internal static class SalesStore
    {
        public static string DataFile
        {
            get
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "B2B Copilot Lite");
                Directory.CreateDirectory(root);
                return Path.Combine(root, "oportunidades.json");
            }
        }

        public static SalesWorkspace Load()
        {
            SalesWorkspace workspace = JsonStore.Load<SalesWorkspace>(DataFile) ?? SalesWorkspace.Create();
            workspace.Repair();
            return workspace;
        }

        public static void Save(SalesWorkspace workspace)
        {
            workspace.Repair();
            JsonStore.Save(DataFile, workspace);
        }
    }
}
