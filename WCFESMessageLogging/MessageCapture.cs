using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Xml;

namespace WCFESMessageLogging
{
    public class MessageCapture : IDispatchMessageInspector
    {
        private readonly MessageLogEntryService _messageLogEntryService;

        public MessageCapture(MessageCaptureSettings settings)
        {
            _messageLogEntryService = new MessageLogEntryService(settings);
        }

        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            var messageLogEntry = _messageLogEntryService.CreateMessageLogEntry(ref request);
            return messageLogEntry;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            _messageLogEntryService.FinishMessageLogEntry(ref reply, correlationState);
        }
    }
}