using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Web;
using VideoProcessor.Models;

namespace VideoProcessor
{
    public class HttpFunctions
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _tableServiceClient;

        public HttpFunctions(ILoggerFactory loggerFactory, TableServiceClient tableServiceClient)
        {
            _logger = loggerFactory.CreateLogger<HttpFunctions>();
            _tableServiceClient = tableServiceClient;
        }

        [Function(nameof(ProcessVideoStarter))]
        public async Task<HttpResponseData> ProcessVideoStarter([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req, [DurableClient] DurableTaskClient client)
        {
            var query = HttpUtility.ParseQueryString(req.Url.Query);

            var video = query.Get("video");

            if (video is null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync("ProcessVideoOrchestrator", video);
            _logger.LogInformation("Created new orchestration with instance ID = {instanceId}", instanceId);

            return client.CreateCheckStatusResponse(req, instanceId);
        }

        [Function(nameof(SubmitVideoApproval))]
        public async Task<HttpResponseData> SubmitVideoApproval(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "SubmitVideoApproval/{id:guid}")] HttpRequestData req,
            Guid? id,
            [DurableClient] DurableTaskClient client)
        {
            var query = HttpUtility.ParseQueryString(req.Url.Query);

            var result = query.Get("result");


            if (id is null)
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                response.WriteString("Need an approval result");

                return response;
            }

            var approvalDataClient = _tableServiceClient.GetTableClient(ApprovalData.TableName);

            var approvalData = await approvalDataClient.GetEntityIfExistsAsync<ApprovalData>(ApprovalData.ApprovalPartitionKey, id.ToString());

            if (!approvalData.HasValue)
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                response.WriteString("Need an approval result");

                return response;
            }

            _logger.LogWarning($"Sending approval result to {approvalData.Value.OrchestrationId} of {id}");


            await client.RaiseEventAsync(approvalData.Value.OrchestrationId, "ApprovalResult", result);


            return req.CreateResponse(HttpStatusCode.OK);
        }


        [Function(nameof(StartPeriodicTask))]
        public async Task<HttpResponseData> StartPeriodicTask(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequestData req, 
            [DurableClient] DurableTaskClient client)
        {


            var options = new StartOrchestrationOptions();
            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync("PeriodicTaskOrchestrator", options);

            return client.CreateCheckStatusResponse(req, instanceId);
        }
    }
}