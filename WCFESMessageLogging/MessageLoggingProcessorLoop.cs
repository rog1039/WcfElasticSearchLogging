using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nest;

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
    }

    public class MessageLoggingProcessorLoop
    {
        Uri elasticSearchNode = new Uri("http://ws2012r2kibana4:9200");
        private IOperationHandler _operationHandler;
        private readonly Dictionary<Guid, MessageLogEntry> _currentRunningRequests = new Dictionary<Guid, MessageLogEntry>();

        private Semaphore _workWaiting;
        private ManualResetEvent _pollingEvent;
        private Queue<MessageLogEntry> _incomingItemQueue;
        private List<Thread> _threads;

        private long _dumpstartTicks;
        private string _dumpIncomingMessage;
        private int _dumpCount;
        private TimeSpan hungMessageThreadCycleWaitTime = TimeSpan.FromSeconds(30);
        private TimeSpan maxHangoutTimeForMessage = TimeSpan.FromMinutes(5);
        private MessageLogEntry _hungMessage;
        private ElasticClient elasticClient;

        public MessageLoggingProcessorLoop(int numThreads)
        {
            var settings = new ConnectionSettings(
                elasticSearchNode,
                defaultIndex: "another-index"
                );
            elasticClient = new ElasticClient(settings);

            if (numThreads <= 0)
                throw new ArgumentOutOfRangeException("numThreads");

            _threads = new List<Thread>(numThreads);
            _incomingItemQueue = new Queue<MessageLogEntry>();
            _workWaiting = new Semaphore(0, int.MaxValue);

            _dumpCount = 0;

            for (int i = 0; i < numThreads; i++)
            {
                Thread t = new Thread(Run);
                t.Name = "RunMethod Thread";
                t.IsBackground = true;
                _threads.Add(t);
                t.Start();
            }
        }

        public void StartHangDumpThread()
        {
            _pollingEvent = new ManualResetEvent(false);

            Thread t = new Thread(HangDumpThread);
            t.IsBackground = true;
            _threads.Add(t);
            t.Start();
        }


        public void EnqueueIncomingItem(MessageLogEntry messageLogEntry)
        {
            lock (_incomingItemQueue)
                _incomingItemQueue.Enqueue(messageLogEntry);

            _workWaiting.Release();
        }

        private MessageLogEntry DequeueIncomingItem()
        {
            MessageLogEntry item = null;

            while (item == null)
            {
                lock (_incomingItemQueue)
                {
                    if (_incomingItemQueue.Count > 0)
                    {
                        item = _incomingItemQueue.Dequeue();
                        break;
                    }
                }
                _workWaiting.WaitOne();
            }
            return item;
        }

        private void Run()
        {
            try
            {
                while (true)
                {
                    var item = DequeueIncomingItem();

                    if (item.EndTime.HasValue)
                    {
                        ProcessEndingMessage(item);

                        lock (_currentRunningRequests)
                        {
                            _currentRunningRequests.Remove(item.MessageId);
                        }
                    }
                    else
                    {
                        lock (_currentRunningRequests)
                        {
                            _currentRunningRequests.Add(item.MessageId, item);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // ignored
            }
        }

        private void ProcessEndingMessage(MessageLogEntry item)
        {
            //Send Message to ElasticSearch.
            var indexResponse = elasticClient.Index(item, i => i.Index("my-index").Type("my-type").Id(item.MessageId.ToString()));
        }

        private void ProcessHungMessage(MessageLogEntry hungMessage)
        {
            //Send message to ElasticSearch.
            elasticClient.Index(hungMessage);
        }


        private void HangDumpThread()
        {
            try
            {
                while (true)
                {
                    _pollingEvent.WaitOne(hungMessageThreadCycleWaitTime);
                    lock (_currentRunningRequests)
                    {
                        foreach (var correlationMessage in _currentRunningRequests.Values)
                        {
                            if (correlationMessage.StartTime + maxHangoutTimeForMessage < DateTime.Now)
                            {
                                _hungMessage = correlationMessage;
                                break;
                            }
                        }
                    }
                    if (_hungMessage != null)
                    {
                        ProcessHungMessage(_hungMessage);
                        lock (_currentRunningRequests)
                        {
                            _currentRunningRequests.Remove(_hungMessage.MessageId);
                        }
                        _hungMessage = null;
                    }
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}
