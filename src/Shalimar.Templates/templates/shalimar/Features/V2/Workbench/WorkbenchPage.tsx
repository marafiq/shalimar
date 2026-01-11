import { Button, Heading, Text } from '@react-spectrum/s2'
import { useBehaviors } from '@shalimar/runtime'
import { Suspense } from 'react'
import {
    useWorkbenchProps,
    useWorkbenchPropsAgentPanelInsightsDeferred,
    useWorkbenchPropsSummaryDeferred,
} from '@generated/store'

function SummaryCard(props: { title: string; value: string; hint?: string }) {
    return (
        <div className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
            <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">{props.title}</div>
            <div className="mt-2 text-2xl font-semibold">{props.value}</div>
            {props.hint ? <div className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">{props.hint}</div> : null}
        </div>
    )
}

function Summary() {
    const summary = useWorkbenchPropsSummaryDeferred<{ openTasks: number; overdueTasks: number; activeAgents: number }>()
    return (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <SummaryCard title="Open tasks" value={String(summary.openTasks)} />
            <SummaryCard title="Overdue" value={String(summary.overdueTasks)} hint="Based on fixed clock in tests" />
            <SummaryCard title="Active agents" value={String(summary.activeAgents)} />
        </div>
    )
}

function AgentInsights() {
    const insights = useWorkbenchPropsAgentPanelInsightsDeferred<{ headline: string; suggestions: string[] }>()
    return (
        <div className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
            <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                    <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">Agent panel</div>
                    <div className="mt-2 text-sm font-medium">{insights.headline}</div>
                </div>
                <Button variant="primary" onPress={() => {}}>
                    New task
                </Button>
            </div>
            <div className="mt-3 space-y-2">
                {insights.suggestions.map((s) => (
                    <div key={s} className="flex items-center justify-between rounded-xl border border-zinc-200 px-3 py-2 text-sm dark:border-zinc-800">
                        <Text>{s}</Text>
                        <span className="text-xs text-zinc-500 dark:text-zinc-400">Draft</span>
                    </div>
                ))}
            </div>
        </div>
    )
}

export default function WorkbenchPage() {
    const props = useWorkbenchProps()

    // Server-owned behavior plan: prefetch deferred leaves for snappy UX after hydration.
    useBehaviors(props.agentPanel.behaviors)

    return (
        <div className="space-y-6">
            <div className="flex flex-wrap items-end justify-between gap-3">
                <div className="min-w-0">
                    <Heading level={2}>{props.title}</Heading>
                    <div className="mt-1 text-sm text-zinc-600 dark:text-zinc-300">
                        v2 proof: pure TSX renderer + generated route module + server-authored tree bindings
                    </div>
                </div>
            </div>

            <Suspense
                fallback={
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-3" data-testid="v2-summary-skeleton">
                        <SummaryCard title="Open tasks" value="…" />
                        <SummaryCard title="Overdue" value="…" />
                        <SummaryCard title="Active agents" value="…" />
                    </div>
                }
            >
                <Summary />
            </Suspense>

            <Suspense
                fallback={
                    <div className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                        <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">Agent panel</div>
                        <div className="mt-2 h-4 w-2/3 animate-pulse rounded bg-zinc-200 dark:bg-zinc-800" />
                        <div className="mt-4 space-y-2">
                            <div className="h-10 animate-pulse rounded-xl bg-zinc-200 dark:bg-zinc-800" />
                            <div className="h-10 animate-pulse rounded-xl bg-zinc-200 dark:bg-zinc-800" />
                        </div>
                    </div>
                }
            >
                <AgentInsights />
            </Suspense>
        </div>
    )
}
