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

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public Guid MessageId { get; set; }
        public string RequestUri { get; set; }
        public string HttpMethod { get; set; }
        public string RequestBody { get; set; }
        public long RequestSize { get; set; }
        public long ResponseSize { get; set; }
        public string OperationName { get; set; }
        public string HttpResponseStatusCode { get; set; }
        public int HttpResponseStatusCodeInt { get; set; }
        public TimeSpan? OperationDuration => GetOperationDuration();
        public double? DurationInMilliseconds => GetOperationDuration()?.TotalMilliseconds;
        public Dictionary<string, string> Headers { get; set; }
        public DateTime @timestamp => StartTime;

        public void MarkOperationAsStarted() => StartTime = DateTime.Now;

        public void MarkOperationAsFinished() => EndTime = DateTime.Now;

        private TimeSpan? GetOperationDuration() => EndTime - StartTime;
    }
}