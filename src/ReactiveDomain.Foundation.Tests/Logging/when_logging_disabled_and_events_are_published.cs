﻿using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using ReactiveDomain.Testing;
using System;
using System.Threading;
using Xunit;
using Xunit.Sdk;

namespace ReactiveDomain.Foundation.Tests.Logging
{

    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_logging_disabled_and_events_are_published :
        with_message_logging_disabled,
        IHandle<DomainEvent>
    {
        static when_logging_disabled_and_events_are_published()
        {
            BootStrap.Load();
        }

       
        private readonly Guid _correlationId = Guid.NewGuid();
        private IListener _listener;

        private readonly int _maxCountedEvents = 5;
        private int _countedEventCount;
        private int _testDomainEventCount;
        private long _gotEvt;
        public when_logging_disabled_and_events_are_published(StreamStoreConnectionFixture fixture):base(fixture.Connection)
        {
       
            _listener = new SynchronizableStreamListener(Logging.FullStreamName, Connection, StreamNameBuilder);
            _listener.EventStream.Subscribe<DomainEvent>(this);

            _listener.Start(Logging.FullStreamName);

            _countedEventCount = 0;
            _testDomainEventCount = 0;

            // create and publish a set of events
            for (int i = 0; i < _maxCountedEvents; i++)
            {
                Bus.Publish(
                    new CountedEvent(i,
                        _correlationId,
                        Guid.NewGuid()));
            }
            Bus.Subscribe(new AdHocHandler<TestEvent>(_ => Interlocked.Increment(ref _gotEvt)));
            Bus.Publish(new TestEvent(_correlationId, Guid.NewGuid()));
        }



        public void events_are_not_logged()
        {
            // wait for all events to be queued
            Assert.IsOrBecomesTrue(()=> _gotEvt >0);

            //// Need the "is or becomes" here because if the handler (see below) is executed, it takes time. 
            // see the enabled test

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                ()=>_countedEventCount > 0, 
                1000,
                $"Found {_countedEventCount} CountedEvents on log"));
            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _testDomainEventCount > 0, 
                1000,
                $"Found {_testDomainEventCount} TestDomainEvents on log"));

            // counters are never incremented because they are not logged, therefore not "heard" by the repo listener
            Assert.False(_countedEventCount == _maxCountedEvents, $"{_countedEventCount} CountedEvents found on Log");
            Assert.True(_testDomainEventCount == 0, $"Last event count {_testDomainEventCount} is not 0");
        }

        public void Handle(DomainEvent message)
        {
            if (message is CountedEvent) _countedEventCount++;
            if (message is TestEvent) _testDomainEventCount++;
        }
    }
}
