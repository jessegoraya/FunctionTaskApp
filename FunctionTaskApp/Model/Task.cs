using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace CMaaS.Task.Model
{
    public class GroupTaskSet
    {
       
        //Contains all of Group Tasks for a specific case, event, item, person/organization (or largest centralizing object)
        [JsonProperty(PropertyName = "GroupTask")]
        public List<GroupTask> grouptask { get; set; }

        //id from Project Container to link Taslow Project to Taslow GTS
        [JsonProperty(PropertyName = "ProjectID")]
        public string caseid { get; set; }

        //associated tenant using 
        [JsonProperty(PropertyName = "TenantID")]
        public string tenantid { get; set; }

        //associated documentid that is used by Cosmos DB to uniquely identify a document in the DB
        [JsonProperty(PropertyName = "id")]
        public string id { get; set; }


    }

    public class GroupTask
    {
        [JsonProperty(PropertyName = "_type")]
        public string _type { get; set; }

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
        public List<GroupTaskDueDate> grouptaskduedate { get; set; }

        //Date the Group Task was closed once the last action has been take against the task
        [JsonProperty(PropertyName = "GroupTaskClosedDate")]
        public DateTime grouptaskcloseddate { get; set; }

        //Any documents associated to the Group Task.  The list of available documents are those that are associated to the Group Task and are accessible to either the group of the user that is viewing the Group Task
        [JsonProperty(PropertyName = "AssoicatedDocuments")]
        public List<AssoicatedDocument> assoicateddocuments { get; set; }

        //An abstract concept shown to identify that depending on the Task Type the user will have different LOB items that can be selected from the drop down   This allows the user to know which items they are working on 
        [JsonProperty(PropertyName = "AssoicatedLOBItems")]
        public List<AssociatedLOBItem> assoicatedlobitems { get; set; }

        //Value of the type of Task associated to the Group Task
        [JsonProperty(PropertyName = "GroupTaskType")]
        public string grouptasktypeid { get; set; }

        //Identified the phase in which the Case Stage currently live within an open status as one of the following phases of Awaiting Assignment, Drafting, Reviewing, Approving.  If the Group Task status is set to the closed the stage provides the user at what stage in the lifecycle it was closed.
        [JsonProperty(PropertyName = "GroupTaskStage")]
        public string grouptaskstage { get; set; }

        //The office/group making the request for work to be completed
        [JsonProperty(PropertyName = "AssignorStakeholderGroup")]
        public AssignorStakeholderGroup assignorstakeholdergroup { get; set; }

        //The office(s)/group(s) which will be carrying out the work. 1 for 1 with Assignor on Approve/Produce Tasks but maybe 1 to many on Send Group Tasks
        [JsonProperty(PropertyName = "AssigneeStakeholderGroup")]
        public List<AssigneeStakeholderGroup> assigneestakeholdergroup { get; set; }

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
        [JsonProperty(PropertyName = "CreatedBy")]
        public string createdby { get; set; }

        //Date captured by the system identifying the creation of the Group Task within the system
        [JsonProperty(PropertyName = "CreatedDate")]
        public DateTime createddate { get; set; }

        //The user who last modified the Group Task
        [JsonProperty(PropertyName = "LastModifiedBy")]
        public string lastmodifiedby { get; set; }

        //Date captured by the system identifying the last modified date of the Group Task instance within the system
        [JsonProperty(PropertyName = "LastModifiedDate")]
        public DateTime lastmodifieddate { get; set; }

        //Individual task array under the Group Task
        [JsonProperty(PropertyName = "IndividualTaskSets")]
        public List<IndividualTaskSet> individualtasksets { get; set; }
    }

    public class IndividualTaskSet
    {
        //Contains 1 or more of the individual tasks seperated by type (facilitator, sme, review, approve) 
        [JsonProperty(PropertyName = "IndividualTaskSetID")]
        public string individualtasksetid { get; set; }

        //The user who initially created the Group Task
        [JsonProperty(PropertyName = "CreatedBy")]
        public string createdby { get; set; }

        //Date captured by the system identifying the creation of the Group Task within the system
        [JsonProperty(PropertyName = "CreatedDate")]
        public DateTime createddate { get; set; }

        //Individual task array under the Group Task
        [JsonProperty(PropertyName = "IndividualTask")]
        public List<IndividualTask> individualtask { get; set; }
    }

    public class IndividualTask
    {
        //unique id for the individual task
        [JsonProperty(PropertyName = "IndividualTaskID")]
        public string individualtaskid { get; set; }

        //Identifies where in a life cycle the task currently lies
        [JsonProperty(PropertyName = "IndividualTaskStatus", Required = Required.Always)]
        public string individualtaskstatus { get; set; }

        //Provides a description of the task which is automatically generated from the Group Task
        [JsonProperty(PropertyName = "IndividualTaskTitle", Required = Required.Always)]
        public string individualtasktitle { get; set; }

        //Defines the type of task being set
        [JsonProperty(PropertyName = "IndividualTaskType", Required = Required.Always)]
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
        [JsonProperty(PropertyName = "CreatedBy")]
        public string createdby { get; set; }

        //Date captured by the system identifying the creation of the Group Task within the system
        [JsonProperty(PropertyName = "CreatedDate")]
        public DateTime createddate { get; set; }

        public static implicit operator IndividualTask(List<IndividualTask> v)
        {
            throw new NotImplementedException();
        }
    }

    public class AssoicatedDocument
    {
        [JsonProperty(PropertyName = "DocumentGUID")]
        public Guid documentguid { get; set; }
    }

    public class AssociatedLOBItem
    {
        [JsonProperty(PropertyName = "LOBID")]
        public Guid lobid { get; set; }

        [JsonProperty(PropertyName = "LOBType")]
        public string lobtype { get; set; }
    }

    public class GroupTaskDueDate
    {
        [JsonProperty(PropertyName = "GroupTaskDueDateSequence")]
        int grouptaskduedatesequence { get; set; }

        [JsonProperty(PropertyName = "GroupTaskDueDate")]
        public DateTime grouptaskduedate { get; set; }

        [JsonProperty(PropertyName = "LastGroupTaskDueDate")]
        public DateTime lastgrouptaskduedate { get; set; }
    }

    public class GroupTaskTypeLOV
    {
        [JsonProperty(PropertyName = "GroupTaskTypeID")]
        public Guid grouptasktypeid { get; set; }

        [JsonProperty(PropertyName = "GroupTaskType")]
        public string grouptasktype { get; set; }
    }

    public class GroupTaskStageLOV
    {
        [JsonProperty(PropertyName = "GroupTaskStageID")]
        public Guid grouptaskstageid { get; set; }

        [JsonProperty(PropertyName = "GroupTaskStageVal")]
        public string grouptaskstage { get; set; }
    }

    public class AssignorStakeholderGroup
    {
        [JsonProperty(PropertyName = "AssignorStakeholderGroupID")]
        public Guid assignorstakeholdergroupid { get; set; }

        [JsonProperty(PropertyName = "AssignorStakeholderGroup")]
        public string assignorstakeholdergroup { get; set; }
    }

    public class AssigneeStakeholderGroup
    {
        [JsonProperty(PropertyName = "AssigneeStakeholderGroupID")]
        public Guid assigneestakeholdergroupid { get; set; }

        [JsonProperty(PropertyName = "AssigneeStakeholderGroup")]
        public string assigneestakeholdergroup { get; set; }
    }

    public class ReturnTaskObject
    {
        // get ids for a Task Object subelements - (id, gtid, itsid(s), itid(s))

        [JsonProperty(PropertyName = "Valid")]
        //used to set if what is being returned was not setReturnTaskObject rto
        public Boolean valid { get; set; }

        [JsonProperty(PropertyName = "ProjectID")]
        public string projectid { get; set; }

        [JsonProperty(PropertyName = "TenantID")]
        public string tenantid { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string id { get; set; }

        [JsonProperty(PropertyName = "GroupTaskID")]
        public string grouptaskid { get; set; }

        [JsonProperty(PropertyName = "IndividualTaskSetID")]
        public string individualtasksetid { get; set; }

        [JsonProperty(PropertyName = "IndividualTaskID")]
        public string individualtaskidorindex { get; set; }


    }
}

