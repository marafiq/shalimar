import { createFileRoute, Link } from '@tanstack/react-router'
import { Button, Heading, Text } from '@react-spectrum/s2'
import { useMemo } from 'react'
import { useStore } from '@tanstack/react-store'
import { crmStore, ensureAccounts, ensureTasks, ensureUsers, setTaskStatus } from '../../../Client/store'
import type { TaskStatus } from '../../../Client/crm/types'

export const Route = createFileRoute('/tasks/$taskId')({
    loader: async () => {
        await Promise.all([ensureUsers(), ensureAccounts(), ensureTasks()])
        return null
    },
    component: TaskDetailRoute,
})

function TaskDetailRoute() {
    const { taskId } = Route.useParams()
    const { tasks, users, accounts } = useStore(crmStore)
    const task = tasks[taskId]

    const assignee = useMemo(() => (task?.assigneeId ? users.find((u) => u.id === task.assigneeId) : undefined), [
        task?.assigneeId,
        users,
    ])

    if (!task) {
        return (
            <div>
                <Heading level={1}>Task not found</Heading>
                <Button elementType={Link as any} to="/tasks" isQuiet>
                    Back to tasks
                </Button>
            </div>
        )
    }

    return (
        <div className="max-w-3xl">
            <div className="flex items-start justify-between gap-4">
                <div className="min-w-0">
                    <Heading level={1}>{task.title}</Heading>
                    <div className="mt-2 text-sm opacity-80">
                        <span>Status: {task.status.replaceAll('_', ' ')}</span>
                        {' • '}
                        <span>Priority: {task.priority}</span>
                    </div>
                    <div className="mt-1 text-sm opacity-70">
                        <span>Account: {task.accountId ? accounts[task.accountId]?.name : 'None'}</span>
                        {' • '}
                        <span>Assignee: {assignee?.name ?? 'Unassigned'}</span>
                    </div>
                </div>
                <Button elementType={Link as any} to="/tasks" isQuiet>
                    Back
                </Button>
            </div>

            <div className="mt-6 rounded-lg border border-black/10 p-4 dark:border-white/10">
                <Heading level={3}>Actions</Heading>
                <div className="mt-3 flex flex-wrap gap-2">
                    <StatusAction taskId={task.id} status={task.status} target="todo" />
                    <StatusAction taskId={task.id} status={task.status} target="in_progress" />
                    <StatusAction taskId={task.id} status={task.status} target="blocked" />
                    <StatusAction taskId={task.id} status={task.status} target="done" />
                    <Button elementType={Link as any} to="/tasks/board" isQuiet>
                        View board
                    </Button>
                </div>
            </div>
        </div>
    )
}

function StatusAction(props: { taskId: string; status: TaskStatus; target: TaskStatus }) {
    const active = props.status === props.target
    const label = props.target === 'in_progress' ? 'In progress' : props.target.replaceAll('_', ' ')
    return (
        <Button isQuiet={active} onPress={() => setTaskStatus(props.taskId, props.target)}>
            {label}
        </Button>
    )
}

