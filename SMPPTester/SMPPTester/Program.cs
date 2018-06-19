using System;
using System.Collections.Generic;
using System.Configuration;

namespace SMPPTester
{
    class Program
    {
        static void Main()
        {
            var smppHelper = new SmppHelper();
            var count = int.Parse(ConfigurationManager.AppSettings["numberOfMessagesToSend"]);
            var orginator = ConfigurationManager.AppSettings["originator"];
            var recipient = ConfigurationManager.AppSettings["recipient"];
            var message = ConfigurationManager.AppSettings["message"];

            var submits = new List<string>();
            for (var i = 0; i <= count; i++)
            {
                smppHelper.Send(orginator, recipient, message);
                var submissionResponse = smppHelper.WaitForSubmissionResponse(TimeSpan.FromMinutes(1));
                var submissionResponseMessageId = submissionResponse.MessageId;

                submits.Add(submissionResponseMessageId);
                Console.WriteLine($"sent: {submissionResponseMessageId}");
            }

            var counter = 0;

            foreach (var submit in submits)
            {
                var deliverSm =
                    smppHelper.WaitForDeliveryReceipt(TimeSpan.FromMinutes(1), submit);
                Console.WriteLine($"messageNo : {counter++}; status: {deliverSm.MessageState}");
            }
            Console.WriteLine("finished");
            Console.ReadLine();
        }
    }
}
