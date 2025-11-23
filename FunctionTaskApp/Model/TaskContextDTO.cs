using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CMaaS.Task.Model
{
    public class TaskContextDTO
    //TaskContextDTO merges key fields from both Group Task and Project with remove lists so evertying from GTS to GT to ITS to IT is flattened to a single row based on the Assignee or Associated Role.  Also includes key fields from other objects like Project Name 
    {
      
                //id from Project Container to link Taslow Project to Taslow GTS
                [JsonProperty(PropertyName = "ProjectID")]
                public string projectid { get; set; }

                //associated tenant using 
                [JsonProperty(PropertyName = "TenantID")]
                public string tenantid { get; set; }

                //**Project Field: associated documentid that is used by Cosmos DB to uniquely identify a document in the DB
                [JsonProperty(PropertyName = "id")]
                public string id { get; set; }

                //**Project Field:External Project ID outside of Taslow from a CRM system, Case Management system or something else from the customer, if projects aren't managed in Taslow.
                [JsonProperty(PropertyName = "ExtProjectID")]
                public string extprojectid { get; set; }

                //**Project Field:
                [JsonProperty(PropertyName = "ProjectName")]
                public string projectname { get; set; }

                //Description of the project
                [JsonProperty(PropertyName = "ProjectDescription")]
                public string projectdescription { get; set; }

                //set project type as one of 4 types: Delivery, Support, Administrative, Capture
                [JsonProperty(PropertyName = "ProjectType")]
                public string projecttype { get; set; }

                //Status of the project
                [JsonProperty(PropertyName = "ProjectStatus")]
                public string projectstatus { get; set; }

                [JsonProperty(PropertyName = "GroupTaskID")]
                public string grouptaskid { get; set; }

                //Short Description of Action that needs to be completed by the Assignee
                [JsonProperty(PropertyName = "GroupTaskTitle")]
                public string grouptasktitle { get; set; }

                //Longer description of Action that needs to be completed
                [JsonProperty(PropertyName = "GroupTaskDescription")]
                public string grouptaskdescription { get; set; }

                //Describes the status of the case status as either being active or if close how was it closed
                [JsonProperty(PropertyName = "GroupTaskStatus")]
                public string grouptaskstatus { get; set; }

                //Date the that Action that needs to be completed is due
                [JsonProperty(PropertyName = "GroupTaskDueDate")]
                public DateTime grouptaskduedate { get; set; }

                //Date the Group Task was closed once the last action has been take against the task
                [JsonProperty(PropertyName = "GroupTaskClosedDate")]
                public DateTime grouptaskcloseddate { get; set; }

                //Any documents associated to the Group Task.  The list of available documents are those that are associated to the Group Task and are accessible to either the group of the user that is viewing the Group Task
                [JsonProperty(PropertyName = "AssociatedDocuments")]
                public List<AssoicatedDocument> assoicateddocuments { get; set; }

                //An abstract concept shown to identify that depending on the Task Type the user will have different LOB items that can be selected from the drop down   This allows the user to know which items they are working on 
                [JsonProperty(PropertyName = "AssociatedLOBItems")]
                public List<AssociatedLOBItem> assoicatedlobitems { get; set; }

                //Value of the type of Task associated to the Group Task
                [JsonProperty(PropertyName = "GroupTaskType")]
                public string grouptasktype { get; set; }

                //Identified the phase in which the Case Stage currently live within an open status as one of the following phases of Awaiting Assignment, Drafting, Reviewing, Approving.  If the Group Task status is set to the closed the stage provides the user at what stage in the lifecycle it was closed.
                [JsonProperty(PropertyName = "GroupTaskStage")]
                public string grouptaskstage { get; set; }

                //The office/group making the request for work to be completed
                [JsonProperty(PropertyName = "AssignorStakeholderGroup")]
                public string assignorstakeholdergroup { get; set; }

                //The office(s)/group(s) which will be carrying out the work. 1 for 1 with Assignor on Approve/Produce Tasks but maybe 1 to many on Send Group Tasks
                [JsonProperty(PropertyName = "AssigneeStakeholderGroup")]
                public string assigneestakeholdergroup { get; set; }

                //Additional notes for the assingee as they process the request
                [JsonProperty(PropertyName = "GroupTaskNotes")]
                public string groupetasknotes { get; set; }

                //User marks this as yes once SMEs, Reviewers & Approvers (if needed) and due dates for each are set
                [JsonProperty(PropertyName = "FacilitiationComplete")]
                public Boolean facilitationcomplete { get; set; }

                //Hidden field which will be automatically set after a user sets the Facilitation Complete field to true/yes on a Group Task for the first time.  By that field being set to yes and the Facilitation Previously Complete field being set to false the Fulfill Assigning workflow will be executed.
                [JsonProperty(PropertyName = "FacilitiationPreviouslyComplete")]
                public Boolean facilitationpreviouslycomplete { get; set; }

                //Identifies if the Group Task has been cancelled and notifications have already been sent in order to prevent duplication
                [JsonProperty(PropertyName = "CancellationSent")]
                public Boolean cancellationsent { get; set; }

                [JsonProperty(PropertyName = "ParentGroupTaskID")]
                public Guid parentgrouptaskid { get; set; }

                //The user who initially created the Group Task
                [JsonProperty(PropertyName = "GTCreatedBy")]
                public string gtcreatedby { get; set; }

                //Date captured by the system identifying the creation of the Group Task within the system
                [JsonProperty(PropertyName = "GTCreatedDate")]
                public DateTime gtcreateddate { get; set; }

                //The user who last modified the Group Task
                [JsonProperty(PropertyName = "GTLastModifiedBy")]
                public string gtlastmodifiedby { get; set; }

                //Date captured by the system identifying the last modified date of the Group Task instance within the system
                [JsonProperty(PropertyName = "GTLastModifiedDate")]
                public DateTime gtlastmodifieddate { get; set; }

                //Individual Task Set
                //Contains 1 or more of the individual tasks seperated by type (facilitator, sme, review, approve) 
                [JsonProperty(PropertyName = "IndividualTaskSetID")]
                public string individualtasksetid { get; set; }

                //The user who initially created the Group Task
                [JsonProperty(PropertyName = "ITSCreatedBy")]
                public string GTcreatedby { get; set; }

                //Date captured by the system identifying the creation of the Group Task within the system
                [JsonProperty(PropertyName = "ITSCreatedDate")]
                public DateTime GTcreateddate { get; set; }
            
                //unique id for the individual task
                [JsonProperty(PropertyName = "IndividualTaskID")]
                public string individualtaskid { get; set; }

                //Identifies where in a life cycle the task currently lies
                [JsonProperty(PropertyName = "IndividualTaskStatus")]
                public string individualtaskstatus { get; set; }

                //Provides a description of the task which is automatically generated from the Group Task
                [JsonProperty(PropertyName = "IndividualTaskTitle")]
                public string individualtasktitle { get; set; }

                //Defines the type of task being set
                [JsonProperty(PropertyName = "IndividualTaskType")]
                public string individualtasktype { get; set; }

                //Provides a description of the task which is automatically generated from the Group Task
                [JsonProperty(PropertyName = "IndividualTaskDescription")]
                public string individualtaskdescription { get; set; }

                //Provides a place for the individual carrying out the task to add additional comments for the record
                [JsonProperty(PropertyName = "IndividualTaskNotes")]
                public string individualtasknotes { get; set; }

                //Identifies the priority level of the task
                [JsonProperty(PropertyName = "Priority")]
                public string priority { get; set; }

                //array of people assigned to this task
                [JsonProperty(PropertyName = "AssignedPerson")]
                public string assignedperson { get; set; }

                //group associated to role of the assigned person
                [JsonProperty(PropertyName = "AssociatedRole")]
                public string associatedrole { get; set; }

                //Used to ensure that duplicate notifications will not be sent to users
                [JsonProperty(PropertyName = "PreviouslySent")]
                public Boolean previouslysent { get; set; }

                //array of dates for the assigned person
                [JsonProperty(PropertyName = "IndividualTaskAssignedDate")]
                public DateTime individualtaskassigneddate { get; set; }

                //Identifies the due date that the work outline in the individual task should be completed
                [JsonProperty(PropertyName = "IndividualTaskDueDate")]
                public DateTime individualtaskduedate { get; set; }

                //date the task was cancelled
                [JsonProperty(PropertyName = "IndividualTaskCancelledDate")]
                public DateTime cancelleddated { get; set; }

                //decision of the individual if the task type is Approval
                [JsonProperty(PropertyName = "IndividualTaskApprovalDecision")]
                public string individualtaskapprovaldecision { get; set; }

                //date the task was actually completed
                [JsonProperty(PropertyName = "IndividualTaskCompletedDate")]
                public DateTime individualtaskcompleteddate { get; set; }

                //The user who initially created the Group Task
                [JsonProperty(PropertyName = "ITCreatedBy")]
                public string ITcreatedby { get; set; }

                //Date captured by the system identifying the creation of the Group Task within the system
                [JsonProperty(PropertyName = "ITCreatedDate")]
                public DateTime ITcreateddate { get; set; }
     }

}
