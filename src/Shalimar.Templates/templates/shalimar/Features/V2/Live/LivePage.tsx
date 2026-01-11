import { Button, Heading, Text } from '@react-spectrum/s2'
import { Suspense, useEffect } from 'react'
import { useV2LiveProps, useV2LivePropsRealtimePanelAuditStream, useV2LivePropsRealtimePanelNotificationsSse, useV2LivePropsSummaryDeferred, useV2LivePropsTimelineLazy } from '@generated/store'

function Card(props: { title: string; children: React.ReactNode; footer?: React.ReactNode }) {
    return (
        <div className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
            <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">{props.title}</div>
            <div className="mt-3">{props.children}</div>
            {props.footer ? <div className="mt-3 border-t border-zinc-200/70 pt-3 text-xs text-zinc-500 dark:border-zinc-800/70 dark:text-zinc-400">{props.footer}</div> : null}
        </div>
    )
}

function SummaryCards() {
    const summary = useV2LivePropsSummaryDeferred()
    return (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <Card title="Open tasks">
                <div className="text-2xl font-semibold tabular-nums">{summary.openTasks}</div>
            </Card>
            <Card title="Overdue">
                <div className="text-2xl font-semibold tabular-nums">{summary.overdueTasks}</div>
            </Card>
            <Card title="Active agents">
                <div className="text-2xl font-semibold tabular-nums">{summary.activeAgents}</div>
            </Card>
            <Card title="Unread notifications">
                <div className="text-2xl font-semibold tabular-nums">{summary.unreadNotifications}</div>
            </Card>
        </div>
    )
}

function Timeline() {
    const tl = useV2LivePropsTimelineLazy()

    useEffect(() => {
        // Keep deterministic initial render: do not auto-load.
    }, [])

    return (
        <Card
            title="Timeline (Lazy)"
            footer={
                <div className="flex items-center justify-between gap-3">
                    <span data-testid="v2live-timeline-status">status: {tl.status}</span>
                    <Button variant="secondary" onPress={() => tl.load()} data-testid="v2live-timeline-load">
                        Load timeline
                    </Button>
                </div>
            }
        >
            {tl.status === 'resolved' && tl.data ? (
                <div className="space-y-2">
                    {tl.data.items.slice(0, 6).map((i) => (
                        <div key={i.id} className="flex items-start justify-between gap-3 rounded-xl border border-zinc-200/70 bg-zinc-50 px-3 py-2 dark:border-zinc-800/70 dark:bg-zinc-900/30">
                            <div className="min-w-0">
                                <div className="text-sm font-medium">{i.summary}</div>
                                <div className="mt-0.5 text-xs text-zinc-500 dark:text-zinc-400">{i.kind}</div>
                            </div>
                            <div className="shrink-0 text-xs text-zinc-500 dark:text-zinc-400 tabular-nums">{new Date(i.ts).toLocaleTimeString()}</div>
                        </div>
                    ))}
                </div>
            ) : (
                <div className="text-sm text-zinc-600 dark:text-zinc-300">
                    This is a user-triggered fetch. It stays idle until you click <span className="font-medium">Load timeline</span>.
                </div>
            )}
        </Card>
    )
}

function AuditStream() {
    const stream = useV2LivePropsRealtimePanelAuditStream({ maxItems: 50 })

    return (
        <Card
            title="Audit export (Stream)"
            footer={
                <div className="flex flex-wrap items-center justify-between gap-2">
                    <span data-testid="v2live-stream-status">status: {stream.status}</span>
                    <div className="flex items-center gap-2">
                        <Button variant="secondary" onPress={() => stream.start()} data-testid="v2live-stream-start">
                            Start
                        </Button>
                        <Button variant="secondary" onPress={() => stream.abort()} isDisabled={stream.status !== 'streaming'}>
                            Abort
                        </Button>
                        <Button variant="secondary" onPress={() => stream.reset()}>
                            Reset
                        </Button>
                    </div>
                </div>
            }
        >
            <div className="text-sm text-zinc-600 dark:text-zinc-300">
                {stream.lastItem ? (
                    <div data-testid="v2live-stream-last" className="rounded-xl border border-zinc-200/70 bg-zinc-50 px-3 py-2 font-mono text-xs dark:border-zinc-800/70 dark:bg-zinc-900/30">
                        {JSON.stringify(stream.lastItem)}
                    </div>
                ) : (
                    <span>Not started. Starting will stream NDJSON rows from the server.</span>
                )}
            </div>
        </Card>
    )
}

function NotificationsSse() {
    const sse = useV2LivePropsRealtimePanelNotificationsSse({ maxEvents: 25 })

    return (
        <Card
            title="Notifications (SSE)"
            footer={
                <div className="flex flex-wrap items-center justify-between gap-2">
                    <span data-testid="v2live-sse-status">status: {sse.status}</span>
                    <div className="flex items-center gap-2">
                        <Button variant="secondary" onPress={() => sse.start()} data-testid="v2live-sse-start">
                            Start
                        </Button>
                        <Button variant="secondary" onPress={() => sse.stop()} isDisabled={sse.status === 'idle' || sse.status === 'closed'}>
                            Stop
                        </Button>
                        <Button variant="secondary" onPress={() => sse.reset()}>
                            Reset
                        </Button>
                    </div>
                </div>
            }
        >
            <div className="text-sm text-zinc-600 dark:text-zinc-300">
                {sse.lastEvent ? (
                    <div data-testid="v2live-sse-last" className="rounded-xl border border-zinc-200/70 bg-zinc-50 px-3 py-2 font-mono text-xs dark:border-zinc-800/70 dark:bg-zinc-900/30">
                        {JSON.stringify(sse.lastEvent)}
                    </div>
                ) : (
                    <span>Not connected. Starting will open an EventSource to the server.</span>
                )}
            </div>
        </Card>
    )
}

export default function LivePage() {
    const props = useV2LiveProps()

    return (
        <div className="space-y-5">
            <div className="flex flex-wrap items-end justify-between gap-3">
                <div className="min-w-0">
                    <Heading level={2}>{props.title}</Heading>
                    <Text UNSAFE_className="text-sm text-zinc-600 dark:text-zinc-300">
                        Proves v2 mode contracts: Deferred (suspends), Lazy (user-triggered), Stream (finite NDJSON), SSE (realtime).
                    </Text>
                </div>
            </div>

            <Suspense
                fallback={
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
                        {Array.from({ length: 4 }).map((_, i) => (
                            <div
                                key={i}
                                className="h-[92px] animate-pulse rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40"
                            />
                        ))}
                    </div>
                }
            >
                <SummaryCards />
            </Suspense>

            <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
                <Timeline />
                <div className="space-y-4">
                    <AuditStream />
                    <NotificationsSse />
                </div>
            </div>
        </div>
    )
}

