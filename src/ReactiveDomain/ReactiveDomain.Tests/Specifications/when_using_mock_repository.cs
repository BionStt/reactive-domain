﻿using System;
using System.Collections.Generic;
using System.Net;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using ReactiveDomain.Bus;
using ReactiveDomain.Domain;
using ReactiveDomain.EventStore;
using ReactiveDomain.Tests.EventStore;
using ReactiveDomain.Tests.Helpers;
using ReactiveDomain.Tests.Mocks;
using Xunit;

namespace ReactiveDomain.Tests.Specifications
{
    // ReSharper disable InconsistentNaming
    public class when_using_mock_repository
    {
        private readonly List<IRepository> _repos = new List<IRepository>();

        public when_using_mock_repository()
        {
            _repos.Add(new MockEventStoreRepository());
            // we only need this when adding new tests for the mock repo, otherwise we don't want to need an eventstore to run unit tests.
            // _repos.Add(BuildDefaultRepo());
        }
        private static GetEventStoreRepository BuildDefaultRepo()
        {
            var connSettings = ConnectionSettings.Create();
            connSettings.UseConsoleLogger();
            connSettings.SetDefaultUserCredentials(new UserCredentials("admin", "changeit"));
            var conn = EventStoreConnection.Create(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1113));
            conn.ConnectAsync().Wait();
            return new GetEventStoreRepository(conn, null);
        }
        [Fact]
        public void can_save_new_aggregate()
        {
            foreach (var repo in _repos)
            {
                var id = Guid.NewGuid();
                var tAgg = new TestAggregate(id);
                repo.Save(tAgg, Guid.NewGuid(), a => { });
                var rAgg = repo.GetById<TestAggregate>(id);
                Assert.NotNull(rAgg);
                Assert.Equal(tAgg.Id, rAgg.Id);
            }
        }
        [Fact]
        public void can_update_and_save_aggregate()
        {

            foreach (var repo in _repos)
            {
                var id = Guid.NewGuid();
                var tAgg = new TestAggregate(id);
                repo.Save(tAgg, Guid.NewGuid(), h => { });
                // Get v2
                var v2Agg = repo.GetById<TestAggregate>(id);
                Assert.Equal((uint)0, v2Agg.CurrentAmount());
                // update v2
                v2Agg.RaiseBy(1);
                Assert.Equal((uint)1, v2Agg.CurrentAmount());
                repo.Save(v2Agg, Guid.NewGuid(), h => { });
                // get v3
                var v3Agg = repo.GetById<TestAggregate>(id);
                Assert.Equal((uint)1, v3Agg.CurrentAmount());
            }
        }

        [Fact]
        public void can_get_aggregate_at_version()
        {
            foreach (var repo in _repos)
            {
                var id = Guid.NewGuid();
                var tAgg = new TestAggregate(id);
                tAgg.RaiseBy(1);
                Assert.Equal((uint)1, tAgg.CurrentAmount());
                Assert.Equal(2, tAgg.Version);
                tAgg.RaiseBy(2);
                Assert.Equal((uint)3, tAgg.CurrentAmount());
                Assert.Equal(3, tAgg.Version);

                // get latest version (v3)
                repo.Save(tAgg, Guid.NewGuid(), h => { });
                var v3Agg = repo.GetById<TestAggregate>(id);
                Assert.Equal((uint)3, v3Agg.CurrentAmount());
                Assert.Equal(3, v3Agg.Version);

                //get version v2
                var v2Agg = repo.GetById<TestAggregate>(id, 2);
                Assert.Equal((uint)1, v2Agg.CurrentAmount());
                Assert.Equal(2, v2Agg.Version);
            }
        }
        [Fact]
        public void will_throw_concurrency_exception()
        {
            foreach (var repo in _repos)
            {
                var id = Guid.NewGuid();
                var tAgg = new TestAggregate(id);
                tAgg.RaiseBy(1);
                Assert.Equal((uint)1, tAgg.CurrentAmount());
                Assert.Equal(2, tAgg.Version);
                tAgg.RaiseBy(2);
                Assert.Equal((uint)3, tAgg.CurrentAmount());
                Assert.Equal(3, tAgg.Version);

                // get latest version (v3) then update & save
                repo.Save(tAgg, Guid.NewGuid(), h => { });
                var v3Agg = repo.GetById<TestAggregate>(id);
                v3Agg.RaiseBy(2);
                repo.Save(v3Agg, Guid.NewGuid(), h => { });

                //Update & save original copy
                tAgg.RaiseBy(6);
                var r = repo; //copy iteration varible for closure
                Assert.Throws<AggregateException>(() => r.Save(tAgg, Guid.NewGuid(), h => { }));
            }
        }
        [Fact]
        public void can_subscribe_to_all()
        {

            foreach (var repo in _repos)
            {
                var r = repo as MockEventStoreRepository;
                if (r == null) continue;
                var q = new TestQueue();
                r.Subscribe(q);

                var id = Guid.NewGuid();
                var tAgg = new TestAggregate(id);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(1);
                repo.Save(tAgg, Guid.NewGuid(), h => { });

                Assert.Equal(4, q.Messages.Count);

            }
        }
        [Fact]
        public void can_replay_all()
        {
            foreach (var repo in _repos)
            {
                var r = repo as MockEventStoreRepository;
                if (r == null) continue;
                var q = new TestQueue();
                r.Subscribe(q);

                var id1 = Guid.NewGuid();
                var tAgg = new TestAggregate(id1);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(2);
                tAgg.RaiseBy(3);
                repo.Save(tAgg, Guid.NewGuid(), h => { });

                var id2 = Guid.NewGuid();
                var tAgg2 = new TestAggregate(id2);
                tAgg2.RaiseBy(4);
                tAgg2.RaiseBy(5);
                tAgg2.RaiseBy(6);
                repo.Save(tAgg2, Guid.NewGuid(), h => { });

                Assert.Equal(8, q.Messages.Count);
                var bus = new InMemoryBus("all");
                var q2 = new TestQueue(bus);
                r.ReplayAllOnto(bus);
                q2.Messages
                     .AssertNext<TestAggregateMessages.NewAggregate>(
                        correlationId: id1)
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 1, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 2, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 3, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.NewAggregate>(
                        correlationId: id2)
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 4, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 5, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 6, "Event mismatch wrong amount")
                    .AssertEmpty();
            }
        }
        [Fact]
        public void can_get_by_stream()
        {
            foreach (var repo in _repos)
            {
                var r = repo as MockEventStoreRepository;
                if (r == null) continue;
                var q = new TestQueue();
                r.Subscribe(q);

                var id1 = Guid.NewGuid();
                var tAgg = new TestAggregate(id1);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(2);
                tAgg.RaiseBy(3);
                repo.Save(tAgg, Guid.NewGuid(), h => { });

                var id2 = Guid.NewGuid();
                var tAgg2 = new TestAggregate(id2);
                tAgg2.RaiseBy(4);
                tAgg2.RaiseBy(5);
                tAgg2.RaiseBy(6);
                repo.Save(tAgg2, Guid.NewGuid(), h => { });

                Assert.Equal(8, q.Messages.Count);
                var bus = new InMemoryBus("all");
                var q2 = new TestQueue(bus);
                r.ReplayStreamOnto(bus, $"testAggregate-{id1.ToString("N")}");
                q2.Messages
                    .AssertNext<TestAggregateMessages.NewAggregate>(
                        correlationId: id1)
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 1, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 2, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 3, "Event mismatch wrong amount");

                r.ReplayStreamOnto(bus, $"testAggregate-{id2.ToString("N")}");
                q2.Messages
                    .AssertNext<TestAggregateMessages.NewAggregate>(
                        correlationId: id2)
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 4, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 5, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 6, "Event mismatch wrong amount")
                    .AssertEmpty();
            }
        }
        [Fact]
        public void can_get_by_category()
        {
            foreach (var repo in _repos)
            {
                var r = repo as MockEventStoreRepository;
                if (r == null) continue;
                var q = new TestQueue();
                r.Subscribe(q);

                var id1 = Guid.NewGuid();
                var tAgg = new TestAggregate(id1);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(2);
                tAgg.RaiseBy(3);
                repo.Save(tAgg, Guid.NewGuid(), h => { });

                var id3 = Guid.NewGuid();
                var tAgg3 = new TestWoftamAggregate(id3);
                tAgg3.ProduceEvents(3);
                repo.Save(tAgg3, Guid.NewGuid(), h => { });

                var id2 = Guid.NewGuid();
                var tAgg2 = new TestAggregate(id2);
                tAgg2.RaiseBy(4);
                tAgg2.RaiseBy(5);
                tAgg2.RaiseBy(6);
                repo.Save(tAgg2, Guid.NewGuid(), h => { });



                Assert.Equal(12, q.Messages.Count);
                var bus = new InMemoryBus("all");
                var q2 = new TestQueue(bus);
                r.ReplayCategoryOnto(bus, "testAggregate");
                q2.Messages
                    .AssertNext<TestAggregateMessages.NewAggregate>(
                        correlationId: id1)
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 1, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 2, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 3, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.NewAggregate>(
                        correlationId: id2)
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 4, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 5, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 6, "Event mismatch wrong amount")
                    .AssertEmpty();
                r.ReplayCategoryOnto(bus, "testWoftamAggregate");
                q2.Messages
                    .AssertNext<TestWoftamAggregateCreated>(
                        correlationId: id3)
                    .AssertNext<WoftamEvent>(
                        correlationId: Guid.Empty)
                    .AssertNext<WoftamEvent>(
                        correlationId: Guid.Empty)
                   .AssertNext<WoftamEvent>(
                        correlationId: Guid.Empty)
                   .AssertEmpty();
            }
        }
    }
    // ReSharper restore InconsistentNaming
}
