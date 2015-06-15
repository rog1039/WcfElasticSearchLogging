using System;
using System.Collections.Generic;

namespace WCFESMessageLogging
{
    public class MessageLogEntry
    {
        public MessageLogEntry(string requestUri)
        {
            MessageId = Guid.NewGuid();
            RequestUri = requestUri;
            Headers = new Dictionary<string, string>();
        }

        public DateTime StartTime { get; private set; }
        public DateTime? EndTime { get; private set; }
        public Guid MessageId { get; }
        public string RequestUri { get; }
        public string RemoteEndpointAddress { get; set; }
        public int RemoteEndpointPort { get; set; }
        public string HttpMethod { get; set; }
        public string RequestBody { get; set; }
        public long RequestSize { get; set; }
        public long ResponseSize { get; set; }
        public string OperationName { get; set; }
        public string HttpResponseStatusCode { get; set; }
        public int HttpResponseStatusCodeInt { get; set; }
        public TimeSpan? OperationDuration => EndTime - StartTime;
        public double? DurationInMilliseconds => OperationDuration?.TotalMilliseconds;
        public Dictionary<string, string> Headers { get; }
        public DateTime @timestamp => StartTime;
        public bool MessageHung { get; private set; }


        public void MarkOperationAsStarted() => StartTime = DateTime.UtcNow;
        public void MarkOperationAsFinished() => EndTime = DateTime.UtcNow;
        public void MarkOperationAsHung()
        {
            MessageHung = true;
            EndTime = DateTime.UtcNow;
        }

    }
}