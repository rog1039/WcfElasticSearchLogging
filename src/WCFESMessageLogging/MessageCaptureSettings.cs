using System;

namespace WCFESMessageLogging
{
    public class MessageCaptureSettings
    {
        public Uri ElasticSearchUri { get; set; }
        public string IndexName { get; set; }
        public string TypeName { get; set; }
        public int NumberOfMessageLoggingThreads { get; set; }
        public int MessageLoggingTimeoutInSeconds { get; set; }
        public bool EnableMessageLoggingTimout { get; set; }
        public TimeSpan HungMessageThreadCycleWaitTime { get; set; }
        public TimeSpan MaxHangoutTimeForMessage { get; set; }
    }
}