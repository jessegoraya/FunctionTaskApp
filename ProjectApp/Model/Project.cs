using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CMaaS.TaskProject.Model
{
    public class Project
    {
        //associated documentid that is used by Cosmos DB to uniquely identify a document in the DB
        [JsonProperty(PropertyName = "id")]
        public string id { get; set; }

        //External Project ID outside of Taslow from a CRM system, Case Management system or something else from the customer, if projects aren't managed in Taslow.
        [JsonProperty(PropertyName = "ExtProjectID")]
        public string projectid { get; set; }

        [JsonProperty(PropertyName = "ProjectName")]
        public string projectname { get; set; }

        [JsonProperty(PropertyName = "ProjectDescription")]
        public string projectdescription { get; set; }

        //set project type as one of 4 types: Delivery, Maintenance, Administrative, Capture
        [JsonProperty(PropertyName = "ProjectType")]
        public string projecttype { get; set; }

        //set 
        [JsonProperty(PropertyName = "ProjectStatus")]
        public string projectstatus { get; set; }

        //set project status as Open or Archivied
        [JsonProperty(PropertyName = "DescVector")]
        public string descvector { get; set; }

        //Contains all of the Asspciated People for a specific project
        [JsonProperty(PropertyName = "AssociatedPeople")]
        public List<AssociatedPeople> associatedpeople { get; set; }

        //Date the project was created
        [JsonProperty(PropertyName = "DateCreated")]
        public DateTime datecreated { get; set; }

        //Date the project was closed
        [JsonProperty(PropertyName = "DateClosed")]
        public DateTime dateclosed { get; set; }

        //associated tenant with the project
        [JsonProperty(PropertyName = "TenantID")]
        public string tenantid { get; set; }
    }

    public class AssociatedPeople
    {
        [JsonProperty(PropertyName = "AssociatedPersonID")]
        public Guid grouptaskid { get; set; }

        //Name of person 
        [JsonProperty(PropertyName = "PersonName")]
        public string grouptasktitle { get; set; }

        //Other names associated with the person
        [JsonProperty(PropertyName = "PersonAliases")]
        public string personaliases { get; set; }

        //email for the person
        [JsonProperty(PropertyName = "PersonEmail")]
        public string personemail { get; set; }

    }

}
