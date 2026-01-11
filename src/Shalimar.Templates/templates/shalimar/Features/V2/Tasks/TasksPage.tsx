import { Button, Heading, TextField } from '@react-spectrum/s2'
import { Suspense, useEffect, useMemo, useState } from 'react'
import { useNavigate, useRouterState } from '@tanstack/react-router'
import { pushToast } from '../../App/ui/store'
import { useMutationSubmit } from '../../App/forms/useMutationSubmit'
import { useMutations } from '@generated/useMutations'
import { invalidateV2TasksPropsGrid, mergeV2Ui, useV2TasksProps, useV2TasksPropsGridDeferred, useV2Ui, v2Keys } from '@generated/store'
import { paths } from '@generated/paths'

function GridSkeleton() {
    return (
        <div className="space-y-3" data-testid="v2-grid-skeleton">
            <div className="h-10 w-full animate-pulse rounded-xl bg-zinc-200 dark:bg-zinc-800" />
            <div className="h-10 w-full animate-pulse rounded-xl bg-zinc-200 dark:bg-zinc-800" />
            <div className="h-10 w-full animate-pulse rounded-xl bg-zinc-200 dark:bg-zinc-800" />
        </div>
    )
}

function V2TasksGrid() {
    const grid = useV2TasksPropsGridDeferred()

    const navigate = useNavigate()
    const search = useRouterState({ select: (s) => s.location.search })
    const sp = useMemo(() => new URLSearchParams(search), [search])

    const navigatePage = (page: number) => {
        const next = new URLSearchParams(sp)
        next.set('page', String(Math.max(1, page)))
        // Server-first route: navigate to same route with new query, causing server re-render.
        navigate({ to: paths.v2TasksProps(), search: Object.fromEntries(next.entries()) })
    }

    return (
        <div className="overflow-hidden rounded-2xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
            <div className="border-b border-zinc-200 px-4 py-3 text-sm font-semibold dark:border-zinc-800">Tasks</div>
            <div className="divide-y divide-zinc-200 dark:divide-zinc-800">
                {grid.items.map((t) => (
                    <div key={t.id} className="flex items-center justify-between gap-3 px-4 py-3" data-testid="v2-grid-row">
                        <div className="min-w-0">
                            <div className="truncate text-sm font-medium">{t.title}</div>
                            <div className="mt-0.5 text-xs text-zinc-500 dark:text-zinc-400">
                                {t.status} · {t.priority}
                            </div>
                        </div>
                        <div className="shrink-0 text-xs text-zinc-500 dark:text-zinc-400">#{t.id}</div>
                    </div>
                ))}
            </div>
            <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 text-xs text-zinc-500 dark:text-zinc-400">
                <div>
                    Page {grid.page} · {grid.total} total
                </div>
                <div className="flex items-center gap-2">
                    <span>Page size {grid.pageSize}</span>
                    <Button isQuiet onPress={() => navigatePage(grid.page - 1)} isDisabled={grid.page <= 1}>
                        Prev
                    </Button>
                    <Button
                        isQuiet
                        onPress={() => navigatePage(grid.page + 1)}
                        isDisabled={grid.page * grid.pageSize >= grid.total}
                    >
                        Next
                    </Button>
                </div>
            </div>
        </div>
    )
}

function CreatePane(props: { onClose: () => void }) {
    const { CrmTasks } = useMutations()
    const { busy, validation, error, submit } = useMutationSubmit(CrmTasks)

    const defaults = CrmTasks.defaults
    const [title, setTitle] = useState(defaults.title ?? '')

    const onSave = async () => {
        const created = await submit({ ...defaults, title })
        if (!created) return
        pushToast({ tone: 'success', title: 'Task created', message: 'Grid refreshed via invalidation.' })
        invalidateV2TasksPropsGrid()
        props.onClose()
    }

    return (
        <div className="fixed inset-y-0 right-0 z-40 w-[min(520px,100vw)] border-l border-zinc-200 bg-white shadow-xl dark:border-zinc-800 dark:bg-zinc-950">
            <div className="flex items-center justify-between gap-3 border-b border-zinc-200 px-4 py-3 dark:border-zinc-800">
                <div className="min-w-0">
                    <div className="text-sm font-semibold">Create task</div>
                    <div className="mt-0.5 text-xs text-zinc-500 dark:text-zinc-400">Server-truth validation</div>
                </div>
                <Button isQuiet onPress={props.onClose} aria-label="Close create pane">
                    Close
                </Button>
            </div>

            <div className="space-y-4 p-4">
                <TextField
                    label="Title"
                    value={title}
                    onChange={(v) => setTitle(v)}
                    isDisabled={busy}
                    validationState={validation?.Title?.length ? 'invalid' : undefined}
                    errorMessage={validation?.Title?.[0]}
                    data-testid="v2-create-title"
                />

                {error ? <div className="text-sm text-red-600">{error}</div> : null}

                <div className="flex justify-end gap-2">
                    <Button variant="secondary" onPress={props.onClose} isDisabled={busy}>
                        Cancel
                    </Button>
                    <Button variant="primary" onPress={onSave} isDisabled={busy} data-testid="v2-create-save">
                        {busy ? 'Saving…' : 'Save'}
                    </Button>
                </div>
            </div>
        </div>
    )
}

export default function V2TasksPage() {
    const props = useV2TasksProps()

    const navigate = useNavigate()
    const search = useRouterState({ select: (s) => s.location.search })
    const sp = useMemo(() => new URLSearchParams(search), [search])

    type Ui = { createOpen?: boolean; q?: string; status?: string; priority?: string }
    const ui = useV2Ui(v2Keys.V2TasksProps) as Ui

    const createOpen = ui.createOpen ?? sp.get('create') === '1'
    const q = ui.q ?? sp.get('q') ?? ''
    const status = ui.status ?? sp.get('status') ?? ''
    const priority = ui.priority ?? sp.get('priority') ?? ''

    useEffect(() => {
        // Keep UI state in the v2 store (survives props refresh/invalidation),
        // while still treating the URL as the refreshable server contract.
        mergeV2Ui(v2Keys.V2TasksProps, {
            createOpen: sp.get('create') === '1',
            q: sp.get('q') ?? '',
            status: sp.get('status') ?? '',
            priority: sp.get('priority') ?? '',
        })
    }, [sp])

    const setCreateParam = (open: boolean) => {
        const next = new URLSearchParams(sp)
        if (open) next.set('create', '1')
        else next.delete('create')
        navigate({ to: paths.v2TasksProps(), search: Object.fromEntries(next.entries()) })
    }

    const onOpenCreate = () => {
        setCreateParam(true)
    }

    const onCloseCreate = () => {
        setCreateParam(false)
    }

    const onApplyFilters = () => {
        const next = new URLSearchParams(sp)
        next.delete('create')
        next.set('page', '1')
        if (q.trim()) next.set('q', q.trim())
        else next.delete('q')
        if (status.trim()) next.set('status', status.trim())
        else next.delete('status')
        if (priority.trim()) next.set('priority', priority.trim())
        else next.delete('priority')
        navigate({ to: paths.v2TasksProps(), search: Object.fromEntries(next.entries()) })
    }

    return (
        <div className="space-y-5">
            <div className="flex flex-wrap items-end justify-between gap-3">
                <div className="min-w-0">
                    <Heading level={2}>{props.title}</Heading>
                    <div className="mt-1 text-sm text-zinc-600 dark:text-zinc-300">
                        v2 proof: paging/filter state in URL, grid refresh via invalidation, no fetch URLs in TSX
                    </div>
                </div>
                <Button variant="primary" onPress={onOpenCreate} data-testid="v2-open-create">
                    New task
                </Button>
            </div>

            <div className="grid grid-cols-1 gap-4 lg:grid-cols-[360px_1fr]">
                <div className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                    <div className="text-sm font-semibold">Filters</div>
                    <div className="mt-3 text-xs text-zinc-500 dark:text-zinc-400">This is URL-driven for server-first routing.</div>
                    <div className="mt-4 space-y-3">
                        <TextField label="Search" value={q} onChange={(v) => mergeV2Ui(v2Keys.V2TasksProps, { q: v })} />
                        <TextField label="Status" value={status} onChange={(v) => mergeV2Ui(v2Keys.V2TasksProps, { status: v })} />
                        <TextField label="Priority" value={priority} onChange={(v) => mergeV2Ui(v2Keys.V2TasksProps, { priority: v })} />
                        <div className="pt-1">
                            <Button variant="secondary" onPress={onApplyFilters} data-testid="v2-filters-apply">
                                Apply
                            </Button>
                        </div>
                    </div>
                </div>

                <Suspense fallback={<GridSkeleton />}>
                    <V2TasksGrid />
                </Suspense>
            </div>

            {createOpen ? <CreatePane onClose={onCloseCreate} /> : null}
        </div>
    )
}

