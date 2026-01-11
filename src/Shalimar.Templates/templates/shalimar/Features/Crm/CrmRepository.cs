using System.Collections.Concurrent;

namespace ShalimarApp.Features.Crm;

public sealed class CrmRepository
{
    private readonly object _gate = new();
    private readonly IClock _clock;
    private readonly List<ActivityItemDto> _activity = new();
    private readonly List<UserDto> _users = new();
    private readonly List<AccountDto> _accounts = new();
    private readonly List<ContactDto> _contacts = new();
    private readonly List<EpicDto> _epics = new();
    private readonly List<TaskDto> _tasks = new();
    private readonly ConcurrentDictionary<string, List<TaskMessageDto>> _messagesByTask = new();
    private readonly ConcurrentDictionary<string, List<TaskArtifactDto>> _artifactsByTask = new();
    private readonly ConcurrentDictionary<string, List<TaskDecisionDto>> _decisionsByTask = new();

    public CrmRepository(IClock clock)
    {
        _clock = clock;
        Seed();
    }

    public IReadOnlyList<UserDto> Users => _users;
    public IReadOnlyList<AccountDto> Accounts => _accounts;
    public IReadOnlyList<ContactDto> Contacts => _contacts;
    public IReadOnlyList<EpicDto> Epics => _epics;
    public IReadOnlyList<TaskDto> Tasks => _tasks;

    public IReadOnlyList<ContactDto> ContactsByAccount(string accountId) =>
        _contacts.Where(c => c.AccountId == accountId).ToList();

    public IReadOnlyList<ActivityItemDto> Activity()
    {
        lock (_gate) return _activity.ToList();
    }

    public CrmSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new CrmSnapshot(
                Users: _users.ToList(),
                Accounts: _accounts.ToList(),
                Epics: _epics.ToList(),
                Tasks: _tasks.ToList(),
                Activity: _activity.ToList());
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _activity.Clear();
            _users.Clear();
            _accounts.Clear();
            _contacts.Clear();
            _epics.Clear();
            _tasks.Clear();
            _messagesByTask.Clear();
            _artifactsByTask.Clear();
            _decisionsByTask.Clear();

            Seed();
        }
    }

    public CrmInsightsDto Insights()
    {
        lock (_gate)
        {
            var open = _tasks.Count(t => t.Status != "done");
            var blocked = _tasks.Count(t => t.Status == "blocked");
            var overdue = _tasks.Count(t => t.DueAt.HasValue && t.DueAt.Value < _clock.UtcNow && t.Status != "done");

            var summary = blocked > 0
                ? $"Focus: unblock {blocked} task(s). {open} open across {_epics.Count} epic(s)."
                : $"{open} open task(s) across {_epics.Count} epic(s). Keep WIP low.";

            var risks = new List<string>();
            if (overdue > 0) risks.Add($"{overdue} overdue task(s) — review due dates and re-scope.");
            if (blocked > 0) risks.Add("Blocked work is accumulating — assign owners for blockers.");
            if (open > 8) risks.Add("Open task count is high — consider splitting epics or pausing lower priority work.");
            if (risks.Count == 0) risks.Add("No critical risks detected.");

            var next = new List<string>
            {
                "Confirm epic owners and collaborators for the next 48 hours.",
                "Move one task to “done” before starting a new one.",
                "Capture decisions + artifacts for auditability."
            };

            return new CrmInsightsDto(summary, risks, next);
        }
    }

    public CrmForecastDto Forecast()
    {
        lock (_gate)
        {
            var summary = "Forecast is stable. Biggest impact comes from unblocking high priority work in active epics.";

            var drivers = new List<string>
            {
                "Blocked tasks reduce throughput and increase cycle time.",
                "High-priority tasks in active epics drive customer outcomes.",
                "Too many open tasks creates context switching."
            };

            var plays = new List<string>
            {
                "Schedule a 15-minute blocker resolution huddle today.",
                "Convert one artifact into a customer-ready deliverable.",
                "Split large tasks into <60 minute slices before assigning."
            };

            return new CrmForecastDto(summary, drivers, plays);
        }
    }

    public TaskDto? GetTask(string taskId) => _tasks.FirstOrDefault(t => t.Id == taskId);

    public EpicDto? GetEpic(string epicId) => _epics.FirstOrDefault(e => e.Id == epicId);

    public IReadOnlyList<TaskMessageDto> Messages(string taskId) =>
        _messagesByTask.TryGetValue(taskId, out var list) ? list.ToList() : [];

    public IReadOnlyList<TaskArtifactDto> Artifacts(string taskId) =>
        _artifactsByTask.TryGetValue(taskId, out var list) ? list.ToList() : [];

    public IReadOnlyList<TaskDecisionDto> Decisions(string taskId) =>
        _decisionsByTask.TryGetValue(taskId, out var list) ? list.ToList() : [];

    public TaskDto CreateTask(CreateTaskRequest req)
    {
        var now = _clock.UtcNow;
        lock (_gate)
        {
            var t = new TaskDto(
                Id: MakeId("t"),
                Title: req.Title.Trim(),
                Status: "todo",
                Priority: req.Priority ?? "medium",
                EpicId: string.IsNullOrWhiteSpace(req.EpicId) ? null : req.EpicId,
                AccountId: string.IsNullOrWhiteSpace(req.AccountId) ? null : req.AccountId,
                AssigneeId: string.IsNullOrWhiteSpace(req.AssigneeId) ? null : req.AssigneeId,
                CollaboratorIds: req.CollaboratorIds ?? [],
                DueAt: req.DueAt,
                EstimateMinutes: req.EstimateMinutes,
                Tags: req.Tags ?? [],
                UpdatedAt: now);

            _tasks.Insert(0, t);
            PushActivity($"Created task \"{t.Title}\"");

            _messagesByTask[t.Id] = [
                new TaskMessageDto(
                    Id: MakeId("m"),
                    TaskId: t.Id,
                    Ts: now,
                    Actor: "agent",
                    Author: "Shalimar Agent",
                    Body: "New task created. Add context here so I can help you execute it faster.")
            ];

            return t;
        }
    }

    public TaskDto? UpdateTask(string taskId, UpdateTaskRequest req)
    {
        lock (_gate)
        {
            var i = _tasks.FindIndex(t => t.Id == taskId);
            if (i < 0) return null;

            var current = _tasks[i];
            var updated = current with
            {
                Title = req.Title?.Trim() ?? current.Title,
                Priority = req.Priority ?? current.Priority,
                EpicId = req.EpicIdSet ? req.EpicId : current.EpicId,
                AccountId = req.AccountIdSet ? req.AccountId : current.AccountId,
                AssigneeId = req.AssigneeIdSet ? req.AssigneeId : current.AssigneeId,
                CollaboratorIds = req.CollaboratorIds ?? current.CollaboratorIds,
                DueAt = req.DueAtSet ? req.DueAt : current.DueAt,
                EstimateMinutes = req.EstimateMinutesSet ? req.EstimateMinutes : current.EstimateMinutes,
                UpdatedAt = _clock.UtcNow
            };

            _tasks[i] = updated;
            PushActivity($"Updated task \"{updated.Title}\"");
            return updated;
        }
    }

    public TaskDto? MoveTask(string taskId, MoveTaskRequest req)
    {
        lock (_gate)
        {
            var i = _tasks.FindIndex(t => t.Id == taskId);
            if (i < 0) return null;

            var current = _tasks[i];
            var updated = current with
            {
                Status = req.Status ?? current.Status,
                EpicId = req.EpicIdSet ? req.EpicId : current.EpicId,
                UpdatedAt = _clock.UtcNow
            };

            _tasks[i] = updated;
            PushActivity($"Moved task \"{updated.Title}\" → {updated.Status.Replace('_', ' ')}");
            return updated;
        }
    }

    public TaskMessageDto PostMessage(string taskId, PostMessageRequest req)
    {
        var now = _clock.UtcNow;
        var msg = new TaskMessageDto(
            Id: MakeId("m"),
            TaskId: taskId,
            Ts: now,
            Actor: req.Actor,
            Author: req.Actor == "agent" ? "Shalimar Agent" : "Human",
            Body: req.Body.Trim());

        var list = _messagesByTask.GetOrAdd(taskId, _ => new List<TaskMessageDto>());
        lock (_gate)
        {
            list.Add(msg);
            PushActivity($"{(req.Actor == "agent" ? "Agent" : "Human")} replied on task");
        }
        return msg;
    }

    public TaskArtifactDto AddArtifact(string taskId, AddArtifactRequest req)
    {
        var now = _clock.UtcNow;
        var a = new TaskArtifactDto(
            Id: MakeId("art"),
            TaskId: taskId,
            Ts: now,
            Kind: req.Kind,
            Title: req.Title.Trim(),
            Content: req.Content,
            CreatedBy: req.CreatedBy);

        var list = _artifactsByTask.GetOrAdd(taskId, _ => new List<TaskArtifactDto>());
        lock (_gate)
        {
            list.Insert(0, a);
            PushActivity($"{(req.CreatedBy == "agent" ? "Agent" : "Human")} created artifact: {a.Title}");
        }
        return a;
    }

    public TaskDecisionDto AddDecision(string taskId, AddDecisionRequest req)
    {
        var now = _clock.UtcNow;
        var d = new TaskDecisionDto(
            Id: MakeId("dec"),
            TaskId: taskId,
            Ts: now,
            Question: req.Question.Trim(),
            Options: req.Options,
            Outcome: req.Outcome.Trim(),
            Rationale: req.Rationale,
            MadeBy: req.MadeBy);

        var list = _decisionsByTask.GetOrAdd(taskId, _ => new List<TaskDecisionDto>());
        lock (_gate)
        {
            list.Insert(0, d);
            PushActivity($"{(req.MadeBy == "agent" ? "Agent" : "Human")} recorded decision: {d.Outcome}");
        }
        return d;
    }

    private void PushActivity(string summary)
    {
        _activity.Insert(0, new ActivityItemDto(MakeId("act"), _clock.UtcNow, "task", summary));
        if (_activity.Count > 50) _activity.RemoveRange(50, _activity.Count - 50);
    }

    private static string MakeId(string prefix) =>
        $"{prefix}_{Guid.NewGuid():N}".Substring(0, prefix.Length + 1 + 10);

    private void Seed()
    {
        var now = _clock.UtcNow;
        _users.AddRange([
            new UserDto("u_1", "Ava Chen", "ava@shalimar.local"),
            new UserDto("u_2", "Noah Patel", "noah@shalimar.local"),
            new UserDto("u_3", "Mia Johnson", "mia@shalimar.local"),
        ]);

        _accounts.AddRange([
            new AccountDto("a_1", "Northwind Traders", "Retail", "Mid", "u_1", now),
            new AccountDto("a_2", "Contoso", "Software", "Enterprise", "u_2", now),
            new AccountDto("a_3", "Fabrikam", "Manufacturing", "SMB", "u_3", now),
        ]);

        _contacts.AddRange([
            new ContactDto("c_1", "a_1", "Jordan Lee", "Buyer", "jordan@northwind.example"),
            new ContactDto("c_2", "a_2", "Riley Smith", "VP Engineering", "riley@contoso.example"),
            new ContactDto("c_3", "a_2", "Casey Nguyen", "Procurement", "casey@contoso.example"),
            new ContactDto("c_4", "a_3", "Taylor Kim", "Owner", "taylor@fabrikam.example"),
        ]);

        _epics.AddRange([
            new EpicDto("e_1", "Contoso: Inbound lead → first meeting", "Qualify, align stakeholders, and schedule a discovery call.", "active", now),
            new EpicDto("e_2", "Northwind: QBR + renewal readiness", "Prepare QBR artifacts, risks, and next-quarter plan.", "active", now),
        ]);

        _tasks.AddRange([
            new TaskDto("t_1", "Qualify inbound lead (Contoso)", "todo", "high", "e_1", "a_2", "u_1", ["u_2"], now.AddDays(1), 45, ["lead", "email"], now),
            new TaskDto("t_2", "Prepare QBR deck (Northwind)", "in_progress", "medium", "e_2", "a_1", "u_2", ["u_1", "u_3"], now.AddDays(5), 180, ["qbr"], now),
            new TaskDto("t_3", "Send renewal reminder (Fabrikam)", "backlog", "low", null, "a_3", "u_3", [], null, null, ["renewal"], now),
            new TaskDto("t_4", "Resolve pricing blocker (Contoso)", "blocked", "high", "e_1", "a_2", "u_2", ["u_1"], null, 60, ["pricing", "legal"], now),
            new TaskDto("t_5", "Post-call notes (Northwind)", "done", "medium", "e_2", "a_1", "u_1", [], null, 30, ["notes"], now),
        ]);

        _messagesByTask["t_1"] = [
            new TaskMessageDto("m_1", "t_1", now, "agent", "Shalimar Agent", "I can draft the outreach email and propose next steps. Who is the best contact at Contoso?"),
            new TaskMessageDto("m_2", "t_1", now, "human", "Ava Chen", "Use Riley (VP Eng). Keep it short and propose a 15-min qualification call."),
        ];

        _artifactsByTask["t_1"] = [
            new TaskArtifactDto("a_1", "t_1", now, "email", "Draft outreach email",
                "Hi Riley — thanks for reaching out. I’d love to learn more about what Contoso is evaluating. Would a 15‑minute call tomorrow work?",
                "agent")
        ];

        _decisionsByTask["t_1"] = [
            new TaskDecisionDto("d_1", "t_1", now, "Who is the primary contact for qualification?",
                ["Riley (VP Eng)", "Casey (Procurement)", "Jordan (Buyer)"],
                "Riley (VP Eng)",
                "They own technical evaluation and can schedule stakeholders quickly.",
                "human")
        ];

        _activity.Add(new ActivityItemDto("act_1", now, "system", "CRM agent bootstrapped with server-owned mock data."));
    }
}

public sealed record UserDto(string Id, string Name, string Email);
public sealed record AccountDto(string Id, string Name, string Industry, string Tier, string OwnerId, DateTimeOffset UpdatedAt);
public sealed record ContactDto(string Id, string AccountId, string Name, string Title, string Email);
public sealed record EpicDto(string Id, string Title, string? Description, string Status, DateTimeOffset UpdatedAt);
public sealed record TaskDto(
    string Id,
    string Title,
    string Status,
    string Priority,
    string? EpicId,
    string? AccountId,
    string? AssigneeId,
    IReadOnlyList<string> CollaboratorIds,
    DateTimeOffset? DueAt,
    int? EstimateMinutes,
    IReadOnlyList<string> Tags,
    DateTimeOffset UpdatedAt);
public sealed record TaskMessageDto(string Id, string TaskId, DateTimeOffset Ts, string Actor, string Author, string Body);
public sealed record TaskArtifactDto(string Id, string TaskId, DateTimeOffset Ts, string Kind, string Title, string Content, string CreatedBy);
public sealed record TaskDecisionDto(string Id, string TaskId, DateTimeOffset Ts, string Question, IReadOnlyList<string> Options, string Outcome, string? Rationale, string MadeBy);
public sealed record ActivityItemDto(string Id, DateTimeOffset Ts, string Kind, string Summary);

public sealed record CreateTaskRequest(
    string Title,
    string? Priority,
    string? EpicId,
    string? AccountId,
    string? AssigneeId,
    IReadOnlyList<string>? CollaboratorIds,
    DateTimeOffset? DueAt,
    int? EstimateMinutes,
    IReadOnlyList<string>? Tags);

public sealed record UpdateTaskRequest(
    string? Title,
    string? Priority,
    bool EpicIdSet,
    string? EpicId,
    bool AccountIdSet,
    string? AccountId,
    bool AssigneeIdSet,
    string? AssigneeId,
    bool DueAtSet,
    DateTimeOffset? DueAt,
    bool EstimateMinutesSet,
    int? EstimateMinutes,
    IReadOnlyList<string>? CollaboratorIds);

public sealed record MoveTaskRequest(
    string? Status,
    bool EpicIdSet,
    string? EpicId);

public sealed record PostMessageRequest(string Actor, string Body);
public sealed record AddArtifactRequest(string Kind, string Title, string Content, string CreatedBy);
public sealed record AddDecisionRequest(string Question, IReadOnlyList<string> Options, string Outcome, string? Rationale, string MadeBy);

