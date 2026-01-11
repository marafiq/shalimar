import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { Button, Heading, Text, TextField } from '@react-spectrum/s2'
import { useMemo, useState } from 'react'
import { useStore } from '@tanstack/react-store'
import { crmStore, ensureAccounts, ensureTasks, ensureUsers, setTaskStatus } from '../../Client/store'
import type { TaskStatus } from '../../Client/crm/types'
import { PageSkeleton } from '../../Client/ui/Skeletons'
import { IconPlus } from '../../Client/ui/icons'
import { TaskDrawer } from './_components/TaskDrawer'

export const Route = createFileRoute('/tasks')({
    validateSearch: (search: Record<string, unknown>) => ({
        drawer: typeof search.drawer === 'string' ? search.drawer : undefined,
    }),
    loader: async () => {
        await Promise.all([ensureUsers(), ensureAccounts(), ensureTasks()])
        return null
    },
    pendingComponent: () => <PageSkeleton />,
    component: TasksRoute,
})

function TasksRoute() {
    const { users, accounts, tasks } = useStore(crmStore)
    const navigate = useNavigate()
    const { drawer } = Route.useSearch()
    const [query, setQuery] = useState('')

    const list = useMemo(() => Object.values(tasks), [tasks])
    const filtered = useMemo(() => {
        const q = query.trim().toLowerCase()
        if (!q) return list
        return list.filter((t) => t.title.toLowerCase().includes(q))
    }, [list, query])

    return (
        <div>
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                <div>
                    <Heading level={1}>Tasks</Heading>
                    <Text>
                        List view (store-backed). Create/edit happens in the right-side drawer to establish a consistent
                        “agent work surface”.
                    </Text>
                </div>
                <div className="flex gap-2">
                    <Button onPress={() => navigate({ to: '/tasks', search: { drawer: 'new' } })}>
                        <span className="inline-flex items-center gap-2">
                            <IconPlus className="h-4 w-4" /> New task
                        </span>
                    </Button>
                    <Button elementType={Link as any} to="/tasks/board" isQuiet>
                        Board view
                    </Button>
                </div>
            </div>

            <div className="mt-6 grid gap-4 lg:grid-cols-12">
                <section className="lg:col-span-8">
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                        <div className="max-w-sm">
                            <TextField label="Search" placeholder="Find tasks…" value={query} onChange={setQuery} />
                        </div>
                    </div>

                    <div className="mt-4 divide-y divide-black/10 rounded-lg border border-black/10 dark:divide-white/10 dark:border-white/10">
                        {filtered.map((t) => (
                            <div key={t.id} className="flex flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between">
                                <div className="min-w-0">
                                    <div className="truncate font-medium">{t.title}</div>
                                    <div className="text-sm opacity-70">
                                        {t.accountId ? accounts[t.accountId]?.name : 'No account'}
                                        {' • '}
                                        {t.assigneeId ? users.find((u) => u.id === t.assigneeId)?.name : 'Unassigned'}
                                        {' • '}
                                        {t.priority}
                                    </div>
                                </div>
                                <div className="flex flex-wrap gap-2">
                                    <StatusButton taskId={t.id} status={t.status} target="todo" onSet={setTaskStatus} />
                                    <StatusButton taskId={t.id} status={t.status} target="in_progress" onSet={setTaskStatus} />
                                    <StatusButton taskId={t.id} status={t.status} target="done" onSet={setTaskStatus} />
                                    <Button onPress={() => navigate({ to: '/tasks', search: { drawer: t.id } })} isQuiet>
                                        Open
                                    </Button>
                                </div>
                            </div>
                        ))}
                    </div>
                </section>

                <aside className="lg:col-span-4">
                    <div className="rounded-lg border border-black/10 p-4 dark:border-white/10">
                        <Heading level={3}>Notes</Heading>
                        <div className="mt-2 text-sm opacity-80">
                            This is intentionally “complex mock UI” to establish patterns for:
                            <ul className="mt-2 list-disc space-y-1 pl-5">
                                <li>Nested routes (`/tasks`, `/tasks/board`, `/tasks/$taskId`)</li>
                                <li>Store-first state management (TanStack Store)</li>
                                <li>Mock APIs for future generator replacement</li>
                            </ul>
                        </div>
                    </div>
                </aside>
            </div>

            <TaskDrawer
                open={typeof drawer === 'string' && drawer.length > 0}
                drawer={drawer}
                onClose={() => navigate({ to: '/tasks', search: {} })}
            />
        </div>
    )
}

function StatusButton(props: {
    taskId: string
    status: TaskStatus
    target: TaskStatus
    onSet: (id: string, status: TaskStatus) => Promise<void>
}) {
    const active = props.status === props.target
    const label = props.target === 'in_progress' ? 'In progress' : props.target
    return (
        <Button isQuiet={active} onPress={() => props.onSet(props.taskId, props.target)}>
            {label}
        </Button>
    )
}

