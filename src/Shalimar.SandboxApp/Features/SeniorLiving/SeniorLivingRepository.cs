namespace Shalimar.SandboxApp.Features.SeniorLiving;

using System.Collections.Concurrent;

public sealed class SeniorLivingRepository
{
    private readonly object _gate = new();
    private readonly List<ResidentDto> _residents = new();
    private readonly List<IncidentDto> _incidents = new();
    private readonly List<ObservationDto> _observations = new();
    private readonly List<MedDto> _meds = new();
    private readonly List<MedPassScheduleDto> _schedules = new();
    private readonly List<MedPassLogDto> _passes = new();

    public SeniorLivingRepository()
    {
        Seed();
    }

    public void Reset()
    {
        lock (_gate)
        {
            _residents.Clear();
            _incidents.Clear();
            _observations.Clear();
            _meds.Clear();
            _schedules.Clear();
            _passes.Clear();
            Seed();
        }
    }

    private void Seed()
    {
        var now = DateTimeOffset.UtcNow;

        _residents.AddRange(new[]
        {
            new ResidentDto("r_1", "Mary Johnson", "A-102", "Memory Care", now.AddDays(-40)),
            new ResidentDto("r_2", "Thomas Nguyen", "B-210", "Assisted Living", now.AddDays(-18)),
            new ResidentDto("r_3", "Evelyn Smith", "A-110", "Memory Care", now.AddDays(-7)),
        });

        _incidents.AddRange(new[]
        {
            new IncidentDto("i_1", now.AddDays(-2), "Fall", "Unwitnessed fall in hallway; no injury reported.", "open", "r_1"),
            new IncidentDto("i_2", now.AddDays(-1), "Medication", "Late med pass due to staffing.", "resolved", "r_2"),
        });

        _observations.AddRange(new[]
        {
            new ObservationDto("o_1", now.AddHours(-8), "Behavior", "Agitated during evening routine.", "r_1"),
            new ObservationDto("o_2", now.AddHours(-3), "Vitals", "BP slightly elevated; monitoring.", "r_3"),
        });

        _meds.AddRange(new[]
        {
            new MedDto("m_1", "Lisinopril 10mg", "oral", "daily"),
            new MedDto("m_2", "Metformin 500mg", "oral", "bid"),
            new MedDto("m_3", "Acetaminophen 500mg", "oral", "prn"),
        });

        _schedules.AddRange(new[]
        {
            new MedPassScheduleDto("s_1", "r_1", "m_1", new TimeOnly(9, 0), "daily"),
            new MedPassScheduleDto("s_2", "r_2", "m_2", new TimeOnly(9, 0), "bid"),
            new MedPassScheduleDto("s_3", "r_2", "m_2", new TimeOnly(21, 0), "bid"),
        });
    }

    public DashboardCountsDto Counts()
    {
        lock (_gate)
        {
            return new DashboardCountsDto(
                Residents: _residents.Count,
                Incidents: _incidents.Count(i => i.Status != "resolved"),
                Observations: _observations.Count);
        }
    }

    public IReadOnlyList<ResidentDto> ResidentsPreview(int take = 12)
    {
        lock (_gate) return _residents.OrderBy(r => r.Name).Take(take).ToList();
    }

    public IReadOnlyList<IncidentDto> IncidentsPreview(int take = 12)
    {
        lock (_gate) return _incidents.OrderByDescending(i => i.Ts).Take(take).ToList();
    }

    public IReadOnlyList<ObservationDto> ObservationsPreview(int take = 12)
    {
        lock (_gate) return _observations.OrderByDescending(o => o.Ts).Take(take).ToList();
    }

    public ResidentsGridDto ResidentsGrid(int page, int pageSize, string? q)
    {
        lock (_gate)
        {
            IEnumerable<ResidentDto> filtered = _residents;
            if (!string.IsNullOrWhiteSpace(q))
            {
                var qq = q.Trim();
                filtered = filtered.Where(r =>
                    r.Name.Contains(qq, StringComparison.OrdinalIgnoreCase) ||
                    r.Room.Contains(qq, StringComparison.OrdinalIgnoreCase) ||
                    r.CareLevel.Contains(qq, StringComparison.OrdinalIgnoreCase));
            }

            var total = filtered.Count();
            var items = filtered
                .OrderBy(r => r.Name)
                .Skip((Math.Max(1, page) - 1) * Math.Max(1, pageSize))
                .Take(Math.Max(1, pageSize))
                .ToList();

            return new ResidentsGridDto(page, pageSize, total, items);
        }
    }

    public ResidentDto CreateResident(CreateResidentRequest req)
    {
        lock (_gate)
        {
            var id = $"r_{_residents.Count + 1}";
            var now = DateTimeOffset.UtcNow;
            var created = new ResidentDto(id, req.Name, req.Room, req.CareLevel, now);
            _residents.Add(created);
            return created;
        }
    }

    public ResidentDto? UpdateResident(string id, UpdateResidentRequest req)
    {
        lock (_gate)
        {
            var idx = _residents.FindIndex(r => r.Id == id);
            if (idx < 0) return null;
            var current = _residents[idx];
            var next = current with
            {
                Name = req.Name ?? current.Name,
                Room = req.Room ?? current.Room,
                CareLevel = req.CareLevel ?? current.CareLevel,
            };
            _residents[idx] = next;
            return next;
        }
    }

    public bool DeleteResident(string id)
    {
        lock (_gate)
        {
            var removed = _residents.RemoveAll(r => r.Id == id) > 0;
            return removed;
        }
    }
}

public sealed record DashboardCountsDto(int Residents, int Incidents, int Observations);

public sealed record ResidentDto(string Id, string Name, string Room, string CareLevel, DateTimeOffset AdmittedAt);
public sealed record IncidentDto(string Id, DateTimeOffset Ts, string Kind, string Summary, string Status, string ResidentId);
public sealed record ObservationDto(string Id, DateTimeOffset Ts, string Kind, string Note, string ResidentId);

public sealed record MedDto(string Id, string Name, string Route, string Frequency);
public sealed record MedPassScheduleDto(string Id, string ResidentId, string MedId, TimeOnly Time, string Frequency);
public sealed record MedPassLogDto(string Id, DateTimeOffset Ts, string ResidentId, string MedId, string Outcome, string? Note);

public sealed record ResidentsGridDto(int Page, int PageSize, int Total, IReadOnlyList<ResidentDto> Items);

public sealed record CreateResidentRequest(string Name, string Room, string CareLevel);
public sealed record UpdateResidentRequest(string? Name, string? Room, string? CareLevel);

