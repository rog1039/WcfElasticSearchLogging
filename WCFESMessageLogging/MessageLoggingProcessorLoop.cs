using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nest;
using Seterlund.CodeGuard;

namespace WCFESMessageLogging
{
    public class MessageLoggingProcessorLoop
    {
        private readonly MessageCaptureSettings _settings;
        private readonly ElasticClient _elasticClient;

        private readonly Queue<MessageLogEntry> _incomingItemQueue = new Queue<MessageLogEntry>();
        private readonly Dictionary<Guid, MessageLogEntry> _currentRunningRequests = new Dictionary<Guid, MessageLogEntry>();


        private readonly List<Thread> _threads;
        private readonly Semaphore _workWaiting = new Semaphore(0, int.MaxValue);
        private readonly ManualResetEvent _pollingEvent = new ManualResetEvent(false);
        
        public MessageLoggingProcessorLoop(MessageCaptureSettings settings)
        {
            Guard.That(() => settings.NumberOfMessageLoggingThreads).IsGreaterThan(0);

            _settings = settings;

            _elasticClient = new ElasticClient(new ConnectionSettings(
                _settings.ElasticSearchUri,
                _settings.IndexName));

            _threads = new List<Thread>(_settings.NumberOfMessageLoggingThreads);
            
            StartProcessingLoopThreads();
            StartHungMessageThread();
        }
        
        private void StartProcessingLoopThreads()
        {
            for (int i = 0; i < _settings.NumberOfMessageLoggingThreads; i++)
            {
                Thread t = new Thread(NormalMessageLogEntryProcessingLoop);
                t.Name = "MessageLoggingProcessorLoop Thread " + i.ToString();
                t.IsBackground = true;
                _threads.Add(t);
                t.Start();
            }
        }
        private void StartHungMessageThread()
        {
            if (_settings.EnableMessageLoggingTimout)
            {
                Thread t = new Thread(HungMessageLogEntryProcessingLoop);
                t.IsBackground = true;
                _threads.Add(t);
                t.Start();
            }
        }

        public void EnqueueIncomingItem(MessageLogEntry messageLogEntry)
        {
            lock (_incomingItemQueue)
                _incomingItemQueue.Enqueue(messageLogEntry);

            _workWaiting.Release();
        }
        
        private void NormalMessageLogEntryProcessingLoop()
        {
            try
            {
                while (true)
                {
                    var item = DequeueIncomingItem();

                    if (item.EndTime.HasValue)
                    {
                        IndexEndingMessage(item);

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

        private void IndexEndingMessage(MessageLogEntry messageLogEntry)
        {
            var indexResponse = _elasticClient.Index(messageLogEntry, i => i.Index("my-index").Type("my-type").Id(messageLogEntry.MessageId.ToString()));
        }
        private void IndexHungMessage(MessageLogEntry messageLogEntry)
        {
            var indexResponse = _elasticClient.Index(messageLogEntry, i => i.Index("my-index").Type("my-type").Id(messageLogEntry.MessageId.ToString()));
        }
        
        private void HungMessageLogEntryProcessingLoop()
        {
            MessageLogEntry hungMessage = null;
            try
            {
                while (true)
                {
                    _pollingEvent.WaitOne(_settings.HungMessageThreadCycleWaitTime);
                    lock (_currentRunningRequests)
                    {
                        foreach (var correlationMessage in _currentRunningRequests.Values)
                        {
                            if (correlationMessage.StartTime + _settings.MaxHangoutTimeForMessage < DateTime.UtcNow)
                            {
                                hungMessage = correlationMessage;
                                break;
                            }
                        }
                    }
                    if (hungMessage != null)
                    {
                        hungMessage.MarkOperationAsHung();
                        IndexHungMessage(hungMessage);
                        lock (_currentRunningRequests)
                        {
                            _currentRunningRequests.Remove(hungMessage.MessageId);
                        }
                        hungMessage = null;
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
