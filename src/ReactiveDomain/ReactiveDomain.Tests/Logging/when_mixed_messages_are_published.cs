﻿using System;
using ReactiveDomain.Bus;
using ReactiveDomain.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Tests.Helpers;
using ReactiveDomain.Tests.Subscribers.QueuedSubscriber;
using Xunit;

namespace ReactiveDomain.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    public class when_mixed_messages_are_published :
        with_message_logging_enabled,
        IHandle<Message>
    {
        private readonly Guid _correlationId = Guid.NewGuid();
        private IListener _listener;

        private readonly int _maxCountedMessages = 25;
        private int _multiFireCount;
        private int _testCommandCount;

        private readonly int _maxCountedEvents = 5;
        private int _countedEventCount;
        private int _testDomainEventCount;


        private TestCommandSubscriber _cmdHandler;
        protected override void When()
        {
            // commands must have a commandHandler
            _cmdHandler = new TestCommandSubscriber(Bus);

            _multiFireCount = 0;
            _testCommandCount = 0;

            _listener = Repo.GetListener(Logging.FullStreamName);
            _listener.EventStream.Subscribe<Message>(this);

            _listener.Start(Logging.FullStreamName);

            // create and fire a mixed set of commands and events
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.TestCommand2(
                                        Guid.NewGuid(),
                                        null);
                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));

                Bus.Publish(
                    new CountedEvent(i,
                        _correlationId,
                        Guid.NewGuid()));

            }

            for (int i = 0; i < _maxCountedEvents; i++)
            {
                Bus.Publish(new TestDomainEvent(_correlationId, Guid.NewGuid()));
            }

            var tstCmd = new TestCommands.TestCommand3(
                        Guid.NewGuid(),
                        null);

            Bus.Fire(tstCmd,
                "Test Command exception message",
                TimeSpan.FromSeconds(1));

        }


        [Fact(Skip = "pending deletion of log stream")]
        public void commands_are_logged()
        {

            TestQueue.WaitFor<TestDomainEvent>(TimeSpan.FromSeconds(5));
            TestQueue.WaitFor<TestCommands.TestCommand3>(TimeSpan.FromSeconds(5));

            // Wait  for last event to be queued
            Assert.IsOrBecomesTrue(() => _countedEventCount == _maxCountedMessages, 2000);
            Assert.Equal(_maxCountedMessages, _countedEventCount, $"Message {_countedEventCount} doesn't match expected index {_maxCountedMessages}");
            Assert.IsOrBecomesTrue(() => _testDomainEventCount == _maxCountedEvents, 
                1000,
                $"Last event count {_testDomainEventCount} doesn't match expected value {_maxCountedEvents}");

            Assert.Equal(_maxCountedEvents, _testDomainEventCount, $"Last event count {_testDomainEventCount} doesn't match expected value {_maxCountedEvents}");


            // Wait  for last TestCommand2 to be "heard" from logger/repo
            Assert.IsOrBecomesTrue(() => _multiFireCount == _maxCountedMessages, 
                3000,
                 $"Command count {_multiFireCount} doesn't match expected index {_maxCountedMessages}");

            Assert.Equal(_maxCountedMessages, _multiFireCount, $"Command count {_multiFireCount} doesn't match expected index {_maxCountedMessages}");
            
            // Wait  for last command to be "heard" from logger/repo
            Assert.IsOrBecomesTrue(() => _testCommandCount == 1, 1000);
            Assert.Equal(1, _testCommandCount, $"Last command count {_testCommandCount} doesn't match expected value 1");
        }

        public void Handle(Message msg)
        {
            if (msg is TestCommands.TestCommand2) _multiFireCount++;
            if (msg is TestCommands.TestCommand3) _testCommandCount++;
            if (msg is CountedEvent) _countedEventCount++;
            if (msg is TestDomainEvent) _testDomainEventCount++;
        }
    }
}