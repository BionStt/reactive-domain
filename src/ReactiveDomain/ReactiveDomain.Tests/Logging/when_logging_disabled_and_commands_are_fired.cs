﻿using System;
using Xunit;
using ReactiveDomain.Bus;
using ReactiveDomain.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Tests.Helpers;
using ReactiveDomain.Tests.Subscribers.QueuedSubscriber;
using Xunit.Sdk;

namespace ReactiveDomain.Tests.Logging
{

    // ReSharper disable once InconsistentNaming
    public class when_logging_disabled_and_commands_are_fired :
        with_message_logging_disabled,
        IHandle<Message>
    {
        static when_logging_disabled_and_commands_are_fired()
        {
            BootStrap.Load();
        }

        private readonly Guid _correlationId = Guid.NewGuid();
        private IListener _listener;

        private readonly int _maxCountedCommands = 25;
        private int _multiFireCount;
        private int _testCommandCount;

        private TestCommandSubscriber _cmdHandler;

        protected override void When()
        {
            // command must have a commandHandler
            _cmdHandler = new TestCommandSubscriber(Bus);

            _multiFireCount = 0;
            _testCommandCount = 0;

            _listener = Repo.GetListener(Logging.FullStreamName);
            _listener.EventStream.Subscribe<Message>(this);

            _listener.Start(Logging.FullStreamName);

            // create and fire a set of commands
            for (int i = 0; i < _maxCountedCommands; i++)
            {
                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.TestCommand2(
                                        Guid.NewGuid(),
                                        null);
                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));

            }
            var tstCmd = new TestCommands.TestCommand3(
                        Guid.NewGuid(),
                        null);

            Bus.Fire(tstCmd,
                "Test Command exception message",
                TimeSpan.FromSeconds(1));

        }

        [Fact(Skip = "pending deletion of log stream")]
        public void commands_are_not_logged()
        {
            TestQueue.WaitFor<TestCommands.TestCommand3>(TimeSpan.FromSeconds(5));
            // Wait  for last command to be queued

            //    // Wait  for last event to be queued
            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _multiFireCount > 0, 
                1000,
                $"Commands logged to ES when logging should be disabled - {_multiFireCount}"));

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _testCommandCount == 1,
                1000,
                $"Last command logged to ES when logging should be disabled"));

            Assert.NotEqual(_maxCountedCommands, _multiFireCount, $"Command count {_multiFireCount} doesn't match expected index {_maxCountedCommands}");
            Assert.NotEqual(1, _testCommandCount, $"Last event count {_testCommandCount} doesn't match expected value 1");

        }

        public void Handle(Message msg)
        {
            if (msg is TestCommands.TestCommand2) _multiFireCount++;
            if (msg is TestCommands.TestCommand3) _testCommandCount++;
        }
    }
}
