using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NeoSharp.Core.ExtensionMethods;
using NeoSharp.Core.Helpers;
using NeoSharp.Core.Logging;
using NeoSharp.Core.Messaging;
using NeoSharp.Core.Messaging.Messages;

namespace NeoSharp.Core.Network
{
    public class Server : IServer, IDisposable
    {
        #region Constants

        private static readonly TimeSpan DefaultMessagePollingInterval = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan DefaultReceiveTimeout = TimeSpan.FromMilliseconds(1_000);

        #endregion

        #region Properties

        private readonly ILogger<Server> _logger;
        private readonly IAsyncDelayer _asyncDelayer;
        private readonly IServerContext _serverContext;
        private readonly IPeerFactory _peerFactory;
        private readonly IPeerListener _peerListener;
        private readonly IMessageHandler<Message> _messageHandler;

        // if we successfully connect with a peer it is inserted into this list
        private readonly ConcurrentBag<IPeer> _connectedPeers;

        // if we can't connect to a peer it is inserted into this list
        // ReSharper disable once NotAccessedField.Local
        private readonly IList<IPEndPoint> _failedPeers;
        private readonly EndPoint[] _peerEndPoints;
        private CancellationTokenSource _messageListenerTokenSource;

        #endregion

        #region Properties

        public IReadOnlyCollection<IPeer> ConnectedPeers => _connectedPeers; // TODO: thread safe?

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">Network config</param>
        /// <param name="peerFactory">PeerFactory</param>
        /// <param name="peerListener">PeerListener</param>
        /// <param name="messageHandler">Mesage Handler</param>
        /// <param name="logger">Logger</param>
        /// <param name="asyncDelayer">Async delayer</param>
        /// <param name="serverContext">Server context</param>
        public Server(
            NetworkConfig config,
            IPeerFactory peerFactory,
            IPeerListener peerListener,
            IMessageHandler<Message> messageHandler,
            ILogger<Server> logger,
            IAsyncDelayer asyncDelayer,
            IServerContext serverContext)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _peerFactory = peerFactory ?? throw new ArgumentNullException(nameof(peerFactory));
            _peerListener = peerListener ?? throw new ArgumentNullException(nameof(peerListener));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _asyncDelayer = asyncDelayer ?? throw new ArgumentNullException(nameof(asyncDelayer));
            _serverContext = serverContext ?? throw new ArgumentNullException(nameof(serverContext));

            _messageHandler = messageHandler;

            _peerListener.OnPeerConnected += PeerConnected;

            _connectedPeers = new ConcurrentBag<IPeer>();
            _failedPeers = new List<IPEndPoint>();

            // TODO: Change after port forwarding implementation
            _peerEndPoints = config.PeerEndPoints;
        }

        /// <summary>
        /// Start server
        /// </summary>
        public void Start()
        {
            Stop();

            _messageListenerTokenSource = new CancellationTokenSource(DefaultReceiveTimeout);

            // connect to peers
            ConnectToPeers();

            // listen for peers
            _peerListener.Start();
        }

        /// <summary>
        /// Stop server
        /// </summary>
        public void Stop()
        {
            _peerListener.Stop();

            DisconnectPeers();

            _messageListenerTokenSource?.Cancel();
        }

        /// <summary>
        /// Free resources
        /// </summary>
        public void Dispose()
        {
            Stop();
            _peerListener.OnPeerConnected -= PeerConnected;
        }

        /// <summary>
        /// Peer connected Event
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="peer">Peer</param>
        private void PeerConnected(object sender, IPeer peer)
        {
            try
            {
                _connectedPeers.Add(peer);

                ListenForMessages(peer, _messageListenerTokenSource.Token);

                // Initiate handshake
                peer.Send(new VersionMessage(_serverContext.Version));
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Something went wrong with {peer}. Exception: {e}");
                peer.Disconnect();
            }
        }

        /// <summary>
        /// Broadcast a message
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="filter">Filter</param>
        public async Task SendBroadcast(Message message, Func<IPeer, bool> filter = null)
        {
            Parallel.ForEach(_connectedPeers, async (peer) =>
            {
                // Check filter

                if (filter != null && !filter(peer)) return;

                // Send

                await peer.Send(message);
            });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Connect to peers
        /// </summary>
        private void ConnectToPeers()
        {
            Parallel.ForEach(_peerEndPoints, async ep =>
            {
                try
                {
                    var peer = await _peerFactory.ConnectTo(ep);
                    PeerConnected(this, peer);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Something went wrong with {ep}. Exception: {ex}");
                }
            });
        }

        /// <summary>
        /// Send disconnect to all current Peers
        /// </summary>
        private void DisconnectPeers()
        {
            foreach (var peer in _connectedPeers)
            {
                peer.Disconnect();
            }

            _connectedPeers.Clear();
        }

        /// <summary>
        /// Listen messages
        /// </summary>
        /// <param name="peer">Peer</param>
        /// <param name="cancellationToken">Cancellation token</param>
        private void ListenForMessages(IPeer peer, CancellationToken cancellationToken)
        {
            Task.Factory.StartNew(async () =>
            {
                while (peer.IsConnected)
                {
                    var message = await peer.Receive();
                    if (message == null)
                    {
                        await _asyncDelayer.Delay(DefaultMessagePollingInterval, cancellationToken);
                        continue;
                    }

                    // TODO: Peer that sending wrong messages has to be disconnected.
                    if (peer.IsReady == message.IsHandshakeMessage()) continue;

                    await _messageHandler.Handle(message, peer);
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}