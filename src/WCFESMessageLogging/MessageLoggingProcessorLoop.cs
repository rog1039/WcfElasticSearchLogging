using System;
using System.Collections.Concurrent;
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

        private readonly BlockingCollection<MessageLogEntry> _incomingItemQueue = new BlockingCollection<MessageLogEntry>();
        private readonly ConcurrentDictionary<Guid, MessageLogEntry> _currentRunningRequests = new ConcurrentDictionary<Guid, MessageLogEntry>();


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
            if (_incomingItemQueue.Count > 10000)
                return;

            _incomingItemQueue.TryAdd(messageLogEntry);
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
                        _currentRunningRequests.Remove(item.MessageId);
                    }
                    else
                    {
                        _currentRunningRequests.TryAdd(item.MessageId, item);
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
            var messageLogEntry = _incomingItemQueue.Take();
            return messageLogEntry;
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
    public static class ConcurrentDictionaryEx
    {
        public static bool Remove<TKey, TValue>(
          this ConcurrentDictionary<TKey, TValue> self, TKey key)
        {
            TValue ignored;
            return self.TryRemove(key, out ignored);
        }
    }
}
