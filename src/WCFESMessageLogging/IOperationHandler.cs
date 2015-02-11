namespace WCFESMessageLogging
{
    public interface IOperationHandler
    {
        void ActionTaken();
        void ActionCompleted();
        void ActionHung();
    }
}