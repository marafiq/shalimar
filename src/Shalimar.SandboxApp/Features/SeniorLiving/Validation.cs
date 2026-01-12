namespace Shalimar.SandboxApp.Features.SeniorLiving;

using FluentValidation;

public sealed class CreateResidentRequestValidator : AbstractValidator<CreateResidentRequest>
{
    public CreateResidentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Room).NotEmpty().MaximumLength(20);
        RuleFor(x => x.CareLevel).NotEmpty().MaximumLength(40);
    }
}

public sealed class UpdateResidentRequestValidator : AbstractValidator<UpdateResidentRequest>
{
    public UpdateResidentRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(120);
        RuleFor(x => x.Room).MaximumLength(20);
        RuleFor(x => x.CareLevel).MaximumLength(40);
    }
}

public sealed class CreateIncidentRequestValidator : AbstractValidator<CreateIncidentRequest>
{
    public CreateIncidentRequestValidator()
    {
        RuleFor(x => x.Kind).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Status).NotEmpty().MaximumLength(40);
        RuleFor(x => x.ResidentId).NotEmpty().MaximumLength(40);
    }
}

public sealed class UpdateIncidentRequestValidator : AbstractValidator<UpdateIncidentRequest>
{
    public UpdateIncidentRequestValidator()
    {
        RuleFor(x => x.Kind).MaximumLength(80);
        RuleFor(x => x.Summary).MaximumLength(500);
        RuleFor(x => x.Status).MaximumLength(40);
        RuleFor(x => x.ResidentId).MaximumLength(40);
    }
}

