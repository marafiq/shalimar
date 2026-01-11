import { useStore } from '@tanstack/react-store'
import { Button, Heading, Text, TextField } from '@react-spectrum/s2'
import { useEffect, useMemo, useState } from 'react'
import {
    createTask,
    crmStore,
    ensureAccounts,
    ensureTasks,
    ensureUsers,
    loadTaskMessages,
    postTaskMessage,
    updateTask,
} from '../../../Client/store'
import type { TaskActor } from '../../../Client/crm/types'
import { DrawerSkeleton } from '../../../Client/ui/Skeletons'
import { IconX } from '../../../Client/ui/icons'
import { openModal, pushNotification, pushToast } from '../../../Client/ui/store'

export function TaskDrawer(props: { open: boolean; drawer: string | undefined; onClose: () => void }) {
    const state = useStore(crmStore)
    const [busy, setBusy] = useState(false)

    const mode = props.drawer === 'new' ? 'new' : 'edit'
    const taskId = mode === 'edit' ? props.drawer : undefined
    const task = taskId ? state.tasks[taskId] : undefined
    const messages = taskId ? state.messagesByTask[taskId] : undefined

    const title = mode === 'new' ? 'Create task' : task ? `Edit task` : 'Task'
    const [draftTitle, setDraftTitle] = useState('')
    const [message, setMessage] = useState('')

    useEffect(() => {
        if (!props.open) return
        void Promise.all([ensureUsers(), ensureAccounts(), ensureTasks()])
    }, [props.open])

    useEffect(() => {
        if (!props.open) return
        if (mode === 'new') {
            setDraftTitle('')
            return
        }
        setDraftTitle(task?.title ?? '')
        if (taskId) void loadTaskMessages(taskId)
    }, [props.open, mode, taskId, task?.title])

    const headerMeta = useMemo(() => {
        if (mode === 'new') return { subtitle: 'Create and assign work to the agent + humans.' }
        if (!task) return { subtitle: 'Loading…' }
        return { subtitle: `Status: ${task.status.replaceAll('_', ' ')} • Priority: ${task.priority}` }
    }, [mode, task])

    if (!props.open) return null

    const showSkeleton = mode === 'edit' && !task

    return (
        <div className="fixed inset-0 z-40" aria-label="Task drawer">
            <div className="absolute inset-0 bg-black/30 backdrop-blur-sm" onClick={props.onClose} />
            <aside className="absolute right-0 top-0 h-full w-[min(520px,100vw)] border-l border-black/10 bg-white shadow-xl dark:border-white/10 dark:bg-zinc-950">
                <div className="flex h-full flex-col">
                    <div className="flex items-start justify-between gap-3 border-b border-black/10 px-5 py-4 dark:border-white/10">
                        <div className="min-w-0">
                            <Heading level={3}>{title}</Heading>
                            <div className="mt-1 text-sm opacity-70">{headerMeta.subtitle}</div>
                        </div>
                        <Button isQuiet onPress={props.onClose} aria-label="Close drawer">
                            <IconX />
                        </Button>
                    </div>

                    <div className="flex-1 overflow-auto px-5 py-4">
                        {showSkeleton ? (
                            <DrawerSkeleton />
                        ) : (
                            <div className="space-y-6">
                                <section className="space-y-3">
                                    <TextField
                                        label="Title"
                                        placeholder="Write a clear outcome…"
                                        value={draftTitle}
                                        onChange={setDraftTitle}
                                    />
                                    <div className="grid gap-3 sm:grid-cols-2">
                                        <div className="rounded-xl border border-black/10 p-3 text-sm dark:border-white/10">
                                            <div className="text-xs font-semibold uppercase tracking-wide opacity-60">
                                                Mode
                                            </div>
                                            <div className="mt-1 font-medium">
                                                {mode === 'new' ? 'Create' : 'Edit'}
                                            </div>
                                            <div className="mt-1 opacity-70">
                                                This drawer is the consistent “agent work surface”.
                                            </div>
                                        </div>
                                        <div className="rounded-xl border border-black/10 p-3 text-sm dark:border-white/10">
                                            <div className="text-xs font-semibold uppercase tracking-wide opacity-60">
                                                Actions
                                            </div>
                                            <div className="mt-2 flex flex-wrap gap-2">
                                                <Button isQuiet onPress={() => openModal('Task mode', 'This will map to Shalimar component modes later.')}>
                                                    About modes
                                                </Button>
                                                <Button isQuiet onPress={() => openModal('Audit trail', 'Activity is currently mock/polled. Later: server-driven events.')}>
                                                    Audit
                                                </Button>
                                            </div>
                                        </div>
                                    </div>
                                </section>

                                {mode === 'edit' && taskId ? (
                                    <section className="space-y-3">
                                        <div className="flex items-end justify-between gap-3">
                                            <Heading level={4}>Thread</Heading>
                                            <Button isQuiet onPress={() => openModal('Thread', 'Agent/human messages are mock today; later: generated hooks + streaming.')}>
                                                How this works
                                            </Button>
                                        </div>

                                        <div className="space-y-2">
                                            {(messages ?? []).map((m) => (
                                                <MessageBubble key={m.id} actor={m.actor} author={m.author} body={m.body} ts={m.ts} />
                                            ))}
                                            {!messages ? <ThreadSkeleton /> : null}
                                        </div>

                                        <div className="rounded-xl border border-black/10 p-3 dark:border-white/10">
                                            <TextField
                                                label="Reply"
                                                placeholder="Ask the agent, add context, or send an update…"
                                                value={message}
                                                onChange={setMessage}
                                            />
                                            <div className="mt-3 flex flex-wrap gap-2">
                                                <Button
                                                    isQuiet
                                                    onPress={() => void sendMessage(taskId, 'human', message, setMessage, setBusy, busy)}
                                                >
                                                    Send as Human
                                                </Button>
                                                <Button
                                                    onPress={() => void sendMessage(taskId, 'agent', message, setMessage, setBusy, busy)}
                                                >
                                                    Send as Agent
                                                </Button>
                                            </div>
                                        </div>
                                    </section>
                                ) : (
                                    <section className="rounded-xl border border-black/10 p-3 text-sm opacity-70 dark:border-white/10">
                                        Thread will be available after the task is created.
                                    </section>
                                )}
                            </div>
                        )}
                    </div>

                    <div className="border-t border-black/10 px-5 py-4 dark:border-white/10">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                            <div className="text-xs opacity-60">Tip: press Esc to close (coming next)</div>
                            <div className="flex flex-wrap gap-2">
                                <Button isQuiet onPress={props.onClose}>
                                    Cancel
                                </Button>
                                <Button
                                    onPress={() => void save(mode, taskId, draftTitle, props.onClose, setBusy, busy)}
                                    isDisabled={busy}
                                >
                                    {mode === 'new' ? 'Create' : 'Save'}
                                </Button>
                            </div>
                        </div>
                    </div>
                </div>
            </aside>
        </div>
    )

    async function save(
        mode: 'new' | 'edit',
        taskId: string | undefined,
        title: string,
        close: () => void,
        setBusy: (v: boolean) => void,
        busy: boolean,
    ) {
        const trimmed = title.trim()
        if (!trimmed || busy) return
        setBusy(true)
        try {
            if (mode === 'new') {
                const created = await createTask(trimmed)
                pushToast({ tone: 'success', title: 'Task created', message: created.title })
                pushNotification({ title: 'New task created', message: created.title, href: `/tasks?drawer=${created.id}` })
            } else if (taskId) {
                await updateTask(taskId, { title: trimmed })
                pushToast({ tone: 'success', title: 'Task updated' })
            }
            close()
        } finally {
            setBusy(false)
        }
    }
}

async function sendMessage(
    taskId: string,
    actor: TaskActor,
    message: string,
    setMessage: (v: string) => void,
    setBusy: (v: boolean) => void,
    busy: boolean,
) {
    const body = message.trim()
    if (!body || busy) return
    setBusy(true)
    try {
        const msg = await postTaskMessage(taskId, actor, body)
        pushToast({
            tone: actor === 'agent' ? 'neutral' : 'success',
            title: actor === 'agent' ? 'Agent replied' : 'Message sent',
            message: msg.body,
        })
        setMessage('')
    } finally {
        setBusy(false)
    }
}

function MessageBubble(props: { actor: TaskActor; author: string; body: string; ts: string }) {
    const mine = props.actor === 'human'
    return (
        <div className={mine ? 'flex justify-end' : 'flex justify-start'}>
            <div
                className={[
                    'max-w-[90%] rounded-2xl border px-3 py-2 text-sm',
                    mine
                        ? 'border-blue-500/30 bg-blue-500/10'
                        : 'border-black/10 bg-black/5 dark:border-white/10 dark:bg-white/10',
                ].join(' ')}
            >
                <div className="flex items-baseline justify-between gap-2">
                    <div className="text-xs font-semibold opacity-70">{props.author}</div>
                    <div className="text-[10px] opacity-50">{new Date(props.ts).toLocaleTimeString()}</div>
                </div>
                <div className="mt-1 whitespace-pre-wrap">{props.body}</div>
            </div>
        </div>
    )
}

function ThreadSkeleton() {
    return (
        <div className="space-y-2" data-testid="thread-skeleton">
            <div className="max-w-[85%] rounded-2xl border border-black/10 bg-black/5 p-3 dark:border-white/10 dark:bg-white/10">
                <div className="h-3 w-32 rounded bg-black/10 dark:bg-white/10" />
                <div className="mt-2 h-4 w-72 max-w-[60vw] rounded bg-black/10 dark:bg-white/10" />
            </div>
            <div className="ml-auto max-w-[85%] rounded-2xl border border-blue-500/30 bg-blue-500/10 p-3">
                <div className="h-3 w-24 rounded bg-black/10 dark:bg-white/10" />
                <div className="mt-2 h-4 w-64 max-w-[55vw] rounded bg-black/10 dark:bg-white/10" />
            </div>
        </div>
    )
}

