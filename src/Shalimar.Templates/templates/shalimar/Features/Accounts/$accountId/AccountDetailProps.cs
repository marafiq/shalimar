using ShalimarApp.Features.Crm;

namespace ShalimarApp.Features.Accounts;

public sealed record AccountDetailProps(
    string Message,
    string AccountId,
    AccountDto? Account,
    IReadOnlyList<ContactDto> Contacts,
    UserDto? Owner);

