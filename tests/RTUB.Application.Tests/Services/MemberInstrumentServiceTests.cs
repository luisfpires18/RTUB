using MockQueryable.Moq;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MemberInstrumentService - Multiple instruments per member
/// </summary>
public partial class MemberInstrumentServiceTests
{
    private readonly Mock<IMemberInstrumentRepository> _mockRepository;
    private readonly MemberInstrumentService _service;
    private readonly string _testMemberId = "test-member-id";

    public MemberInstrumentServiceTests()
    {
        _mockRepository = new Mock<IMemberInstrumentRepository>();
        _service = new MemberInstrumentService(_mockRepository.Object);
    }
}
