﻿using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using ReactiveDomain.Testing;
using System;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.Logging
{

    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_commands_are_fired :
        with_message_logging_enabled,
        IHandle<Message>
    {
        private const int MaxCountedCommands = 25;

        private Guid _correlationId;
        private IListener _listener;

        private int _multiFireCount;
        private int _testCommandCount;

        private TestCommandSubscriber _cmdHandler;


        static when_commands_are_fired()
        {
            Messaging.BootStrap.Load();
        }

        public when_commands_are_fired(StreamStoreConnectionFixture fixture):base(fixture.Connection)
        {
            _correlationId = Guid.NewGuid();

            // command must have a commandHandler
            _cmdHandler = new TestCommandSubscriber(Bus);

            _multiFireCount = 0;
            _testCommandCount = 0;

            _listener = new SynchronizableStreamListener(Logging.FullStreamName, Connection, StreamNameBuilder);
            _listener.EventStream.Subscribe<Message>(this);

            _listener.Start(Logging.FullStreamName);

            // create and fire a set of commands
            for (int i = 0; i < MaxCountedCommands; i++)
            {
                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.Command2(
                    Guid.NewGuid(),
                    null);
                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));

            }
            var tstCmd = new TestCommands.Command3(
                Guid.NewGuid(),
                null);

            Bus.Fire(tstCmd,
                "Test Command exception message",
                TimeSpan.FromSeconds(1));
        }
        

        [Fact(Skip = "Mock store not implemented")]
        private void all_commands_are_logged()
        {
            // Wait  for last command to be queued
            Assert.IsOrBecomesTrue(()=> _cmdHandler.TestCommand3Handled >0);
            
            Assert.IsOrBecomesTrue(() => _multiFireCount == MaxCountedCommands, 9000);
            Assert.True(_multiFireCount == MaxCountedCommands, $"Command count {_multiFireCount} doesn't match expected index {MaxCountedCommands}");
            Assert.IsOrBecomesTrue(() => _testCommandCount == 1, 1000);

            Assert.True(_testCommandCount == 1, $"Last event count {_testCommandCount} doesn't match expected value {1}");

        }

        public void Handle(Message msg)
        {
            if (msg is TestCommands.Command2) _multiFireCount++;
            if (msg is TestCommands.Command3) _testCommandCount++;
        }
    }
}

