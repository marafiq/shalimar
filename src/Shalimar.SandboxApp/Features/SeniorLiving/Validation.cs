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

public sealed class CreateObservationRequestValidator : AbstractValidator<CreateObservationRequest>
{
    public CreateObservationRequestValidator()
    {
        RuleFor(x => x.Kind).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Note).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ResidentId).NotEmpty().MaximumLength(40);
    }
}

public sealed class UpdateObservationRequestValidator : AbstractValidator<UpdateObservationRequest>
{
    public UpdateObservationRequestValidator()
    {
        RuleFor(x => x.Kind).MaximumLength(80);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.ResidentId).MaximumLength(40);
    }
}

public sealed class CreateMedPassScheduleRequestValidator : AbstractValidator<CreateMedPassScheduleRequest>
{
    public CreateMedPassScheduleRequestValidator()
    {
        RuleFor(x => x.ResidentId).NotEmpty().MaximumLength(40);
        RuleFor(x => x.MedId).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Time).NotEmpty().MaximumLength(10); // "HH:mm"
        RuleFor(x => x.Frequency).NotEmpty().MaximumLength(40);
    }
}

public sealed class UpdateMedPassScheduleRequestValidator : AbstractValidator<UpdateMedPassScheduleRequest>
{
    public UpdateMedPassScheduleRequestValidator()
    {
        RuleFor(x => x.ResidentId).MaximumLength(40);
        RuleFor(x => x.MedId).MaximumLength(40);
        RuleFor(x => x.Time).MaximumLength(10);
        RuleFor(x => x.Frequency).MaximumLength(40);
    }
}

public sealed class PassMedRequestValidator : AbstractValidator<PassMedRequest>
{
    public PassMedRequestValidator()
    {
        RuleFor(x => x.ScheduleId).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Outcome).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

