import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { Button, Heading, Text } from '@react-spectrum/s2'
import { useMemo, useState } from 'react'
import { useStore } from '@tanstack/react-store'
import { crmStore, ensureAccounts, ensureTasks, ensureUsers, setTaskStatus } from '../../../Client/store'
import type { Task, TaskStatus } from '../../../Client/crm/types'
import { PageSkeleton } from '../../../Client/ui/Skeletons'
import { IconPlus } from '../../../Client/ui/icons'
import { TaskDrawer } from '../_components/TaskDrawer'

export const Route = createFileRoute('/tasks/board')({
    validateSearch: (search: Record<string, unknown>) => ({
        drawer: typeof search.drawer === 'string' ? search.drawer : undefined,
        skeleton: search.skeleton === '1' || search.skeleton === 'true',
        drawerSkeleton: search.drawerSkeleton === '1' || search.drawerSkeleton === 'true',
        threadSkeleton: search.threadSkeleton === '1' || search.threadSkeleton === 'true',
    }),
    loader: async () => {
        await Promise.all([ensureUsers(), ensureAccounts(), ensureTasks()])
        return null
    },
    pendingComponent: () => <PageSkeleton />,
    component: BoardRoute,
})

const columns: { status: TaskStatus; title: string }[] = [
    { status: 'backlog', title: 'Backlog' },
    { status: 'todo', title: 'To do' },
    { status: 'in_progress', title: 'In progress' },
    { status: 'blocked', title: 'Blocked' },
    { status: 'done', title: 'Done' },
]

function BoardRoute() {
    const { tasks } = useStore(crmStore)
    const navigate = useNavigate()
    const { drawer, skeleton, drawerSkeleton, threadSkeleton } = Route.useSearch()
    const [focus, setFocus] = useState<'all' | 'mine'>('all')
    const list = useMemo(() => Object.values(tasks), [tasks])

    if (skeleton) return <PageSkeleton />

    const byStatus = useMemo(() => {
        const map: Record<TaskStatus, Task[]> = {
            backlog: [],
            todo: [],
            in_progress: [],
            blocked: [],
            done: [],
        }
        for (const t of list) map[t.status].push(t)
        return map
    }, [list])

    return (
        <div>
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                <div>
                    <Heading level={1}>Board</Heading>
                    <Text>
                        Kanban-style board with quick actions. This is intentionally “busy” so we can lock in patterns
                        before generation.
                    </Text>
                </div>
                <div className="flex flex-wrap gap-2">
                    <Button onPress={() => navigate({ to: '/tasks/board', search: { drawer: 'new' } })}>
                        <span className="inline-flex items-center gap-2">
                            <IconPlus className="h-4 w-4" /> New task
                        </span>
                    </Button>
                    <Button elementType={Link as any} to="/tasks" isQuiet>
                        List view
                    </Button>
                </div>
            </div>

            <div className="mt-6 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-black/10 bg-black/5 p-3 text-sm dark:border-white/10 dark:bg-white/10">
                <div className="flex flex-wrap gap-2">
                    <Button isQuiet={focus !== 'all'} onPress={() => setFocus('all')}>
                        All work
                    </Button>
                    <Button isQuiet={focus !== 'mine'} onPress={() => setFocus('mine')}>
                        My queue
                    </Button>
                </div>
                <div className="text-xs opacity-70">WIP guidance: keep “In progress” under 3 per agent.</div>
            </div>

            <div className="mt-4 grid gap-4 lg:grid-cols-5">
                {columns.map((c) => (
                    <div key={c.status} className="min-w-0">
                        <div className="mb-2 flex items-center justify-between gap-2">
                            <div className="text-sm font-semibold opacity-80">{c.title}</div>
                            <div className="rounded-full border border-black/10 bg-white/70 px-2 py-0.5 text-xs tabular-nums dark:border-white/10 dark:bg-zinc-950/40">
                                {byStatus[c.status].length}
                            </div>
                        </div>
                        <div className="space-y-3">
                            {byStatus[c.status]
                                .filter((t) => (focus === 'mine' ? t.assigneeId === 'u_1' : true))
                                .map((t) => (
                                    <TaskCard
                                        key={t.id}
                                        task={t}
                                        onOpen={() => navigate({ to: '/tasks/board', search: { drawer: t.id } })}
                                        onToggleDone={() => setTaskStatus(t.id, c.status === 'done' ? 'todo' : 'done')}
                                    />
                                ))}
                        </div>
                    </div>
                ))}
            </div>

            <TaskDrawer
                open={typeof drawer === 'string' && drawer.length > 0}
                drawer={drawer}
                forceSkeleton={drawerSkeleton}
                forceThreadSkeleton={threadSkeleton}
                onClose={() => navigate({ to: '/tasks/board', search: {} })}
            />
        </div>
    )
}

function TaskCard(props: { task: Task; onOpen: () => void; onToggleDone: () => void }) {
    const priority =
        props.task.priority === 'high'
            ? 'bg-red-500/10 text-red-700 dark:text-red-300'
            : props.task.priority === 'medium'
              ? 'bg-amber-500/10 text-amber-700 dark:text-amber-300'
              : 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300'

    return (
        <div className="group rounded-2xl border border-black/10 bg-white p-3 shadow-sm dark:border-white/10 dark:bg-zinc-950">
            <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                    <div className="truncate text-sm font-semibold">{props.task.title}</div>
                    <div className="mt-2 flex flex-wrap gap-2 text-xs">
                        <span className={`rounded-full px-2 py-0.5 ${priority}`}>{props.task.priority}</span>
                        <span className="rounded-full border border-black/10 bg-black/5 px-2 py-0.5 opacity-80 dark:border-white/10 dark:bg-white/10">
                            {props.task.status.replaceAll('_', ' ')}
                        </span>
                    </div>
                </div>
                <Button isQuiet onPress={props.onOpen}>
                    Open
                </Button>
            </div>
            <div className="mt-3 flex flex-wrap gap-2">
                <Button isQuiet onPress={props.onToggleDone}>
                    {props.task.status === 'done' ? 'Reopen' : 'Done'}
                </Button>
                <Button isQuiet onPress={() => setTaskStatus(props.task.id, 'in_progress')}>
                    In progress
                </Button>
            </div>
        </div>
    )
}

