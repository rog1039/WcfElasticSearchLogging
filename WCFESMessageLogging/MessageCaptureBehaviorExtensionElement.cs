﻿using System;
using System.Configuration;
using System.ServiceModel.Configuration;

namespace WCFESMessageLogging
{
    public class MessageCaptureBehaviorExtensionElement : BehaviorExtensionElement
    {
        public MessageCapture CreateMessageCapture() => new MessageCapture(GetMessageCaptureSettings());


        protected override object CreateBehavior() => new MessageCaptureBehavior();

        public override Type BehaviorType
        {
            get { return BehaviorType2(); }
        }

        public Type BehaviorType2()
        {
            var asd = this["hungMessageThreadCycleWaitTime"];
            var asdfas = (string)this["elasticSearchIndexName"];;
            var tasdf = HungMessageThreadCycleWaitTime;
            return typeof (MessageCaptureBehavior);
        }


        private MessageCaptureSettings GetMessageCaptureSettings()
        {
            MessageCaptureSettings settings = new MessageCaptureSettings()
            {
                ElasticSearchUri = new Uri(ElasticSearchNodeAddress),
                IndexName = ElasticSearchIndexName,
                TypeName = ElasticSearchDocumentType,
                NumberOfMessageLoggingThreads = NumberOfMessageLoggingThreads,
                EnableMessageLoggingTimout = EnableMessageLoggingTimout,
                MessageLoggingTimeoutInSeconds = MessageLoggingTimeoutInSeconds,
                HungMessageThreadCycleWaitTime = HungMessageThreadCycleWaitTime,
                MaxHangoutTimeForMessage = MaxHangoutTimeForMessage
            };
            
            return settings;
        }


        [ConfigurationProperty("elasticSearchNodeAddress", DefaultValue = "http://ws2012r2kibana4:9200")]
        public string ElasticSearchNodeAddress => (string)this["elasticSearchNodeAddress"];

        [ConfigurationProperty("elasticSearchIndexName", DefaultValue = "another-index")]
        public string ElasticSearchIndexName => (string)this["elasticSearchIndexName"];

        [ConfigurationProperty("elasticSearchDocumentType", DefaultValue = "my-type")]
        public string ElasticSearchDocumentType => (string)this["elasticSearchDocumentType"];

        [ConfigurationProperty("numberOfMessageLoggingThreads", DefaultValue = 1)]
        public int NumberOfMessageLoggingThreads => (int)this["numberOfMessageLoggingThreads"];

        [ConfigurationProperty("messageLoggingTimeoutInSeconds", DefaultValue = 30)]
        public int MessageLoggingTimeoutInSeconds => (int)this["messageLoggingTimeoutInSeconds"];

        [ConfigurationProperty("enableMessageLoggingTimout", DefaultValue = true)]
        public bool EnableMessageLoggingTimout => (bool)this["enableMessageLoggingTimout"];

        [ConfigurationProperty("hungMessageThreadCycleWaitTime")]
        public TimeSpan HungMessageThreadCycleWaitTime => (TimeSpan?)this["hungMessageThreadCycleWaitTime"] ?? defaultHungMessageThreadCycleWaitTime;

        [ConfigurationProperty("maxHangoutTimeForMessage")]
        public TimeSpan MaxHangoutTimeForMessage => (TimeSpan?)this["maxHangoutTimeForMessage"] ?? defaultMaxHangoutTimeForMessage;

        private static readonly TimeSpan defaultHungMessageThreadCycleWaitTime = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan defaultMaxHangoutTimeForMessage = TimeSpan.FromMinutes(5);
    }
}