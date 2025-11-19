using Xunit;

namespace RTUB.Application.Tests.Collections;

[CollectionDefinition("MeetingService Collection")]
public class MeetingServiceCollection : ICollectionFixture<Fixtures.DatabaseFixture>
{
}
