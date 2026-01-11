import { createFileRoute, Link } from '@tanstack/react-router'
import { Button, Card, CardPreview, Heading, Text } from '@react-spectrum/s2'
import { useMemo } from 'react'
import { useStore } from '@tanstack/react-store'
import { crmStore, ensureAccounts, ensureTasks, ensureUsers, setTaskStatus } from '../../../Client/store'
import type { Task, TaskStatus } from '../../../Client/crm/types'

export const Route = createFileRoute('/tasks/board')({
    loader: async () => {
        await Promise.all([ensureUsers(), ensureAccounts(), ensureTasks()])
        return null
    },
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
    const list = useMemo(() => Object.values(tasks), [tasks])

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
                    <Text>Kanban-style view (mock, store-backed).</Text>
                </div>
                <Button elementType={Link as any} to="/tasks" isQuiet>
                    List view
                </Button>
            </div>

            <div className="mt-6 grid gap-4 lg:grid-cols-5">
                {columns.map((c) => (
                    <div key={c.status} className="min-w-0">
                        <div className="mb-2 text-sm font-medium opacity-80">{c.title}</div>
                        <div className="space-y-3">
                            {byStatus[c.status].map((t) => (
                                <Card key={t.id}>
                                    <CardPreview>
                                        <div className="p-3">
                                            <div className="truncate font-medium">{t.title}</div>
                                            <div className="mt-2 flex flex-wrap gap-2">
                                                <Button
                                                    isQuiet
                                                    onPress={() => setTaskStatus(t.id, c.status === 'done' ? 'todo' : 'done')}
                                                >
                                                    {c.status === 'done' ? 'Reopen' : 'Done'}
                                                </Button>
                                                <Button elementType={Link as any} to={`/tasks/${t.id}`} isQuiet>
                                                    Open
                                                </Button>
                                            </div>
                                        </div>
                                    </CardPreview>
                                </Card>
                            ))}
                        </div>
                    </div>
                ))}
            </div>
        </div>
    )
}

