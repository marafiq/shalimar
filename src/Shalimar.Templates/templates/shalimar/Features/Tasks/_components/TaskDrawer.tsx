import { useStore } from '@tanstack/react-store'
import { Button, Heading, Text, TextField } from '@react-spectrum/s2'
import { useEffect, useMemo, useState } from 'react'
import {
    addTaskArtifact,
    addTaskDecision,
    createTask,
    crmStore,
    ensureAccounts,
    ensureEpics,
    ensureTasks,
    ensureUsers,
    loadTaskArtifacts,
    loadTaskDecisions,
    loadTaskMessages,
    postTaskMessage,
    updateTask,
} from '../../Crm/store'
import type { ArtifactKind, Id, TaskActor, TaskPriority } from '../../Crm/types'
import { DrawerSkeleton } from '../../App/ui/Skeletons'
import { IconX } from '../../App/ui/icons'
import { openModal, pushNotification, pushToast } from '../../App/ui/store'

export function TaskDrawer(props: {
    open: boolean
    drawer: string | undefined
    prefillEpicId?: string
    forceSkeleton?: boolean
    forceThreadSkeleton?: boolean
    onClose: () => void
}) {
    const state = useStore(crmStore)
    const [busy, setBusy] = useState(false)

    const mode = props.drawer === 'new' ? 'new' : 'edit'
    const taskId = mode === 'edit' ? props.drawer : undefined
    const task = taskId ? state.tasks[taskId] : undefined
    const messages = taskId ? state.messagesByTask[taskId] : undefined
    const artifacts = taskId ? state.artifactsByTask[taskId] : undefined
    const decisions = taskId ? state.decisionsByTask[taskId] : undefined

    const title = mode === 'new' ? 'Create task' : task ? `Edit task` : 'Task'
    const [draftTitle, setDraftTitle] = useState('')
    const [message, setMessage] = useState('')
    const [tab, setTab] = useState<'details' | 'thread' | 'decisions' | 'artifacts'>('details')

    const [draftEpicId, setDraftEpicId] = useState<string>('')
    const [draftPriority, setDraftPriority] = useState<TaskPriority>('medium')
    const [draftAssigneeId, setDraftAssigneeId] = useState<string>('')
    const [draftDueDate, setDraftDueDate] = useState<string>('') // YYYY-MM-DD
    const [draftEstimate, setDraftEstimate] = useState<string>('') // minutes
    const [draftCollaborators, setDraftCollaborators] = useState<Record<Id, boolean>>({})

    const [artifactKind, setArtifactKind] = useState<ArtifactKind>('notes')
    const [artifactTitle, setArtifactTitle] = useState('')
    const [artifactContent, setArtifactContent] = useState('')

    const [decisionQuestion, setDecisionQuestion] = useState('')
    const [decisionOptions, setDecisionOptions] = useState('') // newline separated
    const [decisionOutcome, setDecisionOutcome] = useState('')
    const [decisionRationale, setDecisionRationale] = useState('')

    useEffect(() => {
        if (!props.open) return
        void Promise.all([ensureUsers(), ensureAccounts(), ensureEpics(), ensureTasks()])
    }, [props.open])

    useEffect(() => {
        if (!props.open) return
        if (mode === 'new') {
            setDraftTitle('')
            setTab('details')
            setDraftEpicId(props.prefillEpicId ?? '')
            setDraftPriority('medium')
            setDraftAssigneeId('')
            setDraftDueDate('')
            setDraftEstimate('')
            setDraftCollaborators({})
            return
        }
        setDraftTitle(task?.title ?? '')
        setTab('details')
        setDraftEpicId(task?.epicId ?? '')
        setDraftPriority(task?.priority ?? 'medium')
        setDraftAssigneeId(task?.assigneeId ?? '')
        setDraftDueDate(task?.dueAt ? task.dueAt.slice(0, 10) : '')
        setDraftEstimate(typeof task?.estimateMinutes === 'number' ? String(task.estimateMinutes) : '')
        setDraftCollaborators(Object.fromEntries((task?.collaboratorIds ?? []).map((id) => [id, true])))
        if (taskId) void loadTaskMessages(taskId)
        if (taskId) void loadTaskArtifacts(taskId)
        if (taskId) void loadTaskDecisions(taskId)
    }, [props.open, mode, taskId, task?.title])

    const headerMeta = useMemo(() => {
        if (mode === 'new') return { subtitle: 'Create and assign work to the agent + humans.' }
        if (!task) return { subtitle: 'Loading…' }
        return { subtitle: `Status: ${task.status.replaceAll('_', ' ')} • Priority: ${task.priority} • Epic: ${task.epicId ?? 'none'}` }
    }, [mode, task])

    if (!props.open) return null

    const showSkeleton = props.forceSkeleton === true || (mode === 'edit' && !task)

    return (
        <div className="fixed inset-0 z-40" aria-label="Task drawer">
            <div className="absolute inset-0 bg-black/30 backdrop-blur-sm" onClick={props.onClose} />
            <aside className="absolute right-0 top-0 h-full w-[min(520px,100vw)] border-l border-zinc-200 bg-white shadow-xl dark:border-zinc-800 dark:bg-zinc-950">
                <div className="flex h-full flex-col">
                    <div className="flex items-start justify-between gap-3 border-b border-zinc-200 px-5 py-4 dark:border-zinc-800">
                        <div className="min-w-0">
                            <Heading level={3}>{title}</Heading>
                            <div className="mt-1 text-sm text-zinc-600 dark:text-zinc-300">{headerMeta.subtitle}</div>
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
                                <div className="flex flex-wrap gap-2 rounded-2xl border border-zinc-200 bg-zinc-50 p-2 text-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                                    <Button isQuiet={tab !== 'details'} onPress={() => setTab('details')}>
                                        Details
                                    </Button>
                                    <Button isQuiet={tab !== 'thread'} onPress={() => setTab('thread')}>
                                        Thread
                                    </Button>
                                    <Button isQuiet={tab !== 'decisions'} onPress={() => setTab('decisions')}>
                                        Decisions
                                    </Button>
                                    <Button isQuiet={tab !== 'artifacts'} onPress={() => setTab('artifacts')}>
                                        Artifacts
                                    </Button>
                                </div>

                                <section className="space-y-3">
                                    {tab === 'details' ? (
                                        <>
                                            <TextField
                                                label="Title"
                                                placeholder="Write a clear outcome…"
                                                value={draftTitle}
                                                onChange={setDraftTitle}
                                            />

                                            <div className="grid gap-3 sm:grid-cols-2">
                                                <Field label="Epic">
                                                    <select
                                                        className={selectClass}
                                                        value={draftEpicId}
                                                        onChange={(e) => setDraftEpicId(e.target.value)}
                                                    >
                                                        <option value="">No epic</option>
                                                        {Object.values(state.epics).map((e) => (
                                                            <option key={e.id} value={e.id}>
                                                                {e.title}
                                                            </option>
                                                        ))}
                                                    </select>
                                                </Field>
                                                <Field label="Priority">
                                                    <select
                                                        className={selectClass}
                                                        value={draftPriority}
                                                        onChange={(e) => setDraftPriority(e.target.value as TaskPriority)}
                                                    >
                                                        <option value="low">Low</option>
                                                        <option value="medium">Medium</option>
                                                        <option value="high">High</option>
                                                    </select>
                                                </Field>
                                                <Field label="Due date">
                                                    <input
                                                        type="date"
                                                        className={inputClass}
                                                        value={draftDueDate}
                                                        onChange={(e) => setDraftDueDate(e.target.value)}
                                                    />
                                                </Field>
                                                <Field label="Estimate (minutes)">
                                                    <input
                                                        inputMode="numeric"
                                                        className={inputClass}
                                                        placeholder="e.g. 45"
                                                        value={draftEstimate}
                                                        onChange={(e) => setDraftEstimate(e.target.value)}
                                                    />
                                                </Field>
                                                <Field label="Assignee">
                                                    <select
                                                        className={selectClass}
                                                        value={draftAssigneeId}
                                                        onChange={(e) => setDraftAssigneeId(e.target.value)}
                                                    >
                                                        <option value="">Unassigned</option>
                                                        {state.users.map((u) => (
                                                            <option key={u.id} value={u.id}>
                                                                {u.name}
                                                            </option>
                                                        ))}
                                                    </select>
                                                </Field>
                                                <div className="rounded-xl border border-zinc-200 bg-white p-3 text-sm shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                                                    <div className="text-xs font-semibold uppercase tracking-wide opacity-60">Collaborators</div>
                                                    <div className="mt-2 space-y-2">
                                                        {state.users.map((u) => (
                                                            <label key={u.id} className="flex items-center gap-2">
                                                                <input
                                                                    type="checkbox"
                                                                    checked={draftCollaborators[u.id] === true}
                                                                    onChange={(e) =>
                                                                        setDraftCollaborators((m) => ({ ...m, [u.id]: e.target.checked }))
                                                                    }
                                                                />
                                                                <span className="text-sm">{u.name}</span>
                                                            </label>
                                                        ))}
                                                    </div>
                                                </div>
                                            </div>

                                            <div className="grid gap-3 sm:grid-cols-2">
                                                <div className="rounded-xl border border-zinc-200 bg-white p-3 text-sm shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                                                    <div className="text-xs font-semibold uppercase tracking-wide opacity-60">Mode</div>
                                                    <div className="mt-1 font-medium">{mode === 'new' ? 'Create' : 'Edit'}</div>
                                                    <div className="mt-1 opacity-70">
                                                        This drawer is the consistent “agent work surface”.
                                                    </div>
                                                </div>
                                                <div className="rounded-xl border border-zinc-200 bg-white p-3 text-sm shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                                                    <div className="text-xs font-semibold uppercase tracking-wide opacity-60">Actions</div>
                                                    <div className="mt-2 flex flex-wrap gap-2">
                                                        <Button
                                                            isQuiet
                                                            data-testid="taskdrawer-about-modes"
                                                            onPress={() =>
                                                                openModal('Task mode', 'This will map to Shalimar component modes later.')
                                                            }
                                                        >
                                                            About modes
                                                        </Button>
                                                        <Button
                                                            isQuiet
                                                            onPress={() =>
                                                                openModal(
                                                                    'Correctness',
                                                                    'Decisions + artifacts are first-class outputs so agent/human loops remain auditable.',
                                                                )
                                                            }
                                                        >
                                                            Correctness
                                                        </Button>
                                                    </div>
                                                </div>
                                            </div>
                                        </>
                                    ) : null}
                                </section>

                                {tab === 'thread' && mode === 'edit' && taskId ? (
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
                                            {!messages || props.forceThreadSkeleton ? <ThreadSkeleton /> : null}
                                        </div>

                                        <div className="rounded-xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
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
                                ) : tab === 'thread' ? (
                                    <section className="rounded-xl border border-zinc-200 bg-white p-3 text-sm text-zinc-600 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-300">
                                        Thread will be available after the task is created.
                                    </section>
                                ) : null}

                                {tab === 'decisions' && mode === 'edit' && taskId ? (
                                    <section className="space-y-3">
                                        <div className="flex items-end justify-between gap-3">
                                            <Heading level={4}>Decisions</Heading>
                                            <Button isQuiet onPress={() => openModal('Decisions', 'Decisions capture correctness: question → options → outcome → rationale.')}>
                                                Why
                                            </Button>
                                        </div>

                                        <div className="space-y-2">
                                            {(decisions ?? []).map((d) => (
                                                <div
                                                    key={d.id}
                                                    className="rounded-2xl border border-zinc-200 bg-white p-3 text-sm shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40"
                                                >
                                                    <div className="text-xs text-zinc-500 dark:text-zinc-400">{new Date(d.ts).toLocaleString()}</div>
                                                    <div className="mt-1 font-semibold">{d.question}</div>
                                                    <div className="mt-1 opacity-80">Outcome: {d.outcome}</div>
                                                    {d.rationale ? <div className="mt-1 opacity-70">Rationale: {d.rationale}</div> : null}
                                                </div>
                                            ))}
                                            {!decisions ? (
                                                <div className="rounded-xl border border-zinc-200 bg-white p-3 text-sm text-zinc-600 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-300">
                                                    Loading…
                                                </div>
                                            ) : null}
                                        </div>

                                        <div className="rounded-2xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                                            <TextField label="Question" value={decisionQuestion} onChange={setDecisionQuestion} />
                                            <div className="mt-3 grid gap-3 sm:grid-cols-2">
                                                <Field label="Options (one per line)">
                                                    <textarea
                                                        className={textareaClass}
                                                        value={decisionOptions}
                                                        onChange={(e) => setDecisionOptions(e.target.value)}
                                                    />
                                                </Field>
                                                <Field label="Outcome">
                                                    <textarea
                                                        className={textareaClass}
                                                        value={decisionOutcome}
                                                        onChange={(e) => setDecisionOutcome(e.target.value)}
                                                    />
                                                </Field>
                                            </div>
                                            <div className="mt-3">
                                                <TextField label="Rationale (optional)" value={decisionRationale} onChange={setDecisionRationale} />
                                            </div>
                                            <div className="mt-3 flex flex-wrap gap-2">
                                                <Button
                                                    onPress={() =>
                                                        void createDecision(
                                                            taskId,
                                                            decisionQuestion,
                                                            decisionOptions,
                                                            decisionOutcome,
                                                            decisionRationale,
                                                            setBusy,
                                                            busy,
                                                            clearDecisionDraft,
                                                        )
                                                    }
                                                    isDisabled={busy}
                                                >
                                                    Record decision
                                                </Button>
                                            </div>
                                        </div>
                                    </section>
                                ) : null}

                                {tab === 'artifacts' && mode === 'edit' && taskId ? (
                                    <section className="space-y-3">
                                        <div className="flex items-end justify-between gap-3">
                                            <Heading level={4}>Artifacts</Heading>
                                            <Button isQuiet onPress={() => openModal('Artifacts', 'Artifacts are durable outputs created by agents/humans.')}>
                                                Why
                                            </Button>
                                        </div>

                                        <div className="space-y-2">
                                            {(artifacts ?? []).map((a) => (
                                                <div
                                                    key={a.id}
                                                    className="rounded-2xl border border-zinc-200 bg-white p-3 text-sm shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40"
                                                >
                                                    <div className="flex items-baseline justify-between gap-2">
                                                        <div className="font-semibold">{a.title}</div>
                                                        <div className="text-xs text-zinc-500 dark:text-zinc-400">{a.kind}</div>
                                                    </div>
                                                    <div className="mt-1 whitespace-pre-wrap opacity-80">{a.content}</div>
                                                </div>
                                            ))}
                                            {!artifacts ? (
                                                <div className="rounded-xl border border-zinc-200 bg-white p-3 text-sm text-zinc-600 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-300">
                                                    Loading…
                                                </div>
                                            ) : null}
                                        </div>

                                        <div className="rounded-2xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                                            <div className="grid gap-3 sm:grid-cols-2">
                                                <Field label="Kind">
                                                    <select
                                                        className={selectClass}
                                                        value={artifactKind}
                                                        onChange={(e) => setArtifactKind(e.target.value as ArtifactKind)}
                                                    >
                                                        <option value="notes">Notes</option>
                                                        <option value="plan">Plan</option>
                                                        <option value="draft">Draft</option>
                                                        <option value="email">Email</option>
                                                        <option value="call_summary">Call summary</option>
                                                        <option value="decision_log">Decision log</option>
                                                        <option value="other">Other</option>
                                                    </select>
                                                </Field>
                                                <TextField label="Title" value={artifactTitle} onChange={setArtifactTitle} />
                                            </div>
                                            <div className="mt-3">
                                                <Field label="Content">
                                                    <textarea
                                                        className={textareaClass}
                                                        value={artifactContent}
                                                        onChange={(e) => setArtifactContent(e.target.value)}
                                                    />
                                                </Field>
                                            </div>
                                            <div className="mt-3 flex flex-wrap gap-2">
                                                <Button
                                                    onPress={() =>
                                                        void createArtifact(
                                                            taskId,
                                                            artifactKind,
                                                            artifactTitle,
                                                            artifactContent,
                                                            setBusy,
                                                            busy,
                                                            clearArtifactDraft,
                                                        )
                                                    }
                                                    isDisabled={busy}
                                                >
                                                    Add artifact
                                                </Button>
                                            </div>
                                        </div>
                                    </section>
                                ) : null}
                            </div>
                        )}
                    </div>

                    <div className="border-t border-zinc-200 px-5 py-4 dark:border-zinc-800">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                            <div className="text-xs text-zinc-500 dark:text-zinc-400">Tip: press Esc to close (coming next)</div>
                            <div className="flex flex-wrap gap-2">
                                <Button isQuiet onPress={props.onClose}>
                                    Cancel
                                </Button>
                                <Button
                                    onPress={() =>
                                        void save(
                                            mode,
                                            taskId,
                                            draftTitle,
                                            draftEpicId,
                                            draftPriority,
                                            draftAssigneeId,
                                            draftDueDate,
                                            draftEstimate,
                                            draftCollaborators,
                                            props.onClose,
                                            setBusy,
                                            busy,
                                        )
                                    }
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

    function clearArtifactDraft() {
        setArtifactTitle('')
        setArtifactContent('')
        setArtifactKind('notes')
    }

    function clearDecisionDraft() {
        setDecisionQuestion('')
        setDecisionOptions('')
        setDecisionOutcome('')
        setDecisionRationale('')
    }

    async function save(
        mode: 'new' | 'edit',
        taskId: string | undefined,
        title: string,
        epicId: string,
        priority: TaskPriority,
        assigneeId: string,
        dueDate: string,
        estimateMinutesRaw: string,
        collaborators: Record<Id, boolean>,
        close: () => void,
        setBusy: (v: boolean) => void,
        busy: boolean,
    ) {
        const trimmed = title.trim()
        if (!trimmed || busy) return

        const estimate = estimateMinutesRaw.trim() ? Number(estimateMinutesRaw.trim()) : undefined
        if (estimateMinutesRaw.trim() && (!Number.isFinite(estimate) || estimate! < 0)) {
            pushToast({ tone: 'danger', title: 'Invalid estimate', message: 'Use a non-negative number of minutes.' })
            return
        }

        const collaboratorIds = Object.entries(collaborators)
            .filter(([, v]) => v)
            .map(([k]) => k)

        setBusy(true)
        try {
            if (mode === 'new') {
                const created = await createTask(trimmed)
                await updateTask(created.id, {
                    epicId: epicId || undefined,
                    priority,
                    assigneeId: assigneeId || undefined,
                    dueAt: dueDate ? new Date(`${dueDate}T00:00:00.000Z`).toISOString() : undefined,
                    estimateMinutes: estimate,
                    collaboratorIds,
                })
                pushToast({ tone: 'success', title: 'Task created', message: created.title })
                pushNotification({ title: 'New task created', message: created.title, href: `/tasks?drawer=${created.id}` })
            } else if (taskId) {
                await updateTask(taskId, {
                    title: trimmed,
                    epicId: epicId || undefined,
                    priority,
                    assigneeId: assigneeId || undefined,
                    dueAt: dueDate ? new Date(`${dueDate}T00:00:00.000Z`).toISOString() : undefined,
                    estimateMinutes: estimate,
                    collaboratorIds,
                })
                pushToast({ tone: 'success', title: 'Task updated' })
            }
            close()
        } finally {
            setBusy(false)
        }
    }
}

const inputClass =
    'w-full rounded-md border border-zinc-200 bg-white px-3 py-2 text-sm text-zinc-950 outline-none focus:ring-2 focus:ring-blue-500/30 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-50'
const selectClass = inputClass
const textareaClass = `${inputClass} min-h-24`

function Field(props: { label: string; children: React.ReactNode }) {
    return (
        <div>
            <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">{props.label}</div>
            {props.children}
        </div>
    )
}

async function createArtifact(
    taskId: string,
    kind: ArtifactKind,
    title: string,
    content: string,
    setBusy: (v: boolean) => void,
    busy: boolean,
    clear: () => void,
) {
    if (busy) return
    const t = title.trim()
    const c = content.trim()
    if (!t || !c) return
    setBusy(true)
    try {
        await addTaskArtifact(taskId, 'agent', kind, t, c)
        pushToast({ tone: 'success', title: 'Artifact added', message: t })
        clear()
    } finally {
        setBusy(false)
    }
}

async function createDecision(
    taskId: string,
    question: string,
    optionsRaw: string,
    outcome: string,
    rationale: string,
    setBusy: (v: boolean) => void,
    busy: boolean,
    clear: () => void,
) {
    if (busy) return
    const q = question.trim()
    const opts = optionsRaw
        .split('\n')
        .map((x) => x.trim())
        .filter(Boolean)
    const o = outcome.trim()
    if (!q || !o || opts.length === 0) return
    setBusy(true)
    try {
        await addTaskDecision(taskId, 'human', q, opts, o, rationale.trim() ? rationale.trim() : undefined)
        pushToast({ tone: 'success', title: 'Decision recorded', message: o })
        clear()
    } finally {
        setBusy(false)
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
                        : 'border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40',
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
            <div className="max-w-[85%] rounded-2xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
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

