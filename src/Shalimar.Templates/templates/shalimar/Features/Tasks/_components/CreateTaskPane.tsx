import { Button, Heading, TextField } from '@react-spectrum/s2'
import { useState } from 'react'
import type { ValidationErrors } from '@generated/store'
import type { CreateTaskRequest } from '@generated/types'
import { useMutations } from '@generated/useMutations'
import { useMutationSubmit } from '../../App/forms/useMutationSubmit'
import { Field, selectClass } from './tasksUi'
import { pushToast } from '../../App/ui/store'

export function CreateTaskPane(props: { open: boolean; epicId?: string; onClose: () => void }) {
    const [title, setTitle] = useState('')
    const [prio, setPrio] = useState('medium')
    const [collaborators, setCollaborators] = useState('')
    const [tags, setTags] = useState('')
    const [estimateMinutes, setEstimateMinutes] = useState('')

    const { CrmTasks } = useMutations()
    const form = useMutationSubmit<CreateTaskRequest, any>(CrmTasks as any)

    if (!props.open) return null

    const errors: ValidationErrors | null = form.validation

    const save = async () => {
        const collaboratorIds = collaborators.trim() ? collaborators.split(',').map((s) => s.trim()) : []
        const tagList = tags.trim() ? tags.split(',').map((s) => s.trim()) : []
        const est = estimateMinutes.trim() ? Number(estimateMinutes.trim()) : null

        const req: CreateTaskRequest = {
            title,
            priority: prio,
            epicId: props.epicId ?? null,
            accountId: null,
            assigneeId: null,
            collaboratorIds,
            dueAt: null,
            estimateMinutes: Number.isFinite(est) ? est : null,
            tags: tagList,
        }

        const created = await form.submit(req)
        if (!created) return

        pushToast({ tone: 'success', title: 'Task created', message: created.title })
        props.onClose()
    }

    return (
        <div className="fixed inset-0 z-40" aria-label="Create task pane">
            <div className="absolute inset-0 bg-black/30 backdrop-blur-sm" onClick={props.onClose} />
            <aside className="absolute right-0 top-0 h-full w-[min(520px,100vw)] border-l border-zinc-200 bg-white shadow-xl dark:border-zinc-800 dark:bg-zinc-950">
                <div className="flex h-full flex-col">
                    <div className="border-b border-zinc-200 px-5 py-4 dark:border-zinc-800">
                        <Heading level={3}>New task</Heading>
                        <div className="mt-1 text-sm text-zinc-600 dark:text-zinc-300">
                            Submit uses generated mutation + generated validation mapping.
                        </div>
                    </div>

                    <div className="flex-1 overflow-auto px-5 py-4">
                        <div className="space-y-4">
                            <div>
                                <TextField label="Title" value={title} onChange={setTitle} />
                                {errors?.Title?.length ? (
                                    <div
                                        className="mt-2 rounded-lg border border-red-500/30 bg-red-500/10 p-2 text-sm text-red-800 dark:text-red-200"
                                        data-testid="create-title-error"
                                    >
                                        {errors.Title[0]}
                                    </div>
                                ) : null}
                            </div>

                            <Field label="Priority">
                                <select className={selectClass} value={prio} onChange={(e) => setPrio(e.target.value)}>
                                    <option value="low">Low</option>
                                    <option value="medium">Medium</option>
                                    <option value="high">High</option>
                                </select>
                            </Field>

                            <TextField
                                label="Collaborator IDs (comma-separated)"
                                description="Leave blanks between commas to prove nested RuleForEach validation."
                                value={collaborators}
                                onChange={setCollaborators}
                            />

                            <TextField
                                label="Tags (comma-separated)"
                                description="Leave blanks between commas to prove nested RuleForEach validation."
                                value={tags}
                                onChange={setTags}
                            />

                            <TextField
                                label="Estimate minutes"
                                description="Try a negative number to trigger validation."
                                inputMode="numeric"
                                value={estimateMinutes}
                                onChange={setEstimateMinutes}
                            />

                            {form.error ? (
                                <div className="rounded-xl border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-900 dark:text-red-100">
                                    {form.error}
                                </div>
                            ) : null}

                            {errors ? (
                                <div
                                    className="rounded-xl border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-900 dark:text-red-100"
                                    data-testid="create-error-summary"
                                >
                                    <div className="text-xs font-semibold uppercase tracking-wide text-red-700 dark:text-red-200">
                                        Validation summary
                                    </div>
                                    <div className="mt-2 space-y-1">
                                        {Object.entries(errors).map(([k, v]) => (
                                            <div key={k} className="flex flex-wrap gap-x-2">
                                                <span className="font-mono text-xs">{k}</span>
                                                <span className="text-sm">{v?.[0]}</span>
                                            </div>
                                        ))}
                                    </div>
                                </div>
                            ) : null}

                            {errors ? (
                                <details className="rounded-xl border border-zinc-200 bg-white p-3 text-sm shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                                    <summary className="cursor-pointer text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                                        Validation details
                                    </summary>
                                    <pre className="mt-2 overflow-auto text-xs">{JSON.stringify(errors, null, 2)}</pre>
                                </details>
                            ) : null}
                        </div>
                    </div>

                    <div className="border-t border-zinc-200 px-5 py-4 dark:border-zinc-800">
                        <div className="flex flex-wrap justify-end gap-2">
                            <Button isQuiet onPress={props.onClose}>
                                Cancel
                            </Button>
                            <Button onPress={() => void save()} isDisabled={form.busy} data-testid="create-save">
                                {form.busy ? 'Saving…' : 'Save'}
                            </Button>
                        </div>
                    </div>
                </div>
            </aside>
        </div>
    )
}

