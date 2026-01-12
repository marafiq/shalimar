import { Button, Heading, TextField } from '@react-spectrum/s2'
import { Suspense, useMemo, useState } from 'react'
import { paths } from '@generated/paths'
import type { ValidationErrors } from '@generated/store'
import { useMedPassScheduleProps, useMedPassSchedulePropsGridDeferred } from '@generated/store'
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

function ScheduleGrid() {
    const props = useMedPassScheduleProps()
    const grid = useMedPassSchedulePropsGridDeferred()

    const prevHref = buildHref(paths.medPassScheduleProps(), {
        q: props.query ?? undefined,
        page: String(Math.max(1, props.page - 1)),
        pageSize: String(props.pageSize),
    })
    const nextHref = buildHref(paths.medPassScheduleProps(), {
        q: props.query ?? undefined,
        page: String(props.page + 1),
        pageSize: String(props.pageSize),
    })

    return (
        <div className="overflow-hidden rounded-2xl border border-zinc-200 bg-white shadow-sm">
            <div className="border-b border-zinc-200 px-4 py-3 text-sm font-semibold">Schedule</div>
            <div className="divide-y divide-zinc-200">
                {grid.items.map((s) => (
                    <a
                        key={s.id}
                        href={buildHref(paths.medPassScheduleProps(), { q: props.query ?? undefined, page: String(props.page), pageSize: String(props.pageSize), pane: s.id })}
                        className="flex items-start justify-between gap-3 px-4 py-3 hover:bg-zinc-50"
                        data-testid="schedule-row"
                    >
                        <div className="min-w-0">
                            <div className="truncate text-sm font-medium">
                                {s.residentName} · {s.medName}
                            </div>
                            <div className="mt-0.5 text-xs text-zinc-500">
                                {s.frequency} · {s.residentId} · {s.medId}
                            </div>
                        </div>
                        <div className="shrink-0 text-sm font-semibold tabular-nums">{s.time}</div>
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

function SchedulePane(props: { mode: 'new' | 'edit'; scheduleId?: string; onCloseHref: string }) {
    const mutations = useMutations()
    const create = useSubmit(mutations.MedpassSchedule.mutate)
    const update = useSubmit((req) => mutations.MedpassScheduleById.mutate(props.scheduleId!, req))
    const del = useSubmit((req) => mutations.MedpassScheduleByIdDelete.mutate(props.scheduleId!, req))

    const title = props.mode === 'new' ? 'New schedule entry' : `Edit schedule ${props.scheduleId}`

    const [residentId, setResidentId] = useState('')
    const [medId, setMedId] = useState('')
    const [time, setTime] = useState('09:00')
    const [frequency, setFrequency] = useState('daily')

    const onSave = async () => {
        if (props.mode === 'new') {
            const created = await create.submit({ residentId, medId, time, frequency })
            if (!created) return
            window.location.assign(props.onCloseHref)
            return
        }
        const updated = await update.submit({
            residentId: residentId || undefined,
            medId: medId || undefined,
            time: time || undefined,
            frequency: frequency || undefined,
        })
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
                    <div className="mt-0.5 text-xs text-zinc-500">All ids are server-owned (seeded in repo).</div>
                </div>
                <a href={props.onCloseHref} className="text-sm text-zinc-600 hover:underline" aria-label="Close med pass schedule pane">
                    Close
                </a>
            </div>
            <div className="space-y-4 p-4">
                <TextField
                    label="Resident id"
                    value={residentId}
                    onChange={(v) => setResidentId(v)}
                    validationState={validation?.ResidentId?.length ? 'invalid' : undefined}
                    errorMessage={validation?.ResidentId?.[0]}
                />
                <TextField
                    label="Med id"
                    value={medId}
                    onChange={(v) => setMedId(v)}
                    validationState={validation?.MedId?.length ? 'invalid' : undefined}
                    errorMessage={validation?.MedId?.[0]}
                />
                <TextField
                    label="Time (HH:mm)"
                    value={time}
                    onChange={(v) => setTime(v)}
                    validationState={validation?.Time?.length ? 'invalid' : undefined}
                    errorMessage={validation?.Time?.[0]}
                />
                <TextField
                    label="Frequency"
                    value={frequency}
                    onChange={(v) => setFrequency(v)}
                    validationState={validation?.Frequency?.length ? 'invalid' : undefined}
                    errorMessage={validation?.Frequency?.[0]}
                />

                {(create.error || update.error || del.error) ? <div className="text-sm text-red-600">{create.error ?? update.error ?? del.error}</div> : null}

                <div className="flex items-center justify-between gap-2">
                    {props.mode === 'edit' ? (
                        <Button variant="negative" onPress={onDelete} isDisabled={del.busy} data-testid="schedule-delete">
                            Delete
                        </Button>
                    ) : (
                        <span />
                    )}
                    <div className="flex items-center gap-2">
                        <Button variant="secondary" onPress={() => window.location.assign(props.onCloseHref)}>
                            Cancel
                        </Button>
                        <Button variant="primary" onPress={onSave} isDisabled={create.busy || update.busy} data-testid="schedule-save">
                            Save
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    )
}

export default function MedPassSchedulePage() {
    const props = useMedPassScheduleProps()

    const base = paths.medPassScheduleProps()
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
                    <div className="mt-1 text-sm text-zinc-600">Manage scheduled med passes.</div>
                </div>
                <a href={newHref} className="rounded-xl bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700" data-testid="schedule-new">
                    New schedule
                </a>
            </div>

            <div className="grid grid-cols-1 gap-4 lg:grid-cols-[360px_1fr]">
                <div className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm">
                    <div className="text-sm font-semibold">Filters</div>
                    <div className="mt-4 space-y-3">
                        <TextField label="Search" value={q} onChange={(v) => setQ(v)} />
                        <a href={applyHref} className="inline-block rounded-xl border border-zinc-200 px-3 py-2 text-sm hover:bg-zinc-50" data-testid="schedule-apply">
                            Apply
                        </a>
                    </div>
                </div>

                <Suspense fallback={<GridSkeleton />}>
                    <ScheduleGrid />
                </Suspense>
            </div>

            {props.pane === 'new' ? <SchedulePane mode="new" onCloseHref={closeHref} /> : null}
            {props.pane && props.pane !== 'new' ? <SchedulePane mode="edit" scheduleId={props.pane} onCloseHref={closeHref} /> : null}
        </div>
    )
}

