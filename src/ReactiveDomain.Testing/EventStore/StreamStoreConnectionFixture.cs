using System;
using System.Diagnostics;
using System.Threading;
using ReactiveDomain.Util;
using ReactiveDomain.EventStore;
#if ! (NETCOREAPP2_0 || NETSTANDARD2_0)
using EventStore.ClientAPI.Embedded;
using EventStore.Core;
#endif

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing {

    public class StreamStoreConnectionFixture : IDisposable {
        private static readonly TimeSpan TimeToStop = TimeSpan.FromSeconds(5);

        private int _suffix;
        private int _prefix;
        private readonly IDisposable _node;

        public StreamStoreConnectionFixture()
        {
            AdminCredentials = new UserCredentials("admin", "changeit");
#if NETCOREAPP2_0 || NETSTANDARD2_0

            Connection = new ReactiveDomain.Testing.EventStore.MockStreamStoreConnection("Test Fixture");
#else
            var node = EmbeddedVNodeBuilder
                        .AsSingleNode()
                        .OnDefaultEndpoints()
                        .RunInMemory()
                        .DisableDnsDiscovery()
                        .DisableHTTPCaching()
                        //.DisableScavengeMerging()
                        .DoNotVerifyDbHashes()
                        .Build();

            node.StartAndWaitUntilReady().Wait();
            Connection = new EventStoreConnectionWrapper(EmbeddedEventStoreConnection.Create(node));

            _node = new Disposer(() => {
                if (!node.Stop(TimeToStop, true, true)) {
                    Trace.WriteLine(
                        $"Failed to gracefully shut down the embedded Eventstore within {TimeToStop.TotalMilliseconds}ms.");
                }
                return Unit.Default;
            });
#endif
        }

        public IStreamStoreConnection Connection { get; }

        public UserCredentials AdminCredentials { get; }

        public StreamName NextStreamName() {
            return new StreamName($"stream-{Interlocked.Increment(ref _suffix)}");
        }

        public string NextStreamNamePrefix() {
            return $"scenario-{Interlocked.Increment(ref _prefix):D}-";
        }

        private bool _disposed;
        public void Dispose() {
            if (_disposed) return;
            Connection?.Close();
            Connection?.Dispose();
            _node?.Dispose();
            _disposed = true;
        }
    }
}