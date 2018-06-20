using System;
using System.Collections.Concurrent;
using System.Configuration;
using AberrantSMPP;
using AberrantSMPP.Packet;
using AberrantSMPP.Packet.Request;
using AberrantSMPP.Packet.Response;
using AberrantSMPP.Utility;

namespace SMPPTester
{
    public class SmppHelper : IDisposable
    {
        private readonly SMPPCommunicator _client;

        private readonly BlockingCollection<SmppSubmitSmResp> _submissionResponses = new BlockingCollection<SmppSubmitSmResp>();
        private readonly BlockingCollection<SmppDeliverSm> _deliveryReceipts = new BlockingCollection<SmppDeliverSm>();
        private readonly BlockingCollection<SmppDeliverSm> _inboundMessages = new BlockingCollection<SmppDeliverSm>();

        public SmppHelper()
        {
            _client = new SMPPCommunicator
                      {
                          Host = ConfigurationManager.AppSettings["hostName"],
                          Port = Convert.ToUInt16(ConfigurationManager.AppSettings["port"]),
                          SystemId = ConfigurationManager.AppSettings["username"],
                          Password = ConfigurationManager.AppSettings["password"],
                          BindType = SmppBind.BindingType.BindAsTransceiver,
                          Version = Pdu.SmppVersionType.Version3_4
                      };

            _client.OnSubmitSmResp += (source, args) =>
            {
                _submissionResponses.Add(args.ResponsePdu);
            };

            _client.OnDeliverSm += (source, args) =>
            {
                _client.SendPdu(new SmppDeliverSmResp()
                {
                    CommandStatus = CommandStatus.ESME_ROK,
                    SequenceNumber = args.DeliverSmPdu.SequenceNumber,
                });

                if ((args.DeliverSmPdu.EsmClass & 0x04) == 0x4)
                    _deliveryReceipts.Add(args.DeliverSmPdu);
                else
                    _inboundMessages.Add(args.DeliverSmPdu);
            };

            _client.Bind();
        }

        public void Send(string orginator, string recipient, string body)
        {
            var submit = new SmppSubmitSm
            {
                DataCoding = DataCoding.SMSCDefault,
                DestinationAddress = recipient,
                DestinationAddressNpi = Pdu.NpiType.ISDN,
                DestinationAddressTon = Pdu.TonType.International,
                SourceAddress = orginator,
                SourceAddressNpi = Pdu.NpiType.Unknown,
                SourceAddressTon = Pdu.TonType.Alphanumeric,
                RegisteredDelivery = Pdu.RegisteredDeliveryType.OnSuccessOrFailure,
                ShortMessage = new GSMEncoding().GetBytes(body)
            };

            _client.SendPdu(submit);
        }

        public SmppSubmitSmResp WaitForSubmissionResponse(TimeSpan timeout)
        {
            if (_submissionResponses.TryTake(out var submissionResponse, timeout)) return submissionResponse;

            throw new Exception($"Did not receive Submission Response within {timeout}");
        }

        public SmppDeliverSm WaitForDeliveryReceipt(TimeSpan timeout)
        {
            var timeoutTime = DateTime.UtcNow + timeout;

            while (timeoutTime > DateTime.UtcNow)
            {
                if (_deliveryReceipts.TryTake(out var deliveryReceipt, timeoutTime - DateTime.UtcNow))
                {
                   return deliveryReceipt;                    
                }
            }

            throw new Exception($"Did not receive Delivery Receipt within {timeout}");

        }

        public SmppDeliverSm WaitForInboundMessage(TimeSpan timeout, string body)
        {
            var timeoutTime = DateTime.UtcNow + timeout;

            while (timeoutTime > DateTime.UtcNow)
            {
                if (_inboundMessages.TryTake(out var inboundMessage, timeoutTime - DateTime.UtcNow) &&
                    inboundMessage.ShortMessage == body) return inboundMessage;

            }
            throw new Exception($"Did not receive Inbound Message within {timeout}");
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}