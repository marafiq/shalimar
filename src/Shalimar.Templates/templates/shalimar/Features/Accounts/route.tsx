import { createFileRoute, Link } from '@tanstack/react-router'
import { Button, Heading, Text } from '@react-spectrum/s2'
import { useMemo } from 'react'
import { useStore } from '@tanstack/react-store'
import { crmStore, ensureAccounts, ensureUsers } from '../../Client/store'

export const Route = createFileRoute('/accounts')({
    loader: async () => {
        await Promise.all([ensureUsers(), ensureAccounts()])
        return null
    },
    component: AccountsRoute,
})

function AccountsRoute() {
    const { accounts, users } = useStore(crmStore)
    const list = useMemo(() => Object.values(accounts), [accounts])

    return (
        <div>
            <Heading level={1}>Accounts</Heading>
            <Text>Mock account list with nested detail route.</Text>

            <div className="mt-6 overflow-hidden rounded-lg border border-black/10 dark:border-white/10">
                <div className="grid grid-cols-12 gap-2 border-b border-black/10 bg-black/5 px-4 py-2 text-xs font-medium dark:border-white/10 dark:bg-white/10">
                    <div className="col-span-5">Name</div>
                    <div className="col-span-3">Industry</div>
                    <div className="col-span-2">Tier</div>
                    <div className="col-span-2">Owner</div>
                </div>
                <div className="divide-y divide-black/10 dark:divide-white/10">
                    {list.map((a) => (
                        <div key={a.id} className="grid grid-cols-12 items-center gap-2 px-4 py-3">
                            <div className="col-span-5 min-w-0">
                                <Button elementType={Link as any} to={`/accounts/${a.id}`} isQuiet>
                                    <span className="truncate">{a.name}</span>
                                </Button>
                            </div>
                            <div className="col-span-3 text-sm opacity-80">{a.industry}</div>
                            <div className="col-span-2 text-sm opacity-80">{a.tier}</div>
                            <div className="col-span-2 text-sm opacity-80">
                                {users.find((u) => u.id === a.ownerId)?.name ?? '—'}
                            </div>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    )
}

