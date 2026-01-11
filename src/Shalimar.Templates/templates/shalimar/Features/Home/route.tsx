import { createFileRoute, Link } from '@tanstack/react-router'
import { Button, Card, CardPreview, Heading, Text, TextField } from '@react-spectrum/s2'
import { useEffect, useMemo, useState } from 'react'
import { useStore } from '@tanstack/react-store'
import { crmStore, ensureAccounts, ensureTasks, ensureUsers, refreshActivity } from '../../Client/store'
import { PageSkeleton } from '../../Client/ui/Skeletons'

export const Route = createFileRoute('/')({
    loader: async () => {
        await Promise.all([ensureUsers(), ensureAccounts(), ensureTasks(), refreshActivity()])
        return null
    },
    pendingComponent: () => <PageSkeleton />,
    component: HomeRoute,
})

function HomeRoute() {
    const state = useStore(crmStore)
    const [query, setQuery] = useState('')

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

    const props = (window.__SHALIMAR_PROPS__ ?? { message: 'Welcome to Shalimar' }) as { message: string }

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

