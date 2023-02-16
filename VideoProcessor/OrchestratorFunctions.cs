using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using VideoProcessor.Dtos;

namespace VideoProcessor
{
    public class OrchestratorFunctions
    {
        public OrchestratorFunctions(ILoggerFactory loggerFactory)
        {
        }

        [Function(nameof(ProcessVideoOrchestrator))]
        public async Task<object> ProcessVideoOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var logger = context.CreateReplaySafeLogger<OrchestratorFunctions>();

            var videoLocation = context.GetInput<string>();

            var retryPolicy = new RetryPolicy(1, TimeSpan.FromSeconds(5))
            {
                HandleAsync = ex => Task.FromResult(ex is InvalidOperationException)
            };

            var options = TaskOptions.FromRetryPolicy(retryPolicy);

            string? transcodedLocation = null;
            string? thumbnailLocation = null;
            string? withIntroduction = null;
            string? approvalResult = "Unknown";

            try
            {
                var transCodeResults = await context.CallSubOrchestratorAsync<VideoFileInfo[]>(nameof(TranscodeVideoOrchestrator));

                transcodedLocation = transCodeResults.OrderByDescending(x => x.BitRate).Select(x => x.Location).First();

                thumbnailLocation = await context.CallActivityAsync<string>("ExtractThumbnail", transcodedLocation, options);

                withIntroduction = await context.CallActivityAsync<string>("PrependIntro", transcodedLocation);

                await context.CallActivityAsync("SendApprovalRequestEmail", new ApprovalInfo
                {
                    OrchestrationId = context.InstanceId,
                    VideoLocation = videoLocation
                });

                try
                {
                    approvalResult = await context.WaitForExternalEvent<string>("ApprovalResult", TimeSpan.FromMinutes(1));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Timed out waiting for approval");
                    approvalResult = "Timed Out";
                }

                if (approvalResult == "Approved")
                {
                    await context.CallActivityAsync("PublishVideo", withIntroduction);
                }
                else
                {
                    await context.CallActivityAsync("RejectVideo", withIntroduction);
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation($"Caught an error from an activity: {ex.Message}");

                await context.CallActivityAsync<string>("Cleanup", new[] { transcodedLocation, thumbnailLocation, withIntroduction });

                return new
                {
                    Error = "Failed to process uploaded video",
                    Message = ex.Message
                };
            }

            return new
            {
                TransCoded = transcodedLocation,
                Thumnail = thumbnailLocation,
                WithIntroduction = withIntroduction,
                ApprovalResult = approvalResult
            };
        }

        [Function(nameof(TranscodeVideoOrchestrator))]
        public async Task<VideoFileInfo[]> TranscodeVideoOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var logger = context.CreateReplaySafeLogger<OrchestratorFunctions>();

            logger.LogInformation("Executing Sub-Orchestration");

            var videoLocation = context.GetInput<string>();

            try
            {
                var bitRates = await context.CallActivityAsync<int[]>("GetTransCodeBitrates", input: null);

                var transCodeTasks = new List<Task<VideoFileInfo>>();

                foreach (var bitRate in bitRates)
                {
                    var info = new VideoFileInfo { Location = videoLocation, BitRate = bitRate };
                    var task = await context.CallActivityAsync<VideoFileInfo>("TranscodeVideo", info);

                    
                }

     

                return new VideoFileInfo[0];
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        // Will end if there is an unhandled exception or use termination API
        // terminating does not terminate any activity functions in progress
        //Can pass data from previous invocation to the next and mutate the state
        //Can exit the orchestration if required
        //Can vary the interval between invocations
        // allow multiple concurrent instances of the workflow

        [Function(nameof(PeriodicTaskOrchestrator))]
        public async Task<int> PeriodicTaskOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var logger = context.CreateReplaySafeLogger<OrchestratorFunctions>();

            var timesRun = context.GetInput<int>();
            timesRun++;

            logger.LogInformation($"Starting the Periodic activity {context.InstanceId}, {timesRun}");

            await context.CallActivityAsync("PeriodicActivity", timesRun);

            var nextRun = context.CurrentUtcDateTime.AddSeconds(30);

            await context.CreateTimer(nextRun, CancellationToken.None);

            context.ContinueAsNew(timesRun);

            return timesRun;
        }
    }
}