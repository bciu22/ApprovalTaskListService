#  Approval Task List Service

The Approval Task List Service installs as a SharePoint Web Service, and implements one method: GetAssociatedWorkflowItemTasks

## GetAssociatedWorkflowItemTasks

This method allows a web service data consumer to obtain all "Approval Tasks" in a SharePoint list related to a SharePoint Designer Approval Workflow.
Since the WorkflowItemID column of a task list is hidden, it cannot be queried by normal means, and we must use the Server Object Model.

This method accepts TaskListName and WorkflowItemID as parameters

This method returns XML with each approval object.

This method can be used as a SOAP call from within InfoPath to generate a tabular list of all approvals pertaining to a particular form