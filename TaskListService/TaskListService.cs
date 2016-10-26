using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint;
using System.Web.Services;
using System.Diagnostics;
using System.Data;
using System.Text.RegularExpressions;
using System.Web;
using System.Collections;

namespace TaskListService
{
    [WebService(Namespace = "http://bucksiu.org/webservices/TaskListWebService")]
    public class TaskListService : WebService
    {
        [WebMethod]
        public DataSet GetFormApprovalTasks(string TaskListName, string subsiteName, string FormURL)
        {
             //gets approvals related to the same workflowitemid for a supplied approval id
            SPWeb thisSite = SPContext.Current.Site.WebApplication.Sites[subsiteName].OpenWeb();
            Debug.WriteLine("Found Site " + thisSite.Name.ToString());
            SPList TaskList = thisSite.Lists.TryGetList(TaskListName);
            if ( TaskList == null )
            {
                throw new Exception("Unable to locate requested Task List: " + TaskListName);
            }
            SPQuery query = new SPQuery();
            query.Query = string.Concat(
                        "<Where><Contains>",
                            "<FieldRef Name='WorkflowLink'/>",
                            "<Value Type='String'>" + FormURL + "</Value>",
                        "</Contains></Where>");

            query.ViewFields = "<FieldRef Name='WorkflowItemId' />";
            query.ViewFieldsOnly = true; // Fetch only the data that we need.
            SPListItemCollection items = TaskList.GetItems(query);
            var distinctItems = (from SPListItem item in items select item["WorkflowItemId"]).Distinct().ToArray();
            if (distinctItems.Length != 1 )
            {
                throw new Exception("Not exactly one related document.  Please verify the name, and try again.  Related Documents: "+distinctItems.Length.ToString());
            }
            string WorkflowItemId = distinctItems[0].ToString();
            return GetSiteApprovalTasks(subsiteName, TaskListName, int.Parse(WorkflowItemId));
        }

        [WebMethod]
        public DataSet GetRelatedTasks(string TaskListName, int taskID)
        {
            //gets approvals related to the same workflowitemid for a supplied approval id
            SPWeb thisSite = SPContext.Current.Site.OpenWeb();
            SPList TaskList = thisSite.Lists.TryGetList(TaskListName);
            SPListItem item = TaskList.GetItemById(taskID);
            string WorkflowItemId = item["WorkflowItemId"].ToString();
            return GetApprovalTasks(TaskListName, int.Parse(WorkflowItemId));
        }
        [WebMethod]
        public DataSet GetApprovalTasks(string TaskListName, int WorkflowItemID)  
        {
            //gets workflow items for a list in the current site's context for "well-behaved" SOAP clients.
            string subsiteName = SPContext.Current.Site.OpenWeb().Url;
            return GetSiteApprovalTasks(subsiteName, TaskListName, WorkflowItemID);
        }
        [WebMethod]
        public DataSet GetSiteApprovalTasks(string subsiteName, string TaskListName, int WorkflowItemID) 
        {
            // ugly workaround for when the SOAP call is executed against the top level site collection according to the WSDL and the client (InfoPath) cannot explicitly 
            // set the site collection URL in the request.
            SPList TaskList = null;
            try
            {
                SPWeb thisSite = SPContext.Current.Site.WebApplication.Sites[subsiteName].OpenWeb();
                Debug.WriteLine("Found Site " + thisSite.Name.ToString());
                TaskList = thisSite.Lists.TryGetList(TaskListName);
                if(TaskList != null)
                {
                    // Build a query.
                    SPQuery query = new SPQuery();
                    query.Query = string.Concat(
                        "<Where><Eq>",
                            "<FieldRef Name='WorkflowItemId'/>",
                            "<Value Type='Integer'>" + WorkflowItemID + "</Value>",
                        "</Eq></Where>");

                    query.ViewFields = string.Concat(
                        "<FieldRef Name='ID' />",
                        "<FieldRef Name='Title' />",
                        "<FieldRef Name='Status' />",
                        "<FieldRef Name='AssignedTo' />",
                        "<FieldRef Name='WorkflowItemId' />",
                        "<FieldRef Name='Completed' />",
                        "<FieldRef Name='ExtendedProperties' />",
                        "<FieldRef Name='Modified' />");

                    Debug.WriteLine("Query: " + query.Query.ToString());
                    Debug.WriteLine("ViewFields : " + query.ViewFields.ToString());
                    query.ViewFieldsOnly = true; // Fetch only the data that we need.
                    SPListItemCollection items = null;
                    try
                    {
                        items = TaskList.GetItems(query);
                    }
                    catch (Exception exc)
                    {
                        Debug.WriteLine("Error invoking List Query:  " + exc.ToString());
                    }

                    if (items != null && items.Count > 0)
                    {
                        Debug.WriteLine(items.Count.ToString() + " Items Found");
                        DataTable results = items.GetDataTable();
                        results.BeginLoadData();
                        results.Columns.Add("Comments");
                        results.Columns.Add("TaskStatus");
                        foreach (DataRow row in results.Rows)
                        {
                            string pattern = @"ows_FieldName_Comments='(.*?)'";
                            //string extendedProperties = HttpUtility.HtmlDecode(row["ExtendedProperties"].ToString());  // this should never have been here since we need to escape comment values
                            string extendedProperties = row["ExtendedProperties"].ToString();
                            MatchCollection comments = Regex.Matches(extendedProperties, pattern, RegexOptions.Singleline);
                            if (comments.Count > 0)
                            {

                                row["Comments"] = HttpUtility.HtmlDecode(comments[0].Groups[1].Value);
                            }

                            pattern = @"ows_TaskStatus='(.*?)'";
                            MatchCollection taskStatus = Regex.Matches(extendedProperties, pattern, RegexOptions.Singleline);
                            pattern = @"ows_FieldName_DelegateTo='(.*)<pc:DisplayName>(.*)</pc:DisplayName>";
                            MatchCollection delegateTo = Regex.Matches(extendedProperties, pattern, RegexOptions.Singleline);
                            if (taskStatus.Count > 0)
                            {
                                if(delegateTo.Count > 0)
                                {
                                    row["TaskStatus"] = "Delegate: " + HttpUtility.HtmlDecode(delegateTo[0].Groups[2].Value);
                                }
                                else
                                {
                                    if (taskStatus[0].Groups[1].Value == "ChangeView")
                                    {

                                        row["TaskStatus"] = "Change Completed. Workflow Restarted";
                                    }
                                    else if(taskStatus[0].Groups[1].Value == "ChangeRequest")
                                    {
                                        row["TaskStatus"] = "Change Requested";
                                        pattern = @"ows_FieldName_NewDescription='(.*?)'";
                                        MatchCollection changeComments = Regex.Matches(extendedProperties, pattern, RegexOptions.Singleline);
                                        row["Comments"] += HttpUtility.HtmlDecode(changeComments[0].Groups[1].Value);
                                    }
                                    else
                                    {
                                        row["TaskStatus"] = HttpUtility.HtmlDecode(taskStatus[0].Groups[1].Value);
                                    }
                                  
                                }
                               
                            }
                            else
                            {
                                row["TaskStatus"] = "Pending";
                            }
                        }
                        results.EndLoadData();
                        results.Columns.Remove("ExtendedProperties");
                        DataSet resultSet = new DataSet();
                        resultSet.Tables.Add(results);
                        return resultSet;
                    }
                    else
                    {
                        Debug.WriteLine("No List Items Found");
                    }
                }
            }
            catch (Exception exc)
            {
                Debug.WriteLine("Unable to locate list " + TaskListName);
            }

            
            return null;
            
        }
    }
}
