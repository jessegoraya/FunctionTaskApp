using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Taslow.Project.Model
{
    public class TaskProject
    {
        //associated documentid that is used by Cosmos DB to uniquely identify a document in the DB
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; } = string.Empty;

        //External Project ID outside of Taslow from a CRM system, Case Management system or something else from the customer, if projects aren't managed in Taslow.
        [JsonProperty(PropertyName = "ExtProjectID")]
        public string ExtProjectID { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "projectNames")]
        public string ProjectNames { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "projectDescription")]
        public string projectdescription { get; set; } = string.Empty;

        //set project type as one of 4 types: Delivery, Maintenance, Administrative, Capture
        [JsonProperty(PropertyName = "projectType")]
        public string projecttype { get; set; } = string.Empty;

        //set 
        [JsonProperty(PropertyName = "projectStatus")]
        public string projectstatus { get; set; } = string.Empty;

        //External customer/client email domains that can help the AI agent identify client-originated project work.
        [JsonProperty(PropertyName = "clientDomains")]
        public List<string> clientdomains { get; set; } = new();

        //set project status as Open or Archivied
        [JsonProperty(PropertyName = "DescVector")]
        public List<float> descvector { get; set; } = new();

        //Contains all of the Asspciated People for a specific project.  They see tasks associated to them in the My Tasks or Individual Tasks view of the app
        [JsonProperty(PropertyName = "AssociatedPeople")]
        public List<AssociatedPeople> associatedpeople { get; set; } = new();

        //Contains all of the Asspciated Managers for a specific project.  They get access to all tasks on the proejct in the Project Tasks view of the app
        [JsonProperty(PropertyName = "AssociatedManagers")]
        public List<AssociatedPeople> associatedmanagers { get; set; } = new();

        //Date the project was created
        [JsonProperty(PropertyName = "DateCreated")]
        public DateTime datecreated { get; set; }

        //Date the project was closed
        [JsonProperty(PropertyName = "DateClosed")]
        public DateTime dateclosed { get; set; }

        //associated tenant with the project
        [JsonProperty(PropertyName = "TenantID")]
        //CosmosDB stores tenant id as "tenantID in Partiion Key settings and JSON for items
        public string tenantid { get; set; } = string.Empty;
    }

    public class AssociatedPeople
    {
        [JsonProperty(PropertyName = "AssociatedPersonID")]
        public Guid associatedpersonid { get; set; }

        //Name of person 
        [JsonProperty(PropertyName = "PersonName")]
        public string personname { get; set; } = string.Empty;

        //Other names associated with the person
        [JsonProperty(PropertyName = "PersonAliases")]
        public string personaliases { get; set; } = string.Empty;

        //email for the person
        [JsonProperty(PropertyName = "PersonEmail")]
        public string personemail { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "Role")]
        public string role { get; set; } = string.Empty;

    }




}
