using Onx100.Driver.Exceptions;
using Onx100.Driver.Protocol;
using Onx100.Driver.Transport;
using System;
using System.Text;

namespace Onx100.Driver.Commands
{
    internal class Onx100CommandManager
    {
        /********* PRIVATE MEMEBERS ***********/
        private readonly IOnx100Transport transport;
        private readonly TimeSpan defaultTimeout;
        private readonly SemaphoreSlim executionLock = new SemaphoreSlim(1, 1);
        private readonly object pendingLock = new();
        private Onx100PendingCommand? pendingCommand;        
        private bool sessionInvalidated;


        /********* CONSTRUCTOR ***********/
        public Onx100CommandManager(IOnx100Transport transport, TimeSpan defaultTimeout)
        {
            ArgumentNullException.ThrowIfNull(transport);

            if (defaultTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(defaultTimeout));

            this.transport = transport;
            this.defaultTimeout = defaultTimeout;
        }


        /********* PUBLIC METHODS ***********/
        public async Task<Onx100InboundMessage> ExecuteAsync(string command, Onx100MessageKind expectedResponseKind, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(command);

            TimeSpan effectiveTimeout = timeout ?? defaultTimeout;

            if (effectiveTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            await executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (IsSessionInvalidated())
                    throw new InvalidOperationException("The protocol session is desynchronized. Reconnect before sending another command.");

                Onx100PendingCommand pendingCommand = new Onx100PendingCommand(command, expectedResponseKind);

                SetPendingCommand(pendingCommand);

                try
                {
                    byte[] data = Encoding.ASCII.GetBytes(command);
                    bool sendAttempted = false;

                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        sendAttempted = true;

                        await transport.SendAsync(data, cancellationToken).ConfigureAwait(false);

                        Onx100InboundMessage response;

                        try
                        {
                            response = await pendingCommand.ResponseTask.WaitAsync(effectiveTimeout, cancellationToken).ConfigureAwait(false);
                        }
                        catch (TimeoutException)
                        {
                            InvalidateSession();
                            throw new Onx100TimeoutException(NormalizeCommand(command), effectiveTimeout);
                        }

                        if (response.Kind == Onx100MessageKind.ErrorResponse)
                        {
                            int errorCode = response.ErrorCode ?? throw new InvalidOperationException("Protocol error response has no error code.");

                            throw new Onx100CommandException(NormalizeCommand(command), errorCode);
                        }

                        return response;
                    }
                    catch (OperationCanceledException) when (sendAttempted)
                    {
                        InvalidateSession();
                        throw;
                    }
                }
                finally
                {
                    ClearPendingCommand(pendingCommand);
                }
            }
            finally
            {
                executionLock.Release();
            }
        }

        public bool TryHandleMessage(Onx100InboundMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            Onx100PendingCommand? command;

            lock (pendingLock)            
                command = pendingCommand;
            

            return command?.TrySetResponse(message) ?? false;
        }

        public bool TryFailPendingCommand(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            Onx100PendingCommand? command;

            lock (pendingLock)            
                command = pendingCommand;            

            return command?.TrySetException(exception) ?? false;
        }

        public async Task ResetSessionAsync()
        {
            await executionLock.WaitAsync().ConfigureAwait(false);

            try
            {
                lock (pendingLock)
                {
                    sessionInvalidated = false;
                }
            }
            finally
            {
                executionLock.Release();
            }
        }

        public bool IsSessionInvalidated()
        {
            lock (pendingLock)
            {
                return sessionInvalidated;
            }
        }

        /********* PRIVATE METHODS ***********/
        private void SetPendingCommand(Onx100PendingCommand command)
        {
            lock (pendingLock)
            {
                if (pendingCommand is not null)                
                    throw new InvalidOperationException("Another command is already awaiting a response.");                

                pendingCommand = command;
            }
        }

        private void ClearPendingCommand(Onx100PendingCommand command)
        {
            lock (pendingLock)
            {
                if (ReferenceEquals(pendingCommand, command))                
                    pendingCommand = null;
                
            }
        }

        private static string NormalizeCommand(string command)
        {
            return command.TrimEnd('\r');
        }

        private void InvalidateSession()
        {
            lock (pendingLock)
            {
                sessionInvalidated = true;
            }
        }

    }
}
