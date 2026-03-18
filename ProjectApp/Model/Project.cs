using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Project.Model
{
    public class TaskProject
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "ExtProjectID")]
        public string ExtProjectID { get; set; }

        [JsonProperty(PropertyName = "ProjectName")]
        public string ProjectNames { get; set; }

        [JsonProperty(PropertyName = "ProjectDescription")]
        public string projectdescription { get; set; }

        [JsonProperty(PropertyName = "ProjectType")]
        public string projecttype { get; set; }

        [JsonProperty(PropertyName = "ProjectStatus")]
        public string projectstatus { get; set; }

        [JsonProperty(PropertyName = "DescVector")]
        public List<float> descvector { get; set; } = new();

        [JsonProperty(PropertyName = "AssociatedPeople")]
        public List<AssociatedPeople> associatedpeople { get; set; } = new();

        [JsonProperty(PropertyName = "AssociatedManagers")]
        public List<AssociatedPeople> associatedmanagers { get; set; } = new();

        [JsonProperty(PropertyName = "ProjectScopes")]
        public List<ProjectScope> projectscopes { get; set; } = new();

        [JsonProperty(PropertyName = "DateCreated")]
        public DateTime datecreated { get; set; }

        [JsonProperty(PropertyName = "DateClosed")]
        public DateTime dateclosed { get; set; }

        [JsonProperty(PropertyName = "LastModifiedDate")]
        public DateTime lastmodifieddate { get; set; }

        [JsonProperty(PropertyName = "tenantID")]
        public string tenantid { get; set; }
    }

    public class ProjectScope
    {

        [JsonProperty(PropertyName = "ScopeID")]
        public string scopeid { get; set; }

        [JsonProperty(PropertyName = "ProjectScopeAreaTitle")]
        public string projectscopeareatitle { get; set; }

        [JsonProperty(PropertyName = "ProjectScopeArea")]
        public string projectscopearea { get; set; }

        [JsonProperty(PropertyName = "ProjectScopeAreaEmbeddings")]
        public List<float> projectscopeareaembeddings { get; set; } = new();

        //ScopeID is just the "id" from Task 
        [JsonProperty(PropertyName = "GroupTaskSetID")]
        public string grouptasksetid { get; set; }

        [JsonProperty(PropertyName = "IsArchived")]
        public bool isarchived { get; set; }
    }

    public class AssociatedPeople
    {
        [JsonProperty(PropertyName = "AssociatedPersonID")]
        public Guid associatedpersonid { get; set; }

        [JsonProperty(PropertyName = "PersonName")]
        public string personname { get; set; }

        [JsonProperty(PropertyName = "PersonAliases")]
        public string personaliases { get; set; }

        [JsonProperty(PropertyName = "PersonEmail")]
        public string personemail { get; set; }

        [JsonProperty(PropertyName = "Role")]
        public string role { get; set; }
    }
}
