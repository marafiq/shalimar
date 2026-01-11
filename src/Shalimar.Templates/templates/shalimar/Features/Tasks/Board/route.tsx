import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { Button, Heading, Text } from '@react-spectrum/s2'
import { useMemo, useState } from 'react'
import { useStore } from '@tanstack/react-store'
import { crmStore, ensureAccounts, ensureEpics, ensureTasks, ensureUsers, moveTask } from '../../Crm/store'
import type { Id, Task, TaskStatus } from '../../Crm/types'
import { PageSkeleton } from '../../App/ui/Skeletons'
import { IconPlus } from '../../App/ui/icons'
import { TaskDrawer } from '../_components/TaskDrawer'

export const Route = createFileRoute('/tasks/board')({
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
    const { tasks, epics } = useStore(crmStore)
    const navigate = useNavigate()
    const { drawer, epicId, skeleton, drawerSkeleton, threadSkeleton } = Route.useSearch()
    const [focus, setFocus] = useState<'all' | 'mine'>('all')
    const [drag, setDrag] = useState<null | { taskId: Id }> (null)
    const [over, setOver] = useState<null | { epicId: Id | 'none'; status: TaskStatus }>(null)
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

    const lanes = useMemo(() => {
        const ordered = Object.values(epics)
        const noneLane: { id: 'none'; title: string } = { id: 'none', title: 'No epic' }
        return [noneLane, ...ordered.map((e) => ({ id: e.id as Id, title: e.title }))]
    }, [epics])

    return (
        <div>
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                <div>
                    <Heading level={1}>Board</Heading>
                    <Text>
                        Swimlanes by epic. Drag tasks across statuses (and between epics) to model real agent + human
                        collaboration loops.
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

            <div className="mt-4 space-y-6">
                {lanes.map((lane) => (
                    <section key={lane.id} className="rounded-2xl border border-black/10 dark:border-white/10">
                        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-black/10 bg-black/5 px-4 py-3 dark:border-white/10 dark:bg-white/10">
                            <div className="min-w-0">
                                <div className="truncate text-sm font-semibold">{lane.title}</div>
                                <div className="mt-0.5 text-xs opacity-70">
                                    {lane.id === 'none' ? 'Unscoped work' : 'Epic workstream'}
                                </div>
                            </div>
                            <div className="flex flex-wrap gap-2">
                                <Button
                                    isQuiet
                                    onPress={() =>
                                        navigate({ to: '/tasks/board', search: { drawer: 'new', epicId: lane.id === 'none' ? undefined : lane.id } as any })
                                    }
                                >
                                    New in epic
                                </Button>
                            </div>
                        </div>

                        <div className="grid gap-4 p-4 lg:grid-cols-5">
                            {columns.map((c) => (
                                <SwimlaneColumn
                                    key={c.status}
                                    title={c.title}
                                    count={laneTasks(tasks, lane.id, c.status, focus).length}
                                    status={c.status}
                                    laneEpicId={lane.id}
                                    drag={drag}
                                    over={over}
                                    onOver={setOver}
                                    onDrop={async (taskId) => {
                                        await moveTask(taskId, c.status, lane.id === 'none' ? undefined : (lane.id as Id))
                                        setDrag(null)
                                        setOver(null)
                                    }}
                                >
                                    {laneTasks(tasks, lane.id, c.status, focus).map((t) => (
                                        <TaskCard
                                            key={t.id}
                                            task={t}
                                            onOpen={() => navigate({ to: '/tasks/board', search: { drawer: t.id } })}
                                            onDragStart={() => setDrag({ taskId: t.id })}
                                            onDragEnd={() => {
                                                setDrag(null)
                                                setOver(null)
                                            }}
                                        />
                                    ))}
                                </SwimlaneColumn>
                            ))}
                        </div>
                    </section>
                ))}
            </div>

            <TaskDrawer
                open={typeof drawer === 'string' && drawer.length > 0}
                drawer={drawer}
                prefillEpicId={epicId}
                forceSkeleton={drawerSkeleton}
                forceThreadSkeleton={threadSkeleton}
                onClose={() => navigate({ to: '/tasks/board', search: {} })}
            />
        </div>
    )
}

function laneTasks(tasks: Record<Id, Task>, laneEpicId: Id | 'none', status: TaskStatus, focus: 'all' | 'mine') {
    return Object.values(tasks)
        .filter((t) => t.status === status)
        .filter((t) => (laneEpicId === 'none' ? !t.epicId : t.epicId === laneEpicId))
        .filter((t) => (focus === 'mine' ? t.assigneeId === 'u_1' : true))
        .toSorted((a, b) => b.updatedAt.localeCompare(a.updatedAt))
}

function SwimlaneColumn(props: {
    title: string
    count: number
    status: TaskStatus
    laneEpicId: Id | 'none'
    drag: null | { taskId: Id }
    over: null | { epicId: Id | 'none'; status: TaskStatus }
    onOver: (v: null | { epicId: Id | 'none'; status: TaskStatus }) => void
    onDrop: (taskId: Id) => Promise<void>
    children: React.ReactNode
}) {
    const active = props.over?.status === props.status && props.over?.epicId === props.laneEpicId
    return (
        <div
            className={[
                'min-w-0 rounded-2xl border p-3',
                active ? 'border-blue-500/40 bg-blue-500/5' : 'border-black/10 dark:border-white/10',
            ].join(' ')}
            onDragOver={(e) => {
                if (!props.drag) return
                e.preventDefault()
                props.onOver({ epicId: props.laneEpicId, status: props.status })
            }}
            onDrop={async (e) => {
                e.preventDefault()
                const taskId = e.dataTransfer.getData('text/plain') as Id
                if (!taskId) return
                await props.onDrop(taskId)
            }}
        >
            <div className="mb-2 flex items-center justify-between gap-2">
                <div className="text-sm font-semibold opacity-80">{props.title}</div>
                <div className="rounded-full border border-black/10 bg-white/70 px-2 py-0.5 text-xs tabular-nums dark:border-white/10 dark:bg-zinc-950/40">
                    {props.count}
                </div>
            </div>
            <div className="space-y-3">{props.children}</div>
        </div>
    )
}

function TaskCard(props: { task: Task; onOpen: () => void; onDragStart: () => void; onDragEnd: () => void }) {
    const priority =
        props.task.priority === 'high'
            ? 'bg-red-500/10 text-red-700 dark:text-red-300'
            : props.task.priority === 'medium'
              ? 'bg-amber-500/10 text-amber-700 dark:text-amber-300'
              : 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300'

    return (
        <div
            className="group rounded-2xl border border-black/10 bg-white p-3 shadow-sm dark:border-white/10 dark:bg-zinc-950"
            draggable
            onDragStart={(e) => {
                e.dataTransfer.setData('text/plain', props.task.id)
                e.dataTransfer.effectAllowed = 'move'
                props.onDragStart()
            }}
            onDragEnd={props.onDragEnd}
        >
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
            <div className="mt-3 text-xs opacity-60">Drag to move</div>
        </div>
    )
}

