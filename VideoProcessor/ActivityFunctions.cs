using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using VideoProcessor.DataExtensions;
using VideoProcessor.Dtos;
using VideoProcessor.Models;
using VideoProcessor.Services;

namespace VideoProcessor
{
    public class ActivityFunctions
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _tableServiceClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IFakeLoadService _fakeLoadService;

        public ActivityFunctions(ILoggerFactory loggerFactory, 
                                 TableServiceClient tableServiceClient, 
                                 BlobServiceClient blobServiceClient, IFakeLoadService fakeLoadService)
        {
            _fakeLoadService = fakeLoadService;
            _blobServiceClient= blobServiceClient;
            _tableServiceClient= tableServiceClient;    
            _logger = loggerFactory.CreateLogger<ActivityFunctions>();
        }

        [Function(nameof(TranscodeVideo))]
        public async Task<VideoFileInfo> TranscodeVideo([ActivityTrigger] VideoFileInfo inputVideo, FunctionContext executionContext)
        {
            _logger.LogInformation($"Transcoding {inputVideo.Location} with Bitrate {inputVideo.BitRate}");

            await _fakeLoadService.LoadTest();

            var transcodedLocation = $"{Path.GetFileNameWithoutExtension(inputVideo.Location)}-kps.mp4";

            return new VideoFileInfo
            {
                Location = transcodedLocation,
                BitRate = inputVideo.BitRate
            };
        }

        [Function(nameof(ExtractThumbnail))]
        public async Task<string> ExtractThumbnail([ActivityTrigger] string inputVideo, FunctionContext executionContext)
        {
            _logger.LogInformation($"Extract thumbnail {inputVideo}");

           
            if (inputVideo.Contains("error"))
            {
                throw new InvalidOperationException("Failed to extract thumbnail");
            }

            await _fakeLoadService.LoadTest();

            return $"{Path.GetFileNameWithoutExtension(inputVideo)}-thumbnail.png";
        }

        [Function(nameof(PrependIntro))]
        public async Task<string> PrependIntro([ActivityTrigger] string inputVideo, FunctionContext executionContext)
        {
            var introLocation = Environment.GetEnvironmentVariable("IntroLocation");

            _logger.LogInformation($"Prepending Intro {introLocation} to {inputVideo}");

            await _fakeLoadService.LoadTest();

            return $"{Path.GetFileNameWithoutExtension(inputVideo)}-withintro.mp4";
        }

        [Function(nameof(Cleanup))]
        public async Task<string> Cleanup([ActivityTrigger] string?[] filesToCleanUp)
        {
            foreach (var file in filesToCleanUp.Where(f => f != null))
            {
                _logger.LogInformation($"Deleting {file}");

                await Task.Delay(1000);
            }

            return "Cleaned up successfully";
        }


        [Function(nameof(GetTransCodeBitrates))]    
        public async Task<int[]> GetTransCodeBitrates([ActivityTrigger] object input)
        {


            return await Task.FromResult(Environment.GetEnvironmentVariable("TranscodeBitRates").Split(",").Select(int.Parse).ToArray());
        }

        [Function(nameof(SendApprovalRequestEmail))]
        public async Task SendApprovalRequestEmail([ActivityTrigger] ApprovalInfo approvalInfo)
        {
            _logger.LogInformation($"Requesting approval for {approvalInfo.VideoLocation}");


            var approvalCode = Guid.NewGuid();


            var approvalsTable = _tableServiceClient.GetTableClient(ApprovalData.TableName);

            await approvalsTable.CreateIfNotExistsAsync();


            var approval = new ApprovalData
            {
                RowKey = approvalCode.ToString(),
                OrchestrationId = approvalInfo.OrchestrationId
            };

            await approvalsTable.AddEntityAsync(approval);

            
            var sendGridClient = new SendGridClient(Environment.GetEnvironmentVariable("SendGridKey"));


            _logger.LogInformation($"Sending approval request for {approvalInfo.VideoLocation}");

            var sendGridMessage = new SendGridMessage();
            sendGridMessage.CreateApprovalEmail(approvalCode.ToString(), approvalInfo.VideoLocation);

            await sendGridClient.SendEmailAsync(sendGridMessage);


        }

        [Function(nameof(PublishVideo))]
        public async Task PublishVideo([ActivityTrigger] string inputVideo)
        {
            _logger.LogInformation($"Publishing {inputVideo}");

            await Task.Delay(1000);
        }


        [Function(nameof(RejectVideo))]
        public async Task RejectVideo([ActivityTrigger] string inputVideo)
        {
            _logger.LogInformation($"Rejecting {inputVideo}");

            await Task.Delay(1000);
        }


        [Function(nameof(PeriodicActivity))]
        public async Task PeriodicActivity([ActivityTrigger] int timesRun)
        {
            _logger.LogWarning($"Running the periodic activity, times run = {timesRun}");
        }

    }
}