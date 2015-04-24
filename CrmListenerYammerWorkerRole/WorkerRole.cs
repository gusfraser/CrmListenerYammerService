using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Services;
using Xrm;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Web;

namespace CrmListenerYammerWorkerRole
{
    public class WorkerRole : RoleEntryPoint
    {
      
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        public static Uri serviceBusUri;

        private string GlobalAccessToken
        {
            get
            {
                return CloudConfigurationManager.GetSetting("Yammer.GlobalAccessToken");
            }
        }

        public override void Run()
        {
            TopicDescription td = new TopicDescription(CloudConfigurationManager.GetSetting("ServiceBus.CRM.Topic"));
            td.MaxSizeInMegabytes = 5120;
            td.DefaultMessageTimeToLive = new TimeSpan(0, 1, 0);

            string connectionString = CloudConfigurationManager.GetSetting("ServiceBus.ConnectionString");

            var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);

            if (!namespaceManager.TopicExists(CloudConfigurationManager.GetSetting("ServiceBus.CRM.Topic")))
            {
                namespaceManager.CreateTopic(td);
            }

            if (!namespaceManager.SubscriptionExists(CloudConfigurationManager.GetSetting("ServiceBus.CRM.Topic"), CloudConfigurationManager.GetSetting("ServiceBus.CRM.Subscription")))
            {
                namespaceManager.CreateSubscription(
                    CloudConfigurationManager.GetSetting("ServiceBus.CRM.Topic"),
                    CloudConfigurationManager.GetSetting("ServiceBus.CRM.Subscription"));
            }

            ReceiveMessages();
        }

        public void ReceiveMessages()
        {
            string connectionString = CloudConfigurationManager.GetSetting("ServiceBus.ConnectionString");

            SubscriptionClient Client = SubscriptionClient.CreateFromConnectionString
                         (connectionString,
                         CloudConfigurationManager.GetSetting("ServiceBus.CRM.Topic"), CloudConfigurationManager.GetSetting("ServiceBus.CRM.Subscription"),
                         ReceiveMode.PeekLock);

            while (true)
            {
                var message = Client.Receive(TimeSpan.FromSeconds(15));

                if (message != null)
                {
                    var CrmEntityData = message.GetBody<RemoteExecutionContext>();
                    try
                    {
                        // TODO: Add other CRM entities; do other stuff if you ike
                        switch (CrmEntityData.PrimaryEntityName)
                        {
                            case "account":
                                handleAccountUpdate(CrmEntityData);
                                break;
                            default:
                                break;
                        }

                        message.Complete(); //Complete means we have done our bit and handled the message
                        EventLog.WriteEntry("Application", string.Format(@"message completed for {0}, primary entity id = {1}", CrmEntityData.PrimaryEntityName, CrmEntityData.PrimaryEntityId), EventLogEntryType.Information);
                    }
                    catch (Exception ex)
                    {
                        message.Abandon(); //Abandon leaves the message on the queue
                        EventLog.WriteEntry("Application", string.Format(
@"message = {0}
source = {1}
stack trace = {2}
primary entity type = {3}
primary entity id = {4}", ex.Message, ex.Source, ex.StackTrace, CrmEntityData.PrimaryEntityName, CrmEntityData.PrimaryEntityId), EventLogEntryType.Error);
                    }
                }
            }
        }

        private void handleAccountUpdate(RemoteExecutionContext CrmEntityData)
        {
            CrmConnection crmConnection = CrmConnection.Parse(CloudConfigurationManager.GetSetting("CRM.ConnectionString"));
            OrganizationService service = new OrganizationService(crmConnection);
            XrmServiceContext context = new XrmServiceContext(service);

            var accounts = context.AccountSet.Where(c => c.Id == CrmEntityData.PrimaryEntityId);

            foreach (var account in accounts)
            {
                AddUpdateAccount(account, context, service);
            }

        }


        public static string PostYammerMessage(string data, int groupId, string accessToken)
        {
            var result = string.Empty;

            // Build request-URI
            var endpoint = "https://www.yammer.com/api/v1/messages.json";

            var sb = new StringBuilder(endpoint);
            if (endpoint.Contains("?"))
                sb.Append("&access_token=" + accessToken);
            else
                sb.Append("?access_token=" + accessToken);

            var uri = new Uri(sb.ToString());
            var request = WebRequest.Create(uri) as HttpWebRequest; // Create the request
            if (request == null)
                result = "It failed.";

            // Add request properties
            request.Headers.Add("Authorization", "Bearer " + accessToken);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";

            // Format data
            var dataArr = data.Split(' ');
            var postData = "body=" + String.Join("+", dataArr);
            //postData += "&group_id=" + groupId;

            byte[] bytes = Encoding.UTF8.GetBytes(postData);
            request.ContentLength = bytes.Length;

            // Fire away, and read the response
            try
            {
                var requestStream = request.GetRequestStream();
                requestStream.Write(bytes, 0, bytes.Length);

                var response = request.GetResponse();
                var stream = response.GetResponseStream();
                if (stream != null)
                {
                    var reader = new StreamReader(stream);

                    var responseString = reader.ReadToEnd();
                    stream.Dispose();
                    reader.Dispose();

                    result = responseString;
                }
            }
            catch (WebException e)
            {
                var response = e.Response;
                // Handle the exception.
            }

            return result; // Return JSON result
        }

        ///
        private void AddUpdateAccount(Account account, XrmServiceContext context, OrganizationService service)
        {
            try
            {

                string accountName = account.Name;

                // TODO: Parameterise/determine this based on config 
                string url = HttpUtility.UrlEncode(string.Format("https://spevo15.crm4.dynamics.com/main.aspx?etc=1&extraqs=formid%3d8448b78f-8f42-454e-8e2a-f8196b0419af&id=%7b{0}%7d&pagetype=entityrecord>", account.Id.ToString()));

                PostYammerMessage("Account " + accountName + " created in CRM " + url, 0, GlobalAccessToken);
            
            }
            catch (Exception ex)
            {
                throw;
            }

        }

    }
}
