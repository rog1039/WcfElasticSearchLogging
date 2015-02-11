using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;

namespace WCFESMessageLogging
{
    public class MessageLogEntryService
    {
        private readonly MessageCaptureSettings _settings;
        private readonly MessageLoggingProcessorLoop _messageLogProcessingLoop;

        public MessageLogEntryService(MessageCaptureSettings settings)
        {
            _settings = settings;
            _messageLogProcessingLoop = new MessageLoggingProcessorLoop(settings);
        }

        public MessageLogEntry CreateMessageLogEntry(ref Message request)
        {
            Uri requestUri = request.Headers.To;
            var messageLogEntry = new MessageLogEntry(requestUri.AbsoluteUri);

            ExtractOperationName(messageLogEntry);
            var h = OperationContext.Current.IncomingMessageHeaders;

            CaptureRemoteEndpointAddress(request, messageLogEntry);

            HttpRequestMessageProperty httpReq =
                (HttpRequestMessageProperty) request.Properties[HttpRequestMessageProperty.Name];
            messageLogEntry.HttpMethod = httpReq.Method;
            messageLogEntry.RequestSize = ExtractHttpContentLength(httpReq);

            foreach (var header in httpReq.Headers.AllKeys)
            {
                messageLogEntry.Headers.Add(header, httpReq.Headers[header]);
            }

            if (!request.IsEmpty)
            {
                var messageToString = MessageToString(ref request);
                messageLogEntry.RequestBody = messageToString;
            }

            messageLogEntry.MarkOperationAsStarted();
            _messageLogProcessingLoop.AddMessageLogEntry(messageLogEntry);
            return messageLogEntry;
        }

        private static void CaptureRemoteEndpointAddress(Message request, MessageLogEntry messageLogEntry)
        {
            try
            {
                RemoteEndpointMessageProperty remoteEndpoint = request.Properties[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
                messageLogEntry.RemoteEndpointAddress = remoteEndpoint.Address;
                messageLogEntry.RemoteEndpointPort = remoteEndpoint.Port;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void ExtractOperationName(MessageLogEntry messageLogEntry)
        {
            try
            {
                var action = OperationContext.Current.IncomingMessageHeaders.Action;
                var operationName = action.Substring(action.LastIndexOf("/") + 1);
                messageLogEntry.OperationName = operationName;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static long ExtractHttpContentLength(HttpRequestMessageProperty httpReq)
        {
            try
            {
                long contentLength = 0;
                long.TryParse(httpReq.Headers["Content-Length"], out contentLength);
                return contentLength;
            }
            catch (Exception e)
            {
                return 0;
            }
        }

        public void FinishMessageLogEntry(ref Message reply, object correlationState)
        {
            var messageCorrelation = (MessageLogEntry) correlationState;

            messageCorrelation.MarkOperationAsFinished();
            CaptureHttpStatusCode(reply, messageCorrelation);

            if (!reply.IsEmpty)
            {
                var messageToString = MessageToString(ref reply);
                messageCorrelation.ResponseSize = messageToString.Length;
            }

            _messageLogProcessingLoop.AddMessageLogEntry(messageCorrelation);
        }

        private static void CaptureHttpStatusCode(Message reply, MessageLogEntry messageLogEntry)
        {
            try
            {
                HttpResponseMessageProperty httpResp =
                    (HttpResponseMessageProperty)reply.Properties[HttpResponseMessageProperty.Name];
                messageLogEntry.HttpResponseStatusCode = httpResp.StatusCode.ToString();
                messageLogEntry.HttpResponseStatusCodeInt = (int)httpResp.StatusCode;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static WebContentFormat GetMessageContentFormat(Message message)
        {
            WebContentFormat format = WebContentFormat.Default;
            if (message.Properties.ContainsKey(WebBodyFormatMessageProperty.Name))
            {
                WebBodyFormatMessageProperty bodyFormat;
                bodyFormat = (WebBodyFormatMessageProperty)message.Properties[WebBodyFormatMessageProperty.Name];
                format = bodyFormat.Format;
            }

            return format;
        }

        private static string MessageToString(ref Message message)
        {
            WebContentFormat messageFormat = GetMessageContentFormat(message);
            MemoryStream ms = new MemoryStream();
            XmlDictionaryWriter writer = null;
            switch (messageFormat)
            {
                case WebContentFormat.Default:
                case WebContentFormat.Xml:
                    writer = XmlDictionaryWriter.CreateTextWriter(ms);
                    break;
                case WebContentFormat.Json:
                    writer = JsonReaderWriterFactory.CreateJsonWriter(ms);
                    break;
                case WebContentFormat.Raw:
                    // special case for raw, easier implemented separately
                    return ReadRawBody(ref message);
            }

            message.WriteMessage(writer);
            writer.Flush();
            string messageBody = Encoding.UTF8.GetString(ms.ToArray());

            // Here would be a good place to change the message body, if so desired.

            // now that the message was read, it needs to be recreated.
            ms.Position = 0;

            // if the message body was modified, needs to reencode it, as show below
            // ms = new MemoryStream(Encoding.UTF8.GetBytes(messageBody));

            XmlDictionaryReader reader;
            if (messageFormat == WebContentFormat.Json)
            {
                reader = JsonReaderWriterFactory.CreateJsonReader(ms, XmlDictionaryReaderQuotas.Max);
            }
            else
            {
                reader = XmlDictionaryReader.CreateTextReader(ms, XmlDictionaryReaderQuotas.Max);
            }

            Message newMessage = Message.CreateMessage(reader, int.MaxValue, message.Version);
            newMessage.Properties.CopyProperties(message.Properties);
            message = newMessage;

            return messageBody;
        }

        private static string ReadRawBody(ref Message message)
        {
            XmlDictionaryReader bodyReader = message.GetReaderAtBodyContents();
            bodyReader.ReadStartElement("Binary");
            byte[] bodyBytes = bodyReader.ReadContentAsBase64();
            string messageBody = Encoding.UTF8.GetString(bodyBytes);

            // Now to recreate the message
            MemoryStream ms = new MemoryStream();
            XmlDictionaryWriter writer = XmlDictionaryWriter.CreateBinaryWriter(ms);
            writer.WriteStartElement("Binary");
            writer.WriteBase64(bodyBytes, 0, bodyBytes.Length);
            writer.WriteEndElement();
            writer.Flush();
            ms.Position = 0;
            XmlDictionaryReader reader = XmlDictionaryReader.CreateBinaryReader(ms, XmlDictionaryReaderQuotas.Max);
            Message newMessage = Message.CreateMessage(reader, int.MaxValue, message.Version);
            newMessage.Properties.CopyProperties(message.Properties);
            message = newMessage;

            return messageBody;
        }
    }
}