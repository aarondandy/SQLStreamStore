﻿namespace SqlStreamStore
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Shouldly;
    using SqlStreamStore.Streams;
    using Xunit;

    public partial class StreamStoreAcceptanceTests
    {
        [Fact]
        public async Task Can_read_all_forwards()
        {
            using (var fixture = GetFixture())
            {
                using (var store = await fixture.GetStreamStore())
                {
                    await store.AppendToStream("stream-1", ExpectedVersion.NoStream, CreateNewStreamMessages(1, 2, 3));
                    await store.AppendToStream("stream-2", ExpectedVersion.NoStream, CreateNewStreamMessages(4, 5, 6));
                    var expectedEvents = new[]
                    {
                        ExpectedStreamMessage("stream-1", 1, 0, fixture.GetUtcNow().UtcDateTime),
                        ExpectedStreamMessage("stream-1", 2, 1, fixture.GetUtcNow().UtcDateTime),
                        ExpectedStreamMessage("stream-1", 3, 2, fixture.GetUtcNow().UtcDateTime),
                        ExpectedStreamMessage("stream-2", 4, 0, fixture.GetUtcNow().UtcDateTime),
                        ExpectedStreamMessage("stream-2", 5, 1, fixture.GetUtcNow().UtcDateTime),
                        ExpectedStreamMessage("stream-2", 6, 2, fixture.GetUtcNow().UtcDateTime)
                    };

                    var allEventsPage = await store.ReadAllForwards(Checkpoint.Start, 4);
                    List<StreamMessage> streamEvents = new List<StreamMessage>(allEventsPage.StreamMessages);
                    int count = 0;
                    while(!allEventsPage.IsEnd && count <20) //should not take more than 20 iterations.
                    {
                        allEventsPage = await store.ReadAllForwards(allEventsPage.NextCheckpoint, 10);
                        streamEvents.AddRange(allEventsPage.StreamMessages);
                        count++;
                    }

                    count.ShouldBeLessThan(20);
                    allEventsPage.Direction.ShouldBe(ReadDirection.Forward);
                    allEventsPage.IsEnd.ShouldBeTrue();

                    for (int i = 0; i < streamEvents.Count; i++)
                    {
                        var streamEvent = streamEvents[i];
                        var expectedEvent = expectedEvents[i];

                        streamEvent.EventId.ShouldBe(expectedEvent.EventId);
                        streamEvent.JsonData.ShouldBe(expectedEvent.JsonData);
                        streamEvent.JsonMetadata.ShouldBe(expectedEvent.JsonMetadata);
                        streamEvent.StreamId.ShouldBe(expectedEvent.StreamId);
                        streamEvent.StreamVersion.ShouldBe(expectedEvent.StreamVersion);
                        streamEvent.Type.ShouldBe(expectedEvent.Type);

                        // We don't care about StreamMessage.Checkpoint and StreamMessage.Checkpoint
                        // as they are non-deterministic
                    }
                }
            }
        }

        [Fact]
        public async Task Can_read_all_backwards()
        {
            using (var fixture = GetFixture())
            {
                using (var store = await fixture.GetStreamStore())
                {
                    await store.AppendToStream("stream-1", ExpectedVersion.NoStream, CreateNewStreamMessages(1, 2, 3));
                    await store.AppendToStream("stream-2", ExpectedVersion.NoStream, CreateNewStreamMessages(4, 5, 6));
                    var expectedEvents = new[]
                    {
                        ExpectedStreamMessage("stream-1", 1, 0, fixture.GetUtcNow().UtcDateTime),
                        ExpectedStreamMessage("stream-1", 2, 1, fixture.GetUtcNow().UtcDateTime),
                        ExpectedStreamMessage("stream-1", 3, 2, fixture.GetUtcNow().UtcDateTime),
                        ExpectedStreamMessage("stream-2", 4, 0, fixture.GetUtcNow().UtcDateTime),
                        ExpectedStreamMessage("stream-2", 5, 1, fixture.GetUtcNow().UtcDateTime),
                        ExpectedStreamMessage("stream-2", 6, 2, fixture.GetUtcNow().UtcDateTime)
                    }.Reverse().ToArray();

                    var allEventsPage = await store.ReadAllBackwards(Checkpoint.End, 4);
                    List<StreamMessage> streamEvents = new List<StreamMessage>(allEventsPage.StreamMessages);
                    int count = 0;
                    while (!allEventsPage.IsEnd && count < 20) //should not take more than 20 iterations.
                    {
                        allEventsPage = await store.ReadAllBackwards(allEventsPage.NextCheckpoint, 10);
                        streamEvents.AddRange(allEventsPage.StreamMessages);
                        count++;
                    }

                    count.ShouldBeLessThan(20);
                    allEventsPage.Direction.ShouldBe(ReadDirection.Backward);
                    allEventsPage.IsEnd.ShouldBeTrue();

                    streamEvents.Count.ShouldBe(expectedEvents.Length);

                    for(int i = 0; i < streamEvents.Count; i++)
                    {
                        var streamEvent = streamEvents[i];
                        var expectedEvent = expectedEvents[i];

                        streamEvent.EventId.ShouldBe(expectedEvent.EventId);
                        streamEvent.JsonData.ShouldBe(expectedEvent.JsonData);
                        streamEvent.JsonMetadata.ShouldBe(expectedEvent.JsonMetadata);
                        streamEvent.StreamId.ShouldBe(expectedEvent.StreamId);
                        streamEvent.StreamVersion.ShouldBe(expectedEvent.StreamVersion);
                        streamEvent.Type.ShouldBe(expectedEvent.Type);

                        // We don't care about StreamMessage.Checkpoint and StreamMessage.Checkpoint
                        // as they are non-deterministic
                    }
                }
            }
        }

        [Theory]
        [InlineData(3, 0, 3, 3, 0, 3)]  // Read entire store
        [InlineData(3, 0, 4, 3, 0, 3)]  // Read entire store
        [InlineData(3, 0, 2, 2, 0, 2)]
        [InlineData(3, 1, 2, 2, 1, 3)]
        [InlineData(3, 2, 1, 1, 2, 3)]
        [InlineData(3, 3, 1, 0, 3, 3)]
        public async Task When_read_all_forwards(
            int numberOfSeedEvents,
            int fromCheckpoint,
            int maxCount,
            int expectedCount,
            int expectedFromCheckpoint,
            int expectedNextCheckPoint)
        {
            using(var fixture = GetFixture())
            {
                using(var store = await fixture.GetStreamStore())
                {
                    await store.AppendToStream(
                        "stream-1",
                        ExpectedVersion.NoStream,
                        CreateNewStreamEventSequence(1, numberOfSeedEvents));

                    var allEventsPage = await store.ReadAllForwards(fromCheckpoint, maxCount);

                    allEventsPage.StreamMessages.Length.ShouldBe(expectedCount);
                    allEventsPage.FromCheckpoint.ShouldBe(expectedFromCheckpoint);
                    allEventsPage.NextCheckpoint.ShouldBe(expectedNextCheckPoint);
                }
            }
        }

        [Theory]
        [InlineData(3, -1, 1, 1, 2, 1)] // -1 is Checkpoint.End
        [InlineData(3, 2, 1, 1, 2, 1)]
        [InlineData(3, 1, 1, 1, 1, 0)]
        [InlineData(3, 0, 1, 1, 0, 0)]
        [InlineData(3, -1, 3, 3, 2, 0)] // Read entire store
        [InlineData(3, -1, 4, 3, 2, 0)] // Read entire store
        [InlineData(0, -1, 1, 0, 0, 0)]
        public async Task When_read_all_backwards(
            int numberOfSeedEvents,
            int fromCheckpoint,
            int maxCount,
            int expectedCount,
            int expectedFromCheckpoint,
            int expectedNextCheckPoint)
        {
            using (var fixture = GetFixture())
            {
                using (var store = await fixture.GetStreamStore())
                {
                    if(numberOfSeedEvents > 0)
                    {
                        await store.AppendToStream(
                            "stream-1",
                            ExpectedVersion.NoStream,
                            CreateNewStreamEventSequence(1, numberOfSeedEvents));
                    }

                    var allEventsPage = await store.ReadAllBackwards(fromCheckpoint, maxCount);

                    allEventsPage.StreamMessages.Length.ShouldBe(expectedCount);
                    allEventsPage.FromCheckpoint.ShouldBe(expectedFromCheckpoint);
                    allEventsPage.NextCheckpoint.ShouldBe(expectedNextCheckPoint);
                }
            }
        }
    }
}
