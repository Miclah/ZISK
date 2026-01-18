using ZISK.Shared.Enums;

namespace ZISK.Shared.DTOs.Trainings;

public record TrainingEventDto(
    Guid Id,
    Guid TeamId,
    string TeamName,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string? Location,
    TrainingType Type,
    string? CoachNote,
    bool IsLocked
);

public record TrainingEventDetailDto(
    Guid Id,
    Guid TeamId,
    string TeamName,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string? Location,
    TrainingType Type,
    string? CoachNote,
    bool IsLocked,
    DateTime CreatedAt,
    List<TrainingAttendanceDto> Attendance
);

public record TrainingAttendanceDto(
    Guid ChildId,
    string ChildName,
    AttendanceStatus Status,
    string? Note,
    string? CoachComment,
    bool HasExcuse,
    string? ExcuseReason
);

public record CreateTrainingEventRequest(
    Guid TeamId,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string? Location,
    TrainingType Type,
    string? CoachNote
);

public record UpdateTrainingEventRequest(
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string? Location,
    TrainingType Type,
    string? CoachNote,
    bool IsLocked
);
