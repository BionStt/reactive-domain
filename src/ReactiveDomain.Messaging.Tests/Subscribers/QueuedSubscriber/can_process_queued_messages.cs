﻿using System;
using System.Threading;
using ReactiveDomain.Messaging.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber {
    // ReSharper disable InconsistentNaming
    public sealed class can_process_queued_messages : when_using_queued_subscriber {
        public can_process_queued_messages() {
            
        }
        [Fact]
        public void can_handle_two_messages() {
            var msg = new TestMessage();

            Bus.Publish(msg);
            Assert.IsOrBecomesTrue(
                () => MsgCount == 1,
                null,
                $"Expected 1 Message, found {MsgCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.TimesTestMessageHandled) == 1,
                1000,
                $"Expected 1 Message, found {MessageSubscriber.TimesTestMessageHandled}");
            var msg2 = new TestMessage();
            Bus.Publish(msg2);
            Assert.IsOrBecomesTrue(
                () => MsgCount == 2,
                1000,
                $"Expected 2 Messages on bus, found {MsgCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.TimesTestMessageHandled) == 2,
                1000,
                $"Expected 2 Messages handled, found {MessageSubscriber.TimesTestMessageHandled}");
        }
    }
}