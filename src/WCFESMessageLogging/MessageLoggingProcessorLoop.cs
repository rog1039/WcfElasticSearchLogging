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
        private readonly ConcurrentDictionary<Guid, MessageLogEntry> _currrentMessageLogEntries = new ConcurrentDictionary<Guid, MessageLogEntry>();


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

        public void AddMessageLogEntry(MessageLogEntry messageLogEntry)
        {
            if (_incomingItemQueue.Count > 10000)
                return;

            _incomingItemQueue.TryAdd(messageLogEntry);
        }

        private void NormalMessageLogEntryProcessingLoop()
        {
            while (true)
            {
                try
                {
                    var messageLogEntry = _incomingItemQueue.Take();

                    if (messageLogEntry.EndTime.HasValue)
                    {
                        ProcessEndingMessage(messageLogEntry);
                    }
                    else
                    {
                        _currrentMessageLogEntries.TryAdd(messageLogEntry.MessageId, messageLogEntry);
                    }
                }
                catch (Exception e)
                {
                    // ignored
                }
            }
        }

        private void ProcessEndingMessage(MessageLogEntry item)
        {
            try
            {
                IndexEndingMessage(item);
                _currrentMessageLogEntries.Remove(item.MessageId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _incomingItemQueue.Add(item);
            }
        }

        private void IndexEndingMessage(MessageLogEntry messageLogEntry)
        {
            var indexResponse = _elasticClient.Index(messageLogEntry, i => i.Index(_settings.IndexName).Type(_settings.TypeName).Id(messageLogEntry.MessageId.ToString()));
        }
        private void IndexHungMessage(MessageLogEntry messageLogEntry)
        {
            var indexResponse = _elasticClient.Index(messageLogEntry, i => i.Index(_settings.IndexName).Type(_settings.TypeName).Id(messageLogEntry.MessageId.ToString()));
        }

        private void HungMessageLogEntryProcessingLoop()
        {
            while (true)
            {
                try
                {
                    _pollingEvent.WaitOne(_settings.HungMessageThreadCycleWaitTime);

                    foreach (var hungMessageLogEntry in _currrentMessageLogEntries.Values.Where(HasMessageTakenTooLong))
                    {
                        try
                        {
                            _currrentMessageLogEntries.Remove(hungMessageLogEntry.MessageId);
                            hungMessageLogEntry.MarkOperationAsHung();
                            IndexHungMessage(hungMessageLogEntry);
                        }
                        catch (Exception e)
                        {
                            // ignored
                        }
                    }
                }
                catch (Exception e)
                {
                    // ignored
                }
            }
        }


        private bool HasMessageTakenTooLong(MessageLogEntry messageLogEntry)
            => messageLogEntry.StartTime + _settings.MaxHangoutTimeForMessage < DateTime.UtcNow;
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
