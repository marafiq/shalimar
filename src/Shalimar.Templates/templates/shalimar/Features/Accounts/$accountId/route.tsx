import { createFileRoute, Link } from '@tanstack/react-router'
import { Button, Heading, Text } from '@react-spectrum/s2'
import { useMemo } from 'react'
import { useStore } from '@tanstack/react-store'
import { crmStore, ensureAccounts, ensureUsers, loadContacts } from '../../Crm/store'
import { PageSkeleton } from '../../App/ui/Skeletons'

export const Route = createFileRoute('/accounts/$accountId')({
    validateSearch: (search: Record<string, unknown>) => ({
        skeleton: search.skeleton === '1' || search.skeleton === 'true',
    }),
    loader: async ({ params }) => {
        await Promise.all([ensureUsers(), ensureAccounts(), loadContacts(params.accountId)])
        return null
    },
    pendingComponent: () => <PageSkeleton />,
    component: AccountDetailRoute,
})

function AccountDetailRoute() {
    const { accountId } = Route.useParams()
    const { skeleton } = Route.useSearch()
    const { accounts, users, contactsByAccount } = useStore(crmStore)
    const account = accounts[accountId]
    const contacts = contactsByAccount[accountId] ?? []

    if (skeleton) return <PageSkeleton />

    const owner = useMemo(() => (account ? users.find((u) => u.id === account.ownerId) : undefined), [account, users])

    if (!account) {
        return (
            <div>
                <Heading level={1}>Account not found</Heading>
                <Button elementType={Link as any} to="/accounts" isQuiet>
                    Back to accounts
                </Button>
            </div>
        )
    }

    return (
        <div className="max-w-4xl">
            <div className="flex items-start justify-between gap-4">
                <div className="min-w-0">
                    <Heading level={1}>{account.name}</Heading>
                    <div className="mt-2 text-sm opacity-80">
                        {account.industry} • {account.tier} • Owner: {owner?.name ?? '—'}
                    </div>
                </div>
                <Button elementType={Link as any} to="/accounts" isQuiet>
                    Back
                </Button>
            </div>

            <div className="mt-6 rounded-lg border border-black/10 p-4 dark:border-white/10">
                <Heading level={3}>Contacts</Heading>
                <div className="mt-3 divide-y divide-black/10 dark:divide-white/10">
                    {contacts.map((c) => (
                        <div key={c.id} className="py-3">
                            <div className="font-medium">{c.name}</div>
                            <div className="text-sm opacity-80">
                                {c.title} • {c.email}
                            </div>
                        </div>
                    ))}
                    {contacts.length === 0 && <Text>No contacts yet.</Text>}
                </div>
            </div>
        </div>
    )
}

