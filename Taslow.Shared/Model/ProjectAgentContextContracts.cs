using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectAgentContextRequest
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("projectIds")]
        public List<string> ProjectIds { get; set; } = new();

        [JsonProperty("includeScopes")]
        public bool IncludeScopes { get; set; } = true;

        [JsonProperty("includeAssociatedPeople")]
        public bool IncludeAssociatedPeople { get; set; } = true;

        [JsonProperty("includeAssociatedManagers")]
        public bool IncludeAssociatedManagers { get; set; } = true;
    }

    public class ProjectAgentContextResponse
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("projects")]
        public List<ProjectAgentContextProject> Projects { get; set; } = new();
    }

    public class ProjectParticipantCandidateRequest
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("participantEmails")]
        public List<string> ParticipantEmails { get; set; } = new();
    }

    public class ProjectParticipantCandidateResponse
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("projects")]
        public List<ProjectParticipantCandidate> Projects { get; set; } = new();
    }

    public class ProjectParticipantCandidate
    {
        [JsonProperty("projectId")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonProperty("matchedParticipantEmails")]
        public List<string> MatchedParticipantEmails { get; set; } = new();
    }

    public class ProjectAgentContextProject
    {
        [JsonProperty("projectId")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonProperty("projectName")]
        public string ProjectName { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("projectStatus")]
        public string ProjectStatus { get; set; } = string.Empty;

        [JsonProperty("clientDomains")]
        public List<string> ClientDomains { get; set; } = new();

        [JsonProperty("associatedPeople")]
        public List<ProjectAgentContextPerson> AssociatedPeople { get; set; } = new();

        [JsonProperty("associatedManagers")]
        public List<ProjectAgentContextPerson> AssociatedManagers { get; set; } = new();

        [JsonProperty("scopes")]
        public List<ProjectAgentContextScope> Scopes { get; set; } = new();
    }

    public class ProjectAgentContextScope
    {
        [JsonProperty("scopeId")]
        public string ScopeId { get; set; } = string.Empty;

        [JsonProperty("scopeTitle")]
        public string ScopeTitle { get; set; } = string.Empty;

        [JsonProperty("scopeDescription")]
        public string ScopeDescription { get; set; } = string.Empty;

        [JsonProperty("groupTaskSetId")]
        public string GroupTaskSetId { get; set; } = string.Empty;
    }

    public class ProjectAgentContextPerson
    {
        [JsonProperty("personId")]
        public string PersonId { get; set; } = string.Empty;

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("aliases")]
        public string Aliases { get; set; } = string.Empty;

        [JsonProperty("role")]
        public string Role { get; set; } = string.Empty;
    }
}
