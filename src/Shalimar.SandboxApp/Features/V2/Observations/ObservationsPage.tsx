import { Button, Heading, TextField } from '@react-spectrum/s2'
import { Suspense, useMemo, useState } from 'react'
import { paths } from '@generated/paths'
import type { ValidationErrors } from '@generated/store'
import { useObservationsProps, useObservationsPropsGridDeferred } from '@generated/store'
import { useMutations } from '@generated/useMutations'

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

function ObservationsGrid() {
    const props = useObservationsProps()
    const grid = useObservationsPropsGridDeferred()

    const prevHref = buildHref(paths.observationsProps(), {
        q: props.query ?? undefined,
        page: String(Math.max(1, props.page - 1)),
        pageSize: String(props.pageSize),
    })
    const nextHref = buildHref(paths.observationsProps(), {
        q: props.query ?? undefined,
        page: String(props.page + 1),
        pageSize: String(props.pageSize),
    })

    return (
        <div className="overflow-hidden rounded-2xl border border-zinc-200 bg-white shadow-sm">
            <div className="border-b border-zinc-200 px-4 py-3 text-sm font-semibold">Observations</div>
            <div className="divide-y divide-zinc-200">
                {grid.items.map((o) => (
                    <a
                        key={o.id}
                        href={buildHref(paths.observationsProps(), { q: props.query ?? undefined, page: String(props.page), pageSize: String(props.pageSize), pane: o.id })}
                        className="block px-4 py-3 hover:bg-zinc-50"
                        data-testid="observation-row"
                    >
                        <div className="flex items-start justify-between gap-3">
                            <div className="min-w-0">
                                <div className="truncate text-sm font-medium">{o.kind}</div>
                                <div className="mt-0.5 text-xs text-zinc-500">{o.residentId}</div>
                            </div>
                            <div className="shrink-0 text-xs text-zinc-500">{new Date(o.ts).toLocaleString()}</div>
                        </div>
                        <div className="mt-2 text-xs text-zinc-600">{o.note}</div>
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

function ObservationPane(props: { mode: 'new' | 'edit'; observationId?: string; onCloseHref: string }) {
    const mutations = useMutations()
    const create = useSubmit(mutations.Observations.mutate)
    const update = useSubmit((req) => mutations.ObservationsById.mutate(props.observationId!, req))
    const del = useSubmit((req) => mutations.ObservationsByIdDelete.mutate(props.observationId!, req))

    const title = props.mode === 'new' ? 'New observation' : `Edit observation ${props.observationId}`

    const [kind, setKind] = useState('')
    const [note, setNote] = useState('')
    const [residentId, setResidentId] = useState('')

    const onSave = async () => {
        if (props.mode === 'new') {
            const created = await create.submit({ kind, note, residentId })
            if (!created) return
            window.location.assign(props.onCloseHref)
            return
        }
        const updated = await update.submit({ kind: kind || undefined, note: note || undefined, residentId: residentId || undefined })
        if (!updated) return
        window.location.assign(props.onCloseHref)
    }

    const onDelete = async () => {
        if (props.mode !== 'edit') return
        const res = await del.submit({})
        if (!res) return
        window.location.assign(props.onCloseHref)
    }

    const validation = (props.mode === 'new' ? create.validation : update.validation) ?? null

    return (
        <div className="fixed inset-y-0 right-0 z-50 w-[min(560px,100vw)] border-l border-zinc-200 bg-white shadow-xl">
            <div className="flex items-center justify-between gap-3 border-b border-zinc-200 px-4 py-3">
                <div className="min-w-0">
                    <div className="text-sm font-semibold">{title}</div>
                    <div className="mt-0.5 text-xs text-zinc-500">Create/edit/delete via generated mutations.</div>
                </div>
                <a href={props.onCloseHref} className="text-sm text-zinc-600 hover:underline" aria-label="Close observation pane">
                    Close
                </a>
            </div>
            <div className="space-y-4 p-4">
                <TextField
                    label="Kind"
                    value={kind}
                    onChange={(v) => setKind(v)}
                    validationState={validation?.Kind?.length ? 'invalid' : undefined}
                    errorMessage={validation?.Kind?.[0]}
                />
                <TextField
                    label="Note"
                    value={note}
                    onChange={(v) => setNote(v)}
                    validationState={validation?.Note?.length ? 'invalid' : undefined}
                    errorMessage={validation?.Note?.[0]}
                />
                <TextField
                    label="Resident id"
                    value={residentId}
                    onChange={(v) => setResidentId(v)}
                    validationState={validation?.ResidentId?.length ? 'invalid' : undefined}
                    errorMessage={validation?.ResidentId?.[0]}
                />

                {(create.error || update.error || del.error) ? <div className="text-sm text-red-600">{create.error ?? update.error ?? del.error}</div> : null}

                <div className="flex items-center justify-between gap-2">
                    {props.mode === 'edit' ? (
                        <Button variant="negative" onPress={onDelete} isDisabled={del.busy} data-testid="observation-delete">
                            Delete
                        </Button>
                    ) : (
                        <span />
                    )}
                    <div className="flex items-center gap-2">
                        <Button variant="secondary" onPress={() => window.location.assign(props.onCloseHref)}>
                            Cancel
                        </Button>
                        <Button variant="primary" onPress={onSave} isDisabled={create.busy || update.busy} data-testid="observation-save">
                            Save
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    )
}

export default function ObservationsPage() {
    const props = useObservationsProps()

    const base = paths.observationsProps()
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
                <a href={newHref} className="rounded-xl bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700" data-testid="observation-new">
                    New observation
                </a>
            </div>

            <div className="grid grid-cols-1 gap-4 lg:grid-cols-[360px_1fr]">
                <div className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm">
                    <div className="text-sm font-semibold">Filters</div>
                    <div className="mt-4 space-y-3">
                        <TextField label="Search" value={q} onChange={(v) => setQ(v)} />
                        <a href={applyHref} className="inline-block rounded-xl border border-zinc-200 px-3 py-2 text-sm hover:bg-zinc-50" data-testid="observation-apply">
                            Apply
                        </a>
                    </div>
                </div>

                <Suspense fallback={<GridSkeleton />}>
                    <ObservationsGrid />
                </Suspense>
            </div>

            {props.pane === 'new' ? <ObservationPane mode="new" onCloseHref={closeHref} /> : null}
            {props.pane && props.pane !== 'new' ? <ObservationPane mode="edit" observationId={props.pane} onCloseHref={closeHref} /> : null}
        </div>
    )
}

