import { Button, Heading, TextField } from '@react-spectrum/s2'
import { Suspense, useMemo, useState } from 'react'
import { paths } from '@generated/paths'
import type { ValidationErrors } from '@generated/store'
import { usePassMedsProps, usePassMedsPropsDueDeferred, usePassMedsPropsRecentDeferred } from '@generated/store'
import { useMutations } from '@generated/useMutations'

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

function DueList(props: { onPass: (scheduleId: string, outcome: string) => void }) {
    const due = usePassMedsPropsDueDeferred()
    if (!due.items.length) return <div className="text-sm text-zinc-600">No scheduled meds.</div>

    return (
        <div className="space-y-2">
            {due.items.map((d) => (
                <div key={d.scheduleId} className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-zinc-200 bg-white px-4 py-3">
                    <div className="min-w-0">
                        <div className="text-sm font-medium">
                            {d.time} · {d.residentName} · {d.medName}
                        </div>
                        <div className="mt-0.5 text-xs text-zinc-500">
                            {d.frequency} · schedule {d.scheduleId}
                        </div>
                    </div>
                    <div className="flex items-center gap-2">
                        <Button variant="primary" onPress={() => props.onPass(d.scheduleId, 'given')} data-testid="pass-given">
                            Given
                        </Button>
                        <Button variant="secondary" onPress={() => props.onPass(d.scheduleId, 'refused')} data-testid="pass-refused">
                            Refused
                        </Button>
                    </div>
                </div>
            ))}
        </div>
    )
}

function RecentList() {
    const recent = usePassMedsPropsRecentDeferred()
    if (!recent.items.length) return <div className="text-sm text-zinc-600">No recent passes yet.</div>

    return (
        <div className="space-y-2">
            {recent.items.map((p) => (
                <div key={p.id} className="rounded-xl border border-zinc-200 bg-white px-3 py-2">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                        <div className="text-sm font-medium">
                            {p.residentName} · {p.medName}
                        </div>
                        <div className="text-xs text-zinc-500">{new Date(p.ts).toLocaleString()}</div>
                    </div>
                    <div className="mt-1 text-xs text-zinc-600">
                        Outcome: <span className="font-semibold">{p.outcome}</span>
                        {p.note ? ` · ${p.note}` : ''}
                    </div>
                </div>
            ))}
        </div>
    )
}

export default function PassMedsPage() {
    const props = usePassMedsProps()
    const mutations = useMutations()
    const pass = useSubmit(mutations.MedpassPass.mutate)

    const base = paths.passMedsProps()
    const [q, setQ] = useState(props.query ?? '')
    const applyHref = useMemo(() => buildHref(base, { q: q.trim() || undefined }), [base, q])

    const onPass = async (scheduleId: string, outcome: string) => {
        const res = await pass.submit({ scheduleId, outcome, note: null })
        if (!res) return
        // server invalidation refreshes due+recent; full reload keeps server truth
        window.location.assign(buildHref(base, { q: props.query ?? undefined }))
    }

    return (
        <div className="space-y-6">
            <div className="flex flex-wrap items-end justify-between gap-3">
                <div className="min-w-0">
                    <Heading level={2}>{props.title}</Heading>
                    <div className="mt-1 text-sm text-zinc-600">Pass meds and record outcomes.</div>
                </div>
            </div>

            <div className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm">
                <div className="text-sm font-semibold">Resident filter</div>
                <div className="mt-3 flex flex-wrap items-end gap-3">
                    <div className="min-w-[280px]">
                        <TextField label="Search" value={q} onChange={(v) => setQ(v)} />
                    </div>
                    <a href={applyHref} className="inline-block rounded-xl border border-zinc-200 px-3 py-2 text-sm hover:bg-zinc-50" data-testid="pass-apply">
                        Apply
                    </a>
                </div>
            </div>

            {(pass.validation || pass.error) ? (
                <div className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
                    {pass.error ?? 'Validation error'}
                </div>
            ) : null}

            <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                <div>
                    <div className="mb-2 text-sm font-semibold">Due</div>
                    <Suspense fallback={<div className="text-sm text-zinc-600">Loading…</div>}>
                        <DueList onPass={onPass} />
                    </Suspense>
                </div>
                <div>
                    <div className="mb-2 text-sm font-semibold">Recent</div>
                    <Suspense fallback={<div className="text-sm text-zinc-600">Loading…</div>}>
                        <RecentList />
                    </Suspense>
                </div>
            </div>
        </div>
    )
}

