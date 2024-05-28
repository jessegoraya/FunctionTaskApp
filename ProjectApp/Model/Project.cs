using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CMaaS.Project.Model
{
    public class Project
    {
        //associated documentid that is used by Cosmos DB to uniquely identify a document in the DB
        [JsonProperty(PropertyName = "id")]
        public string id { get; set; }

        //Task ID is the partition key in Cosmos.  Consider making this a hash of tenant + process type 
        [JsonProperty(PropertyName = "ProjectID")]
        public string projectid { get; set; }

        [JsonProperty(PropertyName = "ProjectNames")]
        public string projectnames { get; set; }

        [JsonProperty(PropertyName = "ProjectDescription")]
        public string projectdescription { get; set; }

        //Contains all of the Asspciated People for a specific project
        [JsonProperty(PropertyName = "AssociatedPeople")]
        public List<AssociatedPeople> associatedpeople { get; set; }

        //Date the project was created
        [JsonProperty(PropertyName = "DateCreated")]
        public DateTime datecreated { get; set; }

        //Date the project was closed
        [JsonProperty(PropertyName = "DateClosed")]
        public DateTime dateclosed { get; set; }
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

        //voice for the person
        [JsonProperty(PropertyName = "PersonVoice")]
        public string personvoice { get; set; }
    }
}
