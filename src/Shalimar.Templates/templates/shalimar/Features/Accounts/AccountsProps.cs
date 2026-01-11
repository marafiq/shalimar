using ShalimarApp.Features.Crm;

namespace ShalimarApp.Features.Accounts;

public sealed record AccountsProps(
    string Message,
    CrmSnapshot Crm);

