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

