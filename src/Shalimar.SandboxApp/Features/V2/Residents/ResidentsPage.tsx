import { Button, Heading, TextField } from '@react-spectrum/s2'
import { Suspense, useMemo, useState } from 'react'
import { paths } from '@generated/paths'
import { invalidateResidentsPropsGrid, useResidentsProps, useResidentsPropsGridDeferred } from '@generated/store'
import { useMutations } from '@generated/useMutations'
import type { ValidationErrors } from '@generated/store'

function GridSkeleton() {
    return (
        <div className="space-y-3">
            <div className="h-10 w-full animate-pulse rounded-xl bg-zinc-200" />
            <div className="h-10 w-full animate-pulse rounded-xl bg-zinc-200" />
            <div className="h-10 w-full animate-pulse rounded-xl bg-zinc-200" />
        </div>
    )
}

function buildHref(base: string, search: Record<string, string | undefined>) {
    const sp = new URLSearchParams()
    for (const [k, v] of Object.entries(search)) {
        if (!v) continue
        sp.set(k, v)
    }
    const qs = sp.toString()
    return qs ? `${base}?${qs}` : base
}

function useSubmit<TReq, TRes>(mutate: (req: TReq) => Promise<{ ok: true; value: TRes } | { ok: false; validation: ValidationErrors } | { ok: false; error: string }>) {
    const [busy, setBusy] = useState(false)
    const [validation, setValidation] = useState<ValidationErrors | null>(null)
    const [error, setError] = useState<string | null>(null)

    const submit = async (req: TReq) => {
        setBusy(true)
        setValidation(null)
        setError(null)
        try {
            const result = await mutate(req)
            if (result.ok) return result.value
            if ('validation' in result) setValidation(result.validation)
            else setError(result.error)
            return null
        } finally {
            setBusy(false)
        }
    }

    return { busy, validation, error, submit } as const
}

function ResidentsGrid() {
    const props = useResidentsProps()
    const grid = useResidentsPropsGridDeferred()

    const prevHref = buildHref(paths.residentsProps(), {
        q: props.query ?? undefined,
        page: String(Math.max(1, props.page - 1)),
        pageSize: String(props.pageSize),
    })
    const nextHref = buildHref(paths.residentsProps(), {
        q: props.query ?? undefined,
        page: String(props.page + 1),
        pageSize: String(props.pageSize),
    })

    return (
        <div className="overflow-hidden rounded-2xl border border-zinc-200 bg-white shadow-sm">
            <div className="border-b border-zinc-200 px-4 py-3 text-sm font-semibold">Residents</div>
            <div className="divide-y divide-zinc-200">
                {grid.items.map((r) => (
                    <a
                        key={r.id}
                        href={buildHref(paths.residentsProps(), { q: props.query ?? undefined, page: String(props.page), pageSize: String(props.pageSize), pane: r.id })}
                        className="flex items-center justify-between gap-3 px-4 py-3 hover:bg-zinc-50"
                        data-testid="resident-row"
                    >
                        <div className="min-w-0">
                            <div className="truncate text-sm font-medium">{r.name}</div>
                            <div className="mt-0.5 text-xs text-zinc-500">
                                {r.careLevel} · Room {r.room}
                            </div>
                        </div>
                        <div className="shrink-0 text-xs text-zinc-500">{r.id}</div>
                    </a>
                ))}
            </div>
            <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 text-xs text-zinc-500">
                <div>
                    Page {grid.page} · {grid.total} total
                </div>
                <div className="flex items-center gap-2">
                    <a
                        href={prevHref}
                        className={[
                            'rounded-lg px-2 py-1 hover:bg-zinc-100',
                            grid.page <= 1 ? 'pointer-events-none opacity-40' : '',
                        ].join(' ')}
                    >
                        Prev
                    </a>
                    <a
                        href={nextHref}
                        className={[
                            'rounded-lg px-2 py-1 hover:bg-zinc-100',
                            grid.page * grid.pageSize >= grid.total ? 'pointer-events-none opacity-40' : '',
                        ].join(' ')}
                    >
                        Next
                    </a>
                </div>
            </div>
        </div>
    )
}

function ResidentPane(props: { mode: 'new' | 'edit'; residentId?: string; onCloseHref: string }) {
    const mutations = useMutations()
    const create = useSubmit((req: any) => mutations.Residents.mutate(req))
    const update = useSubmit((req: any) => mutations.ResidentsById.mutate(props.residentId!, req))
    const del = useSubmit((req: any) => mutations.ResidentsByIdDelete.mutate(props.residentId!, req))

    const title = props.mode === 'new' ? 'New resident' : `Edit resident ${props.residentId}`

    const [name, setName] = useState('')
    const [room, setRoom] = useState('')
    const [careLevel, setCareLevel] = useState('')

    const onSave = async () => {
        if (props.mode === 'new') {
            const created = await create.submit({ name, room, careLevel })
            if (!created) return
            invalidateResidentsPropsGrid()
            window.location.assign(props.onCloseHref)
            return
        }
        const updated = await update.submit({ name: name || undefined, room: room || undefined, careLevel: careLevel || undefined })
        if (!updated) return
        invalidateResidentsPropsGrid()
        window.location.assign(props.onCloseHref)
    }

    const onDelete = async () => {
        if (props.mode !== 'edit') return
        const res = await del.submit({})
        if (!res) return
        invalidateResidentsPropsGrid()
        window.location.assign(props.onCloseHref)
    }

    const validation = (props.mode === 'new' ? create.validation : update.validation) ?? null

    return (
        <div className="fixed inset-y-0 right-0 z-50 w-[min(520px,100vw)] border-l border-zinc-200 bg-white shadow-xl">
            <div className="flex items-center justify-between gap-3 border-b border-zinc-200 px-4 py-3">
                <div className="min-w-0">
                    <div className="text-sm font-semibold">{title}</div>
                    <div className="mt-0.5 text-xs text-zinc-500">Server-truth validation (FluentValidation)</div>
                </div>
                <a href={props.onCloseHref} className="text-sm text-zinc-600 hover:underline" aria-label="Close resident pane">
                    Close
                </a>
            </div>
            <div className="space-y-4 p-4">
                <TextField
                    label="Name"
                    value={name}
                    onChange={(v) => setName(v)}
                    validationState={validation?.Name?.length ? 'invalid' : undefined}
                    errorMessage={validation?.Name?.[0]}
                />
                <TextField
                    label="Room"
                    value={room}
                    onChange={(v) => setRoom(v)}
                    validationState={validation?.Room?.length ? 'invalid' : undefined}
                    errorMessage={validation?.Room?.[0]}
                />
                <TextField
                    label="Care level"
                    value={careLevel}
                    onChange={(v) => setCareLevel(v)}
                    validationState={validation?.CareLevel?.length ? 'invalid' : undefined}
                    errorMessage={validation?.CareLevel?.[0]}
                />

                {(create.error || update.error || del.error) ? <div className="text-sm text-red-600">{create.error ?? update.error ?? del.error}</div> : null}

                <div className="flex items-center justify-between gap-2">
                    {props.mode === 'edit' ? (
                        <Button variant="negative" onPress={onDelete} isDisabled={del.busy} data-testid="resident-delete">
                            Delete
                        </Button>
                    ) : (
                        <span />
                    )}
                    <div className="flex items-center gap-2">
                        <Button variant="secondary" onPress={() => window.location.assign(props.onCloseHref)}>
                            Cancel
                        </Button>
                        <Button variant="primary" onPress={onSave} isDisabled={create.busy || update.busy} data-testid="resident-save">
                            Save
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    )
}

export default function ResidentsPage() {
    const props = useResidentsProps()

    const base = paths.residentsProps()
    const [q, setQ] = useState(props.query ?? '')
    const applyHref = useMemo(
        () =>
            buildHref(base, {
                q: q.trim() || undefined,
                page: '1',
                pageSize: String(props.pageSize),
            }),
        [base, q, props.pageSize],
    )

    const newHref = buildHref(base, { q: props.query ?? undefined, page: String(props.page), pageSize: String(props.pageSize), pane: 'new' })
    const closeHref = buildHref(base, { q: props.query ?? undefined, page: String(props.page), pageSize: String(props.pageSize) })

    return (
        <div className="space-y-5">
            <div className="flex flex-wrap items-end justify-between gap-3">
                <div className="min-w-0">
                    <Heading level={2}>{props.title}</Heading>
                    <div className="mt-1 text-sm text-zinc-600">Filter + paging + CRUD side pane; invalidates grid on success.</div>
                </div>
                <a href={newHref} className="rounded-xl bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700" data-testid="resident-new">
                    New resident
                </a>
            </div>

            <div className="grid grid-cols-1 gap-4 lg:grid-cols-[360px_1fr]">
                <div className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm">
                    <div className="text-sm font-semibold">Filters</div>
                    <div className="mt-4 space-y-3">
                        <TextField label="Search" value={q} onChange={(v) => setQ(v)} />
                        <a href={applyHref} className="inline-block rounded-xl border border-zinc-200 px-3 py-2 text-sm hover:bg-zinc-50" data-testid="resident-apply">
                            Apply
                        </a>
                    </div>
                </div>

                <Suspense fallback={<GridSkeleton />}>
                    <ResidentsGrid />
                </Suspense>
            </div>

            {props.pane === 'new' ? <ResidentPane mode="new" onCloseHref={closeHref} /> : null}
            {props.pane && props.pane !== 'new' ? <ResidentPane mode="edit" residentId={props.pane} onCloseHref={closeHref} /> : null}
        </div>
    )
}

