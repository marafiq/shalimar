import { Outlet, createFileRoute, Link, useNavigate, useRouterState } from '@tanstack/react-router'
import { Button, Heading, Text, TextField } from '@react-spectrum/s2'
import { Suspense, useMemo, useState } from 'react'
import { useStore } from '@tanstack/react-store'
import { crmStore, ensureAccounts, ensureEpics, ensureTasks, ensureUsers } from '../Crm/store'
import { PageSkeleton } from '../App/ui/Skeletons'
import { IconPlus } from '../App/ui/icons'
import { TaskDrawer } from './_components/TaskDrawer'
import { useDeferred } from '@shalimar/runtime'
import { pushToast } from '../App/ui/store'
import { mutateCrmTasks, type ValidationErrors } from '@generated/store'
import type { CrmTasksGridDto, CreateTaskRequest, TasksProps } from '@generated/types'

export const Route = createFileRoute('/tasks')({
    validateSearch: (search: Record<string, unknown>) => ({
        drawer: typeof search.drawer === 'string' ? search.drawer : undefined,
        epicId: typeof search.epicId === 'string' ? search.epicId : undefined,
        q: typeof search.q === 'string' ? search.q : '',
        status: typeof search.status === 'string' ? search.status : '',
        priority: typeof search.priority === 'string' ? search.priority : '',
        page:
            typeof search.page === 'number'
                ? search.page
                : typeof search.page === 'string'
                  ? Number(search.page)
                  : 1,
        pageSize:
            typeof search.pageSize === 'number'
                ? search.pageSize
                : typeof search.pageSize === 'string'
                  ? Number(search.pageSize)
                  : 20,
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
    const { users, epics } = useStore(crmStore)
    const navigate = useNavigate()
    const { drawer, epicId, q, status, priority, page, pageSize, skeleton, drawerSkeleton, threadSkeleton } = Route.useSearch()
    const pathname = useRouterState({ select: (s) => s.location.pathname })

    if (skeleton) return <PageSkeleton />

    const isTasksIndex = pathname === '/tasks'
    const props = window.__SHALIMAR_PROPS__ as TasksProps
    const baseHref = props.grid.props.tasksGrid.href
    const href = useMemo(() => {
        const sp = new URLSearchParams()
        if (q.trim()) sp.set('q', q.trim())
        if (status) sp.set('status', status)
        if (priority) sp.set('priority', priority)
        if (epicId) sp.set('epicId', epicId)
        sp.set('page', String(Number.isFinite(page) && page > 0 ? page : 1))
        sp.set('pageSize', String(Number.isFinite(pageSize) && pageSize > 0 ? pageSize : 20))
        const qs = sp.toString()
        return qs ? `${baseHref}?${qs}` : baseHref
    }, [baseHref, epicId, page, pageSize, priority, q, status])

    return (
        <div>
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                <div>
                    <Heading level={1}>Tasks</Heading>
                    <Text>
                        Table view (server-truth). Filters + paging stay in the URL. Create happens in a right-side pane,
                        and the grid refreshes via typed invalidation without losing state.
                    </Text>
                </div>
                <div className="flex gap-2">
                    <Button onPress={() => navigate({ to: '/tasks', search: (s) => ({ ...s, drawer: 'new' }) })}>
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
                    <aside className="lg:col-span-4">
                        <div className="sticky top-4 space-y-3">
                            <div className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                                <Heading level={3}>Filters</Heading>
                                <div className="mt-3 space-y-3">
                                    <TextField
                                        label="Search"
                                        placeholder="Find tasks…"
                                        value={q}
                                        onChange={(v) => navigate({ to: '/tasks', search: (s) => ({ ...s, q: v, page: 1 }) })}
                                    />

                                    <Field label="Status">
                                        <select
                                            className={selectClass}
                                            value={status}
                                            onChange={(e) =>
                                                navigate({ to: '/tasks', search: (s) => ({ ...s, status: e.target.value, page: 1 }) })
                                            }
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
                                            value={priority}
                                            onChange={(e) =>
                                                navigate({ to: '/tasks', search: (s) => ({ ...s, priority: e.target.value, page: 1 }) })
                                            }
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
                                            value={epicId ?? ''}
                                            onChange={(e) =>
                                                navigate({ to: '/tasks', search: (s) => ({ ...s, epicId: e.target.value || undefined, page: 1 }) })
                                            }
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
                                            value={String(pageSize)}
                                            onChange={(e) =>
                                                navigate({ to: '/tasks', search: (s) => ({ ...s, pageSize: Number(e.target.value), page: 1 }) })
                                            }
                                        >
                                            <option value="10">10</option>
                                            <option value="20">20</option>
                                            <option value="50">50</option>
                                        </select>
                                    </Field>

                                    <div className="flex flex-wrap gap-2">
                                        <Button
                                            isQuiet
                                            onPress={() => navigate({ to: '/tasks', search: (s) => ({ ...s, q: '', status: '', priority: '', epicId: undefined, page: 1 }) })}
                                        >
                                            Clear
                                        </Button>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </aside>

                    <section className="lg:col-span-8">
                        <Suspense fallback={<GridSkeleton />}>
                            <TasksGrid
                                href={href}
                                onOpenTask={(id) => navigate({ to: '/tasks', search: (s) => ({ ...s, drawer: id }) })}
                                onPageChange={(p) => navigate({ to: '/tasks', search: (s) => ({ ...s, page: p }) })}
                            />
                        </Suspense>
                    </section>
                </div>
            ) : (
                <div className="mt-6">
                    <Outlet />
                </div>
            )}

            {isTasksIndex ? (
                <>
                    <CreateTaskPane
                        open={drawer === 'new'}
                        onClose={() =>
                            navigate({
                                to: '/tasks',
                                search: { drawer: undefined, epicId, q, status, priority, page, pageSize },
                            })
                        }
                    />
                    <TaskDrawer
                        open={typeof drawer === 'string' && drawer.length > 0 && drawer !== 'new'}
                        drawer={drawer === 'new' ? undefined : drawer}
                        prefillEpicId={epicId}
                        forceSkeleton={drawerSkeleton}
                        forceThreadSkeleton={threadSkeleton}
                        onClose={() =>
                            navigate({
                                to: '/tasks',
                                search: { drawer: undefined, epicId, q, status, priority, page, pageSize },
                            })
                        }
                    />
                </>
            ) : null}
        </div>
    )
}

function TasksGrid(props: { href: string; onOpenTask: (id: string) => void; onPageChange: (page: number) => void }) {
    const data = useDeferred<CrmTasksGridDto>({ href: props.href })
    const pages = Math.max(1, Math.ceil(data.total / data.pageSize))

    return (
        <div className="space-y-3">
            <div className="flex items-center justify-between gap-3">
                <div className="text-sm text-zinc-600 dark:text-zinc-300">
                    {data.total} total • page {data.page} / {pages}
                </div>
                <div className="flex gap-2">
                    <Button isQuiet isDisabled={data.page <= 1} onPress={() => props.onPageChange(data.page - 1)}>
                        Prev
                    </Button>
                    <Button isQuiet isDisabled={data.page >= pages} onPress={() => props.onPageChange(data.page + 1)}>
                        Next
                    </Button>
                </div>
            </div>

            <div className="overflow-hidden rounded-2xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                <table className="w-full text-sm">
                    <thead className="bg-zinc-50 text-left text-xs uppercase tracking-wide text-zinc-500 dark:bg-zinc-950/60 dark:text-zinc-400">
                        <tr>
                            <th className="px-4 py-3">Title</th>
                            <th className="px-4 py-3">Status</th>
                            <th className="px-4 py-3">Priority</th>
                            <th className="px-4 py-3">Updated</th>
                            <th className="px-4 py-3"></th>
                        </tr>
                    </thead>
                    <tbody className="divide-y divide-zinc-200 dark:divide-zinc-800">
                        {data.items.map((t) => (
                            <tr key={t.id} data-testid="tasks-row">
                                <td className="px-4 py-3">
                                    <div className="font-medium">{t.title}</div>
                                    <div className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">{t.id}</div>
                                </td>
                                <td className="px-4 py-3">{t.status.replaceAll('_', ' ')}</td>
                                <td className="px-4 py-3">{t.priority}</td>
                                <td className="px-4 py-3">{new Date(t.updatedAt).toLocaleString()}</td>
                                <td className="px-4 py-3 text-right">
                                    <Button isQuiet onPress={() => props.onOpenTask(t.id)}>
                                        Open
                                    </Button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    )
}

function GridSkeleton() {
    return (
        <div className="space-y-3" data-testid="tasks-grid-skeleton">
            <div className="h-4 w-48 rounded bg-zinc-200 dark:bg-zinc-800" />
            <div className="overflow-hidden rounded-2xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                <div className="space-y-0">
                    {Array.from({ length: 6 }).map((_, i) => (
                        <div key={i} className="flex gap-3 border-b border-zinc-200 px-4 py-3 dark:border-zinc-800">
                            <div className="h-4 w-[55%] rounded bg-zinc-200 dark:bg-zinc-800" />
                            <div className="h-4 w-[15%] rounded bg-zinc-200 dark:bg-zinc-800" />
                            <div className="h-4 w-[15%] rounded bg-zinc-200 dark:bg-zinc-800" />
                            <div className="ml-auto h-4 w-16 rounded bg-zinc-200 dark:bg-zinc-800" />
                        </div>
                    ))}
                </div>
            </div>
        </div>
    )
}

function CreateTaskPane(props: { open: boolean; onClose: () => void }) {
    const { epicId } = Route.useSearch()
    const [busy, setBusy] = useState(false)
    const [title, setTitle] = useState('')
    const [prio, setPrio] = useState('medium')
    const [errors, setErrors] = useState<ValidationErrors | null>(null)

    if (!props.open) return null

    const save = async () => {
        if (busy) return
        setBusy(true)
        setErrors(null)

        const req: CreateTaskRequest = {
            title,
            priority: prio,
            epicId: epicId ?? null,
            accountId: null,
            assigneeId: null,
            collaboratorIds: [],
            dueAt: null,
            estimateMinutes: null,
            tags: [],
        }

        try {
            const result = await mutateCrmTasks(req)
            if (!result.ok) {
                if ('validation' in result) {
                    setErrors(result.validation)
                    return
                }
                pushToast({ tone: 'danger', title: 'Create failed', message: result.error })
                return
            }

            pushToast({ tone: 'success', title: 'Task created', message: result.value.title })
            // Close pane while keeping URL state (filters/paging/search).
            props.onClose()
        } finally {
            setBusy(false)
        }
    }

    return (
        <div className="fixed inset-0 z-40" aria-label="Create task pane">
            <div className="absolute inset-0 bg-black/30 backdrop-blur-sm" onClick={props.onClose} />
            <aside className="absolute right-0 top-0 h-full w-[min(520px,100vw)] border-l border-zinc-200 bg-white shadow-xl dark:border-zinc-800 dark:bg-zinc-950">
                <div className="flex h-full flex-col">
                    <div className="border-b border-zinc-200 px-5 py-4 dark:border-zinc-800">
                        <Heading level={3}>New task</Heading>
                        <div className="mt-1 text-sm text-zinc-600 dark:text-zinc-300">
                            Server validation is the source of truth. Fix errors and save again.
                        </div>
                    </div>

                    <div className="flex-1 overflow-auto px-5 py-4">
                        <div className="space-y-4">
                            <div>
                                <TextField label="Title" value={title} onChange={setTitle} />
                                {errors?.Title?.length ? (
                                    <div className="mt-2 rounded-lg border border-red-500/30 bg-red-500/10 p-2 text-sm text-red-800 dark:text-red-200" data-testid="create-title-error">
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
                            <Button onPress={() => void save()} isDisabled={busy} data-testid="create-save">
                                {busy ? 'Saving…' : 'Save'}
                            </Button>
                        </div>
                    </div>
                </div>
            </aside>
        </div>
    )
}

const selectClass =
    'w-full rounded-md border border-zinc-200 bg-white px-3 py-2 text-sm text-zinc-950 outline-none focus:ring-2 focus:ring-blue-500/30 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-50'

function Field(props: { label: string; children: React.ReactNode }) {
    return (
        <div>
            <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">{props.label}</div>
            {props.children}
        </div>
    )
}

