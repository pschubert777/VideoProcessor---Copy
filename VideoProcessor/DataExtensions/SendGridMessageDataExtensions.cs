using SendGrid.Helpers.Mail;

namespace VideoProcessor.DataExtensions
{
    public static class SendGridMessageDataExtensions
    {
        public static void CreateApprovalEmail(this SendGridMessage sendGridMessage, string approvalCode, string? videoLocation)
        {

            var host = Environment.GetEnvironmentVariable("Host");
            var functionAddress = $"{host}/api/SubmitVideoApproval/{approvalCode}";
            var approvedLink = $"{functionAddress}?result=Approved";
            var rejectedLink = $"{functionAddress}?result=Rejected";
            var body = @$"Please review {videoLocation}
                          <a href=""{approvedLink}""> Approve </a> <br>
                          <a href=""{rejectedLink}""> Reject </a>";

            sendGridMessage.From = new EmailAddress(Environment.GetEnvironmentVariable("ApproverEmail"));
            sendGridMessage.Subject = "A video is awaiting approval";
            sendGridMessage.HtmlContent = body;
            sendGridMessage.AddTo(Environment.GetEnvironmentVariable("SenderEmail"));

        }
    }
}
