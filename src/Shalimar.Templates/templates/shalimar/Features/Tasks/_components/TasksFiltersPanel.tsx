import { Heading, TextField, Button } from '@react-spectrum/s2'
import { Field, selectClass } from './tasksUi'

export type TasksFilters = {
    q: string
    status: string
    priority: string
    epicId?: string
    page: number
    pageSize: number
}

export function TasksFiltersPanel(props: {
    epics: Record<string, { id: string; title: string }>
    value: TasksFilters
    onChange: (next: Partial<TasksFilters>) => void
    onClear: () => void
}) {
    const { epics, value } = props

    return (
        <div className="sticky top-4 space-y-3">
            <div className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                <Heading level={3}>Filters</Heading>
                <div className="mt-3 space-y-3">
                    <TextField
                        label="Search"
                        placeholder="Find tasks…"
                        value={value.q}
                        onChange={(v) => props.onChange({ q: v, page: 1 })}
                    />

                    <Field label="Status">
                        <select
                            className={selectClass}
                            value={value.status}
                            onChange={(e) => props.onChange({ status: e.target.value, page: 1 })}
                        >
                            <option value="">Any</option>
                            <option value="backlog">Backlog</option>
                            <option value="todo">Todo</option>
                            <option value="in_progress">In progress</option>
                            <option value="blocked">Blocked</option>
                            <option value="done">Done</option>
                        </select>
                    </Field>

                    <Field label="Priority">
                        <select
                            className={selectClass}
                            value={value.priority}
                            onChange={(e) => props.onChange({ priority: e.target.value, page: 1 })}
                        >
                            <option value="">Any</option>
                            <option value="low">Low</option>
                            <option value="medium">Medium</option>
                            <option value="high">High</option>
                        </select>
                    </Field>

                    <Field label="Epic">
                        <select
                            className={selectClass}
                            value={value.epicId ?? ''}
                            onChange={(e) => props.onChange({ epicId: e.target.value || undefined, page: 1 })}
                        >
                            <option value="">Any</option>
                            {Object.values(epics).map((ep) => (
                                <option key={ep.id} value={ep.id}>
                                    {ep.title}
                                </option>
                            ))}
                        </select>
                    </Field>

                    <Field label="Page size">
                        <select
                            className={selectClass}
                            value={String(value.pageSize)}
                            onChange={(e) => props.onChange({ pageSize: Number(e.target.value), page: 1 })}
                        >
                            <option value="10">10</option>
                            <option value="20">20</option>
                            <option value="50">50</option>
                        </select>
                    </Field>

                    <div className="flex flex-wrap gap-2">
                        <Button isQuiet onPress={props.onClear}>
                            Clear
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    )
}

