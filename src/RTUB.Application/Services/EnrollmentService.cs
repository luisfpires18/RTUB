using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using RTUB.Core.Enums;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;

namespace RTUB.Application.Services;

/// <summary>
/// Enrollment service implementation
/// Contains business logic for enrollment operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class EnrollmentService : IEnrollmentService
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IRetirementStatusService _retirementStatusService;

    public EnrollmentService(
        IEnrollmentRepository enrollmentRepository,
        IRetirementStatusService retirementStatusService)
    {
        _enrollmentRepository = enrollmentRepository;
        _retirementStatusService = retirementStatusService;
    }

    public async Task<Enrollment?> GetEnrollmentByIdAsync(int id)
    {
        return await _enrollmentRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Enrollment>> GetAllEnrollmentsAsync()
    {
        return await _enrollmentRepository.GetAllAsync();
    }

    public async Task<IEnumerable<Enrollment>> GetEnrollmentsByEventIdAsync(int eventId)
    {
        return await _enrollmentRepository.GetByEventIdAsync(eventId);
    }

    public async Task<IEnumerable<Enrollment>> GetEnrollmentsByUserIdAsync(string userId)
    {
        return await _enrollmentRepository.GetByUserIdAsync(userId);
    }

    public async Task<Enrollment> CreateEnrollmentAsync(string userId, int eventId, InstrumentType? instrument = null, string? notes = null, bool willAttend = true, string? otherInstruments = null)
    {
        var enrollment = Enrollment.Create(userId, eventId);
        enrollment.Instrument = instrument;
        enrollment.Notes = notes;
        enrollment.WillAttend = willAttend;
        enrollment.OtherInstruments = otherInstruments;
        return await _enrollmentRepository.AddAsync(enrollment);
    }

    public async Task<Enrollment> UpdateEnrollmentAsync(
       int enrollmentId,
       bool willAttend,
       InstrumentType? instrument = null,
       string? notes = null,
       string? otherInstruments = null)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(enrollmentId);

        if (enrollment == null)
        {
            throw new EntityNotFoundException(nameof(Enrollment), enrollmentId);
        }

        // Update enrollment fields
        enrollment.WillAttend = willAttend;
        enrollment.Instrument = instrument;
        enrollment.Notes = notes;
        enrollment.OtherInstruments = otherInstruments;

        await _enrollmentRepository.UpdateAsync(enrollment);
        
        // Update retirement status if enrollment was confirmed
        if (willAttend)
        {
            await _retirementStatusService.UpdateUserRetirementStatusAsync(enrollment.UserId);
        }

        return enrollment;
    }

    public async Task DeleteEnrollmentAsync(int id)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(id);
        if (enrollment == null)
            throw new EntityNotFoundException(nameof(Enrollment), id);

        await _enrollmentRepository.DeleteAsync(id);
    }
}
