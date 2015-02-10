using System;
using System.Configuration;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace WCFESMessageLogging
{
    public class MessageCaptureBehavior : IEndpointBehavior
    {
        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(
                new MessageCaptureBehaviorExtensionElement().CreateMessageCapture());
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
        }
        public void Validate(ServiceEndpoint endpoint)
        {
        }
    }
}