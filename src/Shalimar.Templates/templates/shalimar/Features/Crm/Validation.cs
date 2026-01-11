using FluentValidation;

namespace ShalimarApp.Features.Crm;

public sealed class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Priority).Must(BePriority).When(x => x.Priority is not null);
        RuleForEach(x => x.CollaboratorIds!).NotEmpty().When(x => x.CollaboratorIds is not null);
        RuleForEach(x => x.Tags!).NotEmpty().When(x => x.Tags is not null);
        RuleFor(x => x.EstimateMinutes).GreaterThanOrEqualTo(0).When(x => x.EstimateMinutes is not null);
    }

    private static bool BePriority(string? p) =>
        p is null || p is "low" or "medium" or "high";
}

public sealed class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200).When(x => x.Title is not null);
        RuleFor(x => x.Priority).Must(BePriority).When(x => x.Priority is not null);
        RuleFor(x => x.EstimateMinutes).GreaterThanOrEqualTo(0).When(x => x.EstimateMinutesSet && x.EstimateMinutes is not null);
    }

    private static bool BePriority(string? p) =>
        p is null || p is "low" or "medium" or "high";
}

public sealed class MoveTaskRequestValidator : AbstractValidator<MoveTaskRequest>
{
    public MoveTaskRequestValidator()
    {
        RuleFor(x => x.Status).Must(BeStatus).When(x => x.Status is not null);
    }

    private static bool BeStatus(string? s) =>
        s is null || s is "backlog" or "todo" or "in_progress" or "blocked" or "done";
}

public sealed class PostMessageRequestValidator : AbstractValidator<PostMessageRequest>
{
    public PostMessageRequestValidator()
    {
        RuleFor(x => x.Actor).Must(a => a is "agent" or "human");
        RuleFor(x => x.Body).NotEmpty().MaximumLength(5000);
    }
}

public sealed class AddArtifactRequestValidator : AbstractValidator<AddArtifactRequest>
{
    public AddArtifactRequestValidator()
    {
        RuleFor(x => x.CreatedBy).Must(a => a is "agent" or "human");
        RuleFor(x => x.Kind).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(20000);
    }
}

public sealed class AddDecisionRequestValidator : AbstractValidator<AddDecisionRequest>
{
    public AddDecisionRequestValidator()
    {
        RuleFor(x => x.MadeBy).Must(a => a is "agent" or "human");
        RuleFor(x => x.Question).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Options).NotNull().Must(o => o.Count > 0);
        RuleForEach(x => x.Options).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Outcome).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Rationale).MaximumLength(5000).When(x => x.Rationale is not null);
    }
}

