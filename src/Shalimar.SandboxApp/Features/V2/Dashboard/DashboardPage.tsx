import { Button, Heading, Text } from '@react-spectrum/s2'
import { useState } from 'react'
import {
    useDashboardProps,
    useDashboardPropsIncidentsLazy,
    useDashboardPropsObservationsLazy,
    useDashboardPropsResidentsLazy,
} from '@generated/store'

function Modal(props: { title: string; onClose: () => void; children: React.ReactNode }) {
    return (
        <div className="fixed inset-0 z-50" data-testid="dash-modal">
            <div className="absolute inset-0 bg-black/30" onClick={props.onClose} />
            <div className="absolute left-1/2 top-16 w-[min(720px,92vw)] -translate-x-1/2 overflow-hidden rounded-2xl border border-zinc-200 bg-white shadow-xl">
                <div className="flex items-center justify-between gap-3 border-b border-zinc-200 px-4 py-3">
                    <div className="min-w-0">
                        <div className="text-sm font-semibold" data-testid="dash-modal-title">
                            {props.title}
                        </div>
                        <div className="mt-0.5 text-xs text-zinc-500">Loaded via Lazy leaf (server-owned)</div>
                    </div>
                    <Button isQuiet onPress={props.onClose}>
                        Close
                    </Button>
                </div>
                <div className="p-4">{props.children}</div>
            </div>
        </div>
    )
}

function Widget(props: { title: string; value: number; onOpen: () => void; testId: string }) {
    return (
        <button
            type="button"
            onClick={props.onOpen}
            data-testid={props.testId}
            className="rounded-2xl border border-zinc-200 bg-white p-4 text-left shadow-sm hover:bg-zinc-50 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        >
            <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500">{props.title}</div>
            <div className="mt-2 text-3xl font-semibold tabular-nums">{props.value}</div>
            <div className="mt-2 text-xs text-zinc-500">Click to view details</div>
        </button>
    )
}

export default function DashboardPage() {
    const props = useDashboardProps()
    const residents = useDashboardPropsResidentsLazy()
    const incidents = useDashboardPropsIncidentsLazy()
    const observations = useDashboardPropsObservationsLazy()

    const [open, setOpen] = useState<'residents' | 'incidents' | 'observations' | null>(null)

    const openResidents = async () => {
        setOpen('residents')
        await residents.load()
    }
    const openIncidents = async () => {
        setOpen('incidents')
        await incidents.load()
    }
    const openObservations = async () => {
        setOpen('observations')
        await observations.load()
    }

    return (
        <div className="space-y-6">
            <div>
                <Heading level={2}>{props.title}</Heading>
                <Text UNSAFE_className="text-sm text-zinc-600">Widgets are immediate; details load lazily into a modal.</Text>
            </div>

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                <Widget title="Incidents" value={props.counts.incidents} onOpen={openIncidents} testId="dash-incidents" />
                <Widget title="Observations" value={props.counts.observations} onOpen={openObservations} testId="dash-observations" />
                <Widget title="Residents" value={props.counts.residents} onOpen={openResidents} testId="dash-residents" />
            </div>

            {open === 'residents' ? (
                <Modal title="Residents" onClose={() => setOpen(null)}>
                    {residents.status === 'resolved' && residents.data ? (
                        <div className="space-y-2">
                            {residents.data.items.map((r) => (
                                <div key={r.id} className="flex items-center justify-between rounded-xl border border-zinc-200 px-3 py-2">
                                    <div className="min-w-0">
                                        <div className="text-sm font-medium">{r.name}</div>
                                        <div className="mt-0.5 text-xs text-zinc-500">
                                            {r.careLevel} · Room {r.room}
                                        </div>
                                    </div>
                                    <div className="text-xs text-zinc-500">{r.id}</div>
                                </div>
                            ))}
                        </div>
                    ) : (
                        <div className="text-sm text-zinc-600">Loading…</div>
                    )}
                </Modal>
            ) : null}

            {open === 'incidents' ? (
                <Modal title="Incidents" onClose={() => setOpen(null)}>
                    {incidents.status === 'resolved' && incidents.data ? (
                        <div className="space-y-2">
                            {incidents.data.items.map((i) => (
                                <div key={i.id} className="rounded-xl border border-zinc-200 px-3 py-2">
                                    <div className="flex items-center justify-between gap-3">
                                        <div className="text-sm font-medium">{i.kind}</div>
                                        <div className="text-xs text-zinc-500">{i.status}</div>
                                    </div>
                                    <div className="mt-1 text-xs text-zinc-600">{i.summary}</div>
                                </div>
                            ))}
                        </div>
                    ) : (
                        <div className="text-sm text-zinc-600">Loading…</div>
                    )}
                </Modal>
            ) : null}

            {open === 'observations' ? (
                <Modal title="Observations" onClose={() => setOpen(null)}>
                    {observations.status === 'resolved' && observations.data ? (
                        <div className="space-y-2">
                            {observations.data.items.map((o) => (
                                <div key={o.id} className="rounded-xl border border-zinc-200 px-3 py-2">
                                    <div className="flex items-center justify-between gap-3">
                                        <div className="text-sm font-medium">{o.kind}</div>
                                        <div className="text-xs text-zinc-500">{o.residentId}</div>
                                    </div>
                                    <div className="mt-1 text-xs text-zinc-600">{o.note}</div>
                                </div>
                            ))}
                        </div>
                    ) : (
                        <div className="text-sm text-zinc-600">Loading…</div>
                    )}
                </Modal>
            ) : null}
        </div>
    )
}

