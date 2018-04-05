﻿using System;
using System.Threading;
using ReactiveDomain.Messaging.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    public class can_handle_inherited_messages : when_using_inherited_queued_subscriber
    {
        private CorrelationId _testCorrelationId;
        private TestEvent _testEvent;
        private ParentTestEvent _parentTestEvent;
        private ChildTestEvent _childTestEvent;

        protected override void When()
        {
            _testCorrelationId = CorrelationId.NewId();
            _testEvent = new TestEvent(_testCorrelationId, SourceId.NullSourceId());
            Bus.Publish(_testEvent);

            _parentTestEvent = new ParentTestEvent(_testCorrelationId, new SourceId(_testEvent));
            Bus.Publish(_parentTestEvent);
            Assert.Equal(BusMessages.Count, 2);

            _childTestEvent = new ChildTestEvent(_testCorrelationId, new SourceId(_parentTestEvent));
            Bus.Publish(_childTestEvent);
            Assert.Equal(BusMessages.Count, 3);

            var msg4 = new GrandChildTestEvent(_testCorrelationId, new SourceId(_childTestEvent));
            Bus.Publish(msg4);
            Assert.Equal(BusMessages.Count, 4);

            //used in multiple_message_handle_invocations_are_correct test 
        }

        [Fact]
        public void grand_child_invokes_all_handlers_once()
        {
            using (var sub = new TestInheritedMessageSubscriber(Bus))
            {
                var subscriber = sub;
                TestQueue.Clear();
                Bus.Publish(new GrandChildTestEvent(_testCorrelationId, new SourceId(_childTestEvent)));

                BusMessages
                    .AssertNext<GrandChildTestEvent>(_testCorrelationId)
                    .AssertEmpty();

                Assert.IsOrBecomesTrue(() => subscriber.Starving, 3000);

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.TestDomainEventHandleCount) == 0,
                    1000,
                    $"Expected 0 Test Domain Event Handled, found {subscriber.TestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.GrandChildTestDomainEventHandleCount) == 1,
                    1000,
                    $"Expected 1 GrandChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.ChildTestDomainEventHandleCount) == 1,
                    1000,
                    $"Expected 1 ChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.ParentTestDomainEventHandleCount) == 1,
                    1000,
                    $"Expected 1 Parent Test Domain Event handled, found {subscriber.ParentTestDomainEventHandleCount}");
            }
        }

        [Fact]
        public void child_invokes_parent_and_child_handlers_once()
        {
            using (var sub = new TestInheritedMessageSubscriber(Bus))
            {
                var subscription = sub;
                TestQueue.Clear();
                Bus.Publish(new ChildTestEvent(_testCorrelationId, new SourceId(_childTestEvent)));

                BusMessages
                    .AssertNext<ChildTestEvent>(_testCorrelationId)
                    .AssertEmpty();

                Assert.IsOrBecomesTrue(() => subscription.Starving, 3000);

                Assert.IsOrBecomesTrue(() => subscription.TestDomainEventHandleCount == 0,
                    1000,
                    $"Expected 0 Test Domain Event Handled, found {subscription.TestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscription.GrandChildTestDomainEventHandleCount) == 0,
                    1000,
                    $"Expected 0 GrandChildTestDomainEvent handled , found {subscription.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscription.ChildTestDomainEventHandleCount) == 1,
                    1000,
                    $"Expected 1 ChildTestDomainEvent handled , found {subscription.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscription.ParentTestDomainEventHandleCount) == 1,
                    1000,
                    $"Expected 1 Parent Test Domain Event handled, found {subscription.ParentTestDomainEventHandleCount}");
               
            }
        }

        [Fact]
        public void parent_invokes_only_parent_handler_once()
        {
            using (var sub = new TestInheritedMessageSubscriber(Bus))
            {
                var subscriber = sub;
                TestQueue.Clear();

                Bus.Publish(new ParentTestEvent(_testCorrelationId, new SourceId(_childTestEvent)));

                BusMessages
                    .AssertNext<ParentTestEvent>(_testCorrelationId)
                    .AssertEmpty();

                Assert.IsOrBecomesTrue(() => subscriber.Starving, 3000);

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.TestDomainEventHandleCount) == 0,
                    1000,
                    $"Expected 0 Test Domain Event Handled, found {subscriber.TestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.GrandChildTestDomainEventHandleCount) == 0,
                    1000,
                    $"Expected 0 GrandChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                     () => Interlocked.Read(ref subscriber.ChildTestDomainEventHandleCount) == 0,
                    1000,
                    $"Expected 0 ChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.ParentTestDomainEventHandleCount) == 1,
                    1000,
                    $"Expected 1 Parent Test Domain Event handled, found {subscriber.ParentTestDomainEventHandleCount}");
            }
        }
        [Fact]
        public void multiple_message_handle_invocations_are_correct()
        {
            BusMessages
                 .AssertNext<TestEvent>(_testCorrelationId)
                 .AssertNext<ParentTestEvent>(_testCorrelationId)
                 .AssertNext<ChildTestEvent>(_testCorrelationId)
                 .AssertNext<GrandChildTestEvent>(_testCorrelationId)
                 .AssertEmpty();

            Assert.IsOrBecomesTrue(
               () => Interlocked.Read(ref MessageSubscriber.TestDomainEventHandleCount) == 1,
               1000,
               $"Expected 1 Test Domain Event Handled, found {MessageSubscriber.TestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
               () => Interlocked.Read(ref MessageSubscriber.GrandChildTestDomainEventHandleCount) == 1,
               1000,
               $"Expected 1 GrandChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ChildTestDomainEventHandleCount) == 2,
                1000,
                $"Expected 2 ChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ParentTestDomainEventHandleCount) == 3,
                1000,
                $"Expected 3 Parent Test Domain Event handled, found {MessageSubscriber.ParentTestDomainEventHandleCount}");


        }
    }
}
