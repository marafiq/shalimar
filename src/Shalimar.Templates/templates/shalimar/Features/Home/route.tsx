import { createFileRoute, Link } from '@tanstack/react-router'
import { Button, Card, CardPreview, Heading, Text, TextField } from '@react-spectrum/s2'
import { Suspense, useEffect, useMemo, useState } from 'react'
import { useStore } from '@tanstack/react-store'
import { createTask, crmStore, ensureAccounts, ensureTasks, ensureUsers, refreshActivity } from '../Crm/store'
import { PageSkeleton } from '../App/ui/Skeletons'
import { useBehaviors, useDeferred, useLazy, useSse, useStream } from '@shalimar/runtime'
import type { CrmActivityExportRowDto, CrmForecastDto, CrmInsightsDto, CrmSseEventDto, DashboardProps } from '@generated/shalimar-types.g'

export const Route = createFileRoute('/')({
    validateSearch: (search: Record<string, unknown>) => ({
        skeleton: search.skeleton === '1' || search.skeleton === 'true',
    }),
    loader: async () => {
        await Promise.all([ensureUsers(), ensureAccounts(), ensureTasks(), refreshActivity()])
        return null
    },
    pendingComponent: () => <PageSkeleton />,
    component: HomeRoute,
})

function HomeRoute() {
    const state = useStore(crmStore)
    const { skeleton } = Route.useSearch()
    const [query, setQuery] = useState('')
    const [creating, setCreating] = useState(false)

    if (skeleton) return <PageSkeleton />

    // Simulate "streamed" updates by polling activity.
    useEffect(() => {
        const id = setInterval(() => {
            void refreshActivity()
        }, 2500)
        return () => clearInterval(id)
    }, [])

    const tasks = useMemo(() => Object.values(state.tasks), [state.tasks])
    const accounts = useMemo(() => Object.values(state.accounts), [state.accounts])

    const overdue = tasks.filter((t) => t.dueAt && new Date(t.dueAt).getTime() < Date.now() && t.status !== 'done')
    const open = tasks.filter((t) => t.status !== 'done')
    const filtered = open.filter((t) => t.title.toLowerCase().includes(query.trim().toLowerCase()))

    const props = (window.__SHALIMAR_PROPS__ ?? { message: 'Welcome to Shalimar' }) as DashboardProps
    useBehaviors(props.agentPanel.behaviors)

    return (
        <div className="grid gap-6 lg:grid-cols-12">
            <section className="lg:col-span-8">
                <Heading level={1}>{props.message}</Heading>
                <div className="mt-2 max-w-prose">
                    <Text>
                        CRM Agent Task Management (mock). Routes and data access will eventually be generated from
                        server-defined Shalimar components/modes.
                    </Text>
                </div>

                <div className="mt-6 grid gap-4 sm:grid-cols-3">
                    <KpiCard title="Open tasks" value={open.length} />
                    <KpiCard title="Overdue" value={overdue.length} tone={overdue.length ? 'danger' : 'neutral'} />
                    <KpiCard title="Accounts" value={accounts.length} />
                </div>

                <div className="mt-6">
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                        <div className="max-w-sm">
                            <TextField
                                label="Quick search"
                                placeholder="Search open tasks…"
                                value={query}
                                onChange={setQuery}
                            />
                        </div>
                        <div className="flex gap-2">
                            <Button
                                data-testid="dashboard-create-task"
                                isDisabled={creating}
                                onPress={() => {
                                    setCreating(true)
                                    void createTask('Follow up with Contoso').finally(() => setCreating(false))
                                }}
                            >
                                {creating ? 'Creating…' : 'New task'}
                            </Button>
                            <Button elementType={Link as any} to="/tasks">
                                Go to tasks
                            </Button>
                            <Button elementType={Link as any} to="/tasks/board" isQuiet>
                                Board
                            </Button>
                        </div>
                    </div>

                    <div className="mt-4 grid gap-3">
                        {filtered.slice(0, 5).map((t) => (
                            <Card key={t.id}>
                                <CardPreview>
                                    <div className="flex items-center justify-between gap-4 p-4">
                                        <div className="min-w-0">
                                            <div className="truncate font-medium">{t.title}</div>
                                            <div className="text-sm opacity-70">Status: {t.status.replaceAll('_', ' ')}</div>
                                        </div>
                                        <Button elementType={Link as any} to={`/tasks/${t.id}`} isQuiet>
                                            Open
                                        </Button>
                                    </div>
                                </CardPreview>
                            </Card>
                        ))}
                    </div>
                </div>
            </section>

            <aside className="lg:col-span-4">
                <Card>
                    <CardPreview>
                        <div className="p-5">
                            <Heading level={3}>Live activity</Heading>
                            <div className="mt-3 space-y-2">
                                {state.activity.slice(0, 6).map((a) => (
                                    <div key={a.id} className="rounded-md border border-black/10 p-2 text-sm">
                                        <div className="opacity-70">{new Date(a.ts).toLocaleTimeString()}</div>
                                        <div>{a.summary}</div>
                                    </div>
                                ))}
                            </div>
                        </div>
                    </CardPreview>
                </Card>

                <div className="mt-4">
                    <Card>
                        <CardPreview>
                            <div className="p-5">
                                <div className="flex items-end justify-between gap-3">
                                    <Heading level={3}>Agent insights</Heading>
                                    <div className="text-xs text-zinc-500 dark:text-zinc-400">Deferred</div>
                                </div>

                                <div className="mt-3">
                                    <Suspense fallback={<InsightsSkeleton />}>
                                        <InsightsLoaded href={props.agentPanel.props.insights.href} />
                                    </Suspense>
                                </div>
                            </div>
                        </CardPreview>
                    </Card>
                </div>

                <div className="mt-4">
                    <Card>
                        <CardPreview>
                            <div className="p-5">
                                <div className="flex items-end justify-between gap-3">
                                    <Heading level={3}>Pipeline forecast</Heading>
                                    <div className="text-xs text-zinc-500 dark:text-zinc-400">Lazy</div>
                                </div>

                                <div className="mt-3">
                                    <ForecastPanel href={props.agentPanel.props.forecast.href} />
                                </div>
                            </div>
                        </CardPreview>
                    </Card>
                </div>

                <div className="mt-4">
                    <Card>
                        <CardPreview>
                            <div className="p-5">
                                <div className="flex items-end justify-between gap-3">
                                    <Heading level={3}>Activity (SSE)</Heading>
                                    <div className="text-xs text-zinc-500 dark:text-zinc-400">SSE</div>
                                </div>

                                <div className="mt-3">
                                    <SsePanel href={props.agentPanel.props.activitySse.href} />
                                </div>
                            </div>
                        </CardPreview>
                    </Card>
                </div>

                <div className="mt-4">
                    <Card>
                        <CardPreview>
                            <div className="p-5">
                                <div className="flex items-end justify-between gap-3">
                                    <Heading level={3}>Activity export</Heading>
                                    <div className="text-xs text-zinc-500 dark:text-zinc-400">Streamed</div>
                                </div>

                                <div className="mt-3">
                                    <StreamedPanel href={props.agentPanel.props.activityExport.href} />
                                </div>
                            </div>
                        </CardPreview>
                    </Card>
                </div>
            </aside>
        </div>
    )
}

function KpiCard(props: { title: string; value: number; tone?: 'neutral' | 'danger' }) {
    const tone = props.tone ?? 'neutral'
    const ring =
        tone === 'danger'
            ? 'ring-1 ring-red-500/30 bg-red-50 dark:bg-red-500/10'
            : 'ring-1 ring-black/10 dark:ring-white/10'
    return (
        <Card>
            <CardPreview>
                <div className={`rounded-lg p-4 ${ring}`}>
                    <div className="text-sm opacity-70">{props.title}</div>
                    <div className="mt-1 text-2xl font-semibold tabular-nums">{props.value}</div>
                </div>
            </CardPreview>
        </Card>
    )
}

function InsightsLoaded(props: { href: string }) {
    const data = useDeferred<CrmInsightsDto>({ href: props.href })
    return (
        <div className="space-y-3 text-sm">
            <div className="rounded-xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">Summary</div>
                <div className="mt-1" data-testid="insights-summary">
                    {data.summary}
                </div>
            </div>
            <div className="grid gap-3">
                <div className="rounded-xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                    <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">Risks</div>
                    <ul className="mt-2 list-disc space-y-1 pl-5">
                        {data.risks.map((r) => (
                            <li key={r}>{r}</li>
                        ))}
                    </ul>
                </div>
                <div className="rounded-xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                    <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">Next actions</div>
                    <ul className="mt-2 list-disc space-y-1 pl-5">
                        {data.nextActions.map((n) => (
                            <li key={n}>{n}</li>
                        ))}
                    </ul>
                </div>
            </div>
        </div>
    )
}

function InsightsSkeleton() {
    return (
        <div className="space-y-3" data-testid="insights-skeleton">
            <div className="rounded-xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                <div className="h-3 w-24 rounded bg-zinc-200 dark:bg-zinc-800" />
                <div className="mt-2 h-4 w-[90%] rounded bg-zinc-200 dark:bg-zinc-800" />
                <div className="mt-2 h-4 w-[70%] rounded bg-zinc-200 dark:bg-zinc-800" />
            </div>
            <div className="rounded-xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                <div className="h-3 w-16 rounded bg-zinc-200 dark:bg-zinc-800" />
                <div className="mt-2 h-3 w-[85%] rounded bg-zinc-200 dark:bg-zinc-800" />
                <div className="mt-2 h-3 w-[72%] rounded bg-zinc-200 dark:bg-zinc-800" />
                <div className="mt-2 h-3 w-[64%] rounded bg-zinc-200 dark:bg-zinc-800" />
            </div>
        </div>
    )
}

function ForecastPanel(props: { href: string }) {
    const { status, data, error, load, reset } = useLazy<CrmForecastDto>({ href: props.href })

    if (status === 'idle') {
        return (
            <div className="space-y-3 text-sm">
                <div className="text-sm text-zinc-600 dark:text-zinc-300">
                    Load forecast only when needed (agent + human intent).
                </div>
                <div className="flex flex-wrap gap-2">
                    <Button onPress={() => void load()}>Load forecast</Button>
                </div>
            </div>
        )
    }

    if (status === 'pending') {
        return <ForecastSkeleton />
    }

    if (status === 'rejected') {
        return (
            <div className="space-y-3 text-sm">
                <div className="rounded-xl border border-red-500/30 bg-red-500/10 p-3 text-red-800 dark:text-red-200">
                    Failed to load forecast.
                </div>
                <div className="text-xs opacity-70">{String(error)}</div>
                <div className="flex gap-2">
                    <Button onPress={() => void load()}>Retry</Button>
                    <Button isQuiet onPress={reset}>
                        Reset
                    </Button>
                </div>
            </div>
        )
    }

    return (
        <div className="space-y-3 text-sm">
            <div className="rounded-xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">Summary</div>
                <div className="mt-1">{data?.summary}</div>
            </div>
            <div className="rounded-xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">Drivers</div>
                <ul className="mt-2 list-disc space-y-1 pl-5">
                    {(data?.drivers ?? []).map((d) => (
                        <li key={d}>{d}</li>
                    ))}
                </ul>
            </div>
            <div className="rounded-xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                    Recommended plays
                </div>
                <ul className="mt-2 list-disc space-y-1 pl-5">
                    {(data?.recommendedPlays ?? []).map((p) => (
                        <li key={p}>{p}</li>
                    ))}
                </ul>
            </div>
            <div className="flex gap-2">
                <Button isQuiet onPress={reset}>
                    Clear
                </Button>
            </div>
        </div>
    )
}

function ForecastSkeleton() {
    return (
        <div className="space-y-3">
            <div className="rounded-xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                <div className="h-3 w-24 rounded bg-zinc-200 dark:bg-zinc-800" />
                <div className="mt-2 h-4 w-[88%] rounded bg-zinc-200 dark:bg-zinc-800" />
                <div className="mt-2 h-4 w-[62%] rounded bg-zinc-200 dark:bg-zinc-800" />
            </div>
            <div className="rounded-xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                <div className="h-3 w-20 rounded bg-zinc-200 dark:bg-zinc-800" />
                <div className="mt-2 h-3 w-[80%] rounded bg-zinc-200 dark:bg-zinc-800" />
                <div className="mt-2 h-3 w-[72%] rounded bg-zinc-200 dark:bg-zinc-800" />
                <div className="mt-2 h-3 w-[66%] rounded bg-zinc-200 dark:bg-zinc-800" />
            </div>
        </div>
    )
}

function SsePanel(props: { href: string }) {
    const { status, lastEvent, start, stop, reset } = useSse<CrmSseEventDto>({ href: props.href }, { maxEvents: 12 })

    const summary =
        lastEvent?.type === 'activity'
            ? lastEvent.activity.summary
            : lastEvent?.type === 'tick'
              ? lastEvent.activity.summary
              : lastEvent
                ? JSON.stringify(lastEvent)
                : 'Not connected.'

    return (
        <div className="space-y-3 text-sm">
            <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="text-xs text-zinc-500 dark:text-zinc-400" data-testid="sse-status">
                    status: {status}
                </div>
                <div className="flex flex-wrap gap-2">
                    <Button data-testid="sse-start" onPress={start} isDisabled={status === 'open' || status === 'connecting'}>
                        Start SSE
                    </Button>
                    <Button isQuiet onPress={stop} isDisabled={status !== 'open' && status !== 'connecting'}>
                        Stop
                    </Button>
                    <Button isQuiet onPress={reset}>
                        Reset
                    </Button>
                </div>
            </div>

            <div
                className="rounded-xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40"
                data-testid="sse-last"
            >
                <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">Last event</div>
                <div className="mt-1">{summary}</div>
            </div>

            <div className="text-xs text-zinc-500 dark:text-zinc-400">
                Tip: SSE is off by default to keep initial render deterministic.
            </div>
        </div>
    )
}

function StreamedPanel(props: { href: string }) {
    const { status, lastItem, start, abort, reset } = useStream<CrmActivityExportRowDto>({ href: props.href }, { maxItems: 50 })

    const last = lastItem ? `#${lastItem.index}: ${lastItem.activity.summary}` : 'No rows yet.'

    return (
        <div className="space-y-3 text-sm">
            <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="text-xs text-zinc-500 dark:text-zinc-400" data-testid="streamed-status">
                    status: {status}
                </div>
                <div className="flex flex-wrap gap-2">
                    <Button data-testid="streamed-start" onPress={() => void start()} isDisabled={status === 'streaming'}>
                        Start export
                    </Button>
                    <Button isQuiet onPress={abort} isDisabled={status !== 'streaming'}>
                        Abort
                    </Button>
                    <Button isQuiet onPress={reset}>
                        Reset
                    </Button>
                </div>
            </div>

            <div className="rounded-xl border border-zinc-200 bg-white p-3 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40" data-testid="streamed-last">
                <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">Last row</div>
                <div className="mt-1">{last}</div>
            </div>
        </div>
    )
}

