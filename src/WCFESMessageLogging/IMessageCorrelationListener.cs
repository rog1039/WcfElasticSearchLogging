namespace WCFESMessageLogging
{
    public interface IMessageCorrelationListener
    {
        void RequestStarted(MessageLogEntry messageLogEntry);
        void RequestCompleted(MessageLogEntry messageLogEntry);
    }
}