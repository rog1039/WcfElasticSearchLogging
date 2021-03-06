using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace WCFESMessageLogging
{
    public class MessageInspector : IDispatchMessageInspector
    {
        private readonly MessageLogEntryService _messageLogEntryService;

        public MessageInspector(MessageCaptureSettings settings)
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