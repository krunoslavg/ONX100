using Onx100.Driver.Protocol;

namespace Onx100.Driver.Commands
{
    internal sealed class Onx100PendingCommand
    {
        /************* PRIVATE MEMBERS **************/
        private readonly TaskCompletionSource<Onx100InboundMessage> completionSource = new TaskCompletionSource<Onx100InboundMessage> ();

        
        /************* PUBLIC PROPERTIES **************/
        public string Command { get; }
        public Onx100MessageKind ExpectedResponseKind { get; }
        public Task<Onx100InboundMessage> ResponseTask => completionSource.Task;


        /************* CONSTRUCTOR **************/
        public Onx100PendingCommand(string command, Onx100MessageKind expectedMessageKind)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(command);

            if (!IsCommandResponseKind(expectedMessageKind)) { 
                throw new ArgumentOutOfRangeException(nameof(expectedMessageKind), expectedMessageKind, "Expected message kind must be a command response.");
            }

            Command = command;
            ExpectedResponseKind = expectedMessageKind;
        }


        /************* PUBLIC METHODS **************/
        public bool CanAccept(Onx100InboundMessage message)
        { 
            ArgumentNullException.ThrowIfNull(message);
            return message.Kind == ExpectedResponseKind || message.Kind == Onx100MessageKind.ErrorResponse;
        }

        public bool TrySetResponse(Onx100InboundMessage message) 
        { 
            ArgumentNullException.ThrowIfNull(message);

            return CanAccept(message) && completionSource.TrySetResult(message);
        }

        public bool TrySetException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return completionSource.TrySetException(exception);
        }

        public bool TrySetCommand(Exception exception) 
        {
            ArgumentNullException.ThrowIfNull(exception);
            return completionSource.TrySetException(exception);
        }

        public bool TrySetCanceled(CancellationToken cancellationToken = default)
        { 
            return completionSource.TrySetCanceled(cancellationToken);
        }


        /************* PRIVATE METHODS **************/
        private static bool IsCommandResponseKind(Onx100MessageKind kind) {
            return kind is Onx100MessageKind.OkResponse or Onx100MessageKind.PowerResponse or Onx100MessageKind.InputResponse or Onx100MessageKind.VolumeResponse or Onx100MessageKind.MuteResponse;
        }
    }
}
