import { Outlet, createFileRoute, Link, useNavigate, useRouterState } from '@tanstack/react-router'
import { Button, Heading, Text, TextField } from '@react-spectrum/s2'
import { useMemo, useState } from 'react'
import { useStore } from '@tanstack/react-store'
import { crmStore, ensureAccounts, ensureEpics, ensureTasks, ensureUsers, setTaskStatus } from '../Crm/store'
import type { TaskStatus } from '../Crm/types'
import { PageSkeleton } from '../App/ui/Skeletons'
import { IconPlus } from '../App/ui/icons'
import { TaskDrawer } from './_components/TaskDrawer'

export const Route = createFileRoute('/tasks')({
    validateSearch: (search: Record<string, unknown>) => ({
        drawer: typeof search.drawer === 'string' ? search.drawer : undefined,
        epicId: typeof search.epicId === 'string' ? search.epicId : undefined,
        skeleton: search.skeleton === '1' || search.skeleton === 'true',
        drawerSkeleton: search.drawerSkeleton === '1' || search.drawerSkeleton === 'true',
        threadSkeleton: search.threadSkeleton === '1' || search.threadSkeleton === 'true',
    }),
    loader: async () => {
        await Promise.all([ensureUsers(), ensureAccounts(), ensureEpics(), ensureTasks()])
        return null
    },
    pendingComponent: () => <PageSkeleton />,
    component: TasksRoute,
})

function TasksRoute() {
    const { users, accounts, tasks } = useStore(crmStore)
    const navigate = useNavigate()
    const { drawer, epicId, skeleton, drawerSkeleton, threadSkeleton } = Route.useSearch()
    const pathname = useRouterState({ select: (s) => s.location.pathname })
    const [query, setQuery] = useState('')

    if (skeleton) return <PageSkeleton />

    const isTasksIndex = pathname === '/tasks'

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

            {isTasksIndex ? (
                <div className="mt-6 grid gap-4 lg:grid-cols-12">
                    <section className="lg:col-span-8">
                        <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                            <div className="max-w-sm">
                                <TextField label="Search" placeholder="Find tasks…" value={query} onChange={setQuery} />
                            </div>
                        </div>

                        <div className="mt-4 divide-y divide-black/10 rounded-lg border border-black/10 dark:divide-white/10 dark:border-white/10">
                            {filtered.map((t) => (
                                <div
                                    key={t.id}
                                    className="flex flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between"
                                >
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
                                        <StatusButton
                                            taskId={t.id}
                                            status={t.status}
                                            target="in_progress"
                                            onSet={setTaskStatus}
                                        />
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
                                    <li>Server-truth + validators (Shalimar/“Inertia-style”)</li>
                                </ul>
                            </div>
                        </div>
                    </aside>
                </div>
            ) : (
                <div className="mt-6">
                    <Outlet />
                </div>
            )}

            {isTasksIndex ? (
                <TaskDrawer
                    open={typeof drawer === 'string' && drawer.length > 0}
                    drawer={drawer}
                    prefillEpicId={epicId}
                    forceSkeleton={drawerSkeleton}
                    forceThreadSkeleton={threadSkeleton}
                    onClose={() => navigate({ to: '/tasks', search: {} })}
                />
            ) : null}
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

