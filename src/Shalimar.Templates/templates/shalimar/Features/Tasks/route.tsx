import { Outlet, createFileRoute, Link, useNavigate, useRouterState } from '@tanstack/react-router'
import { Button, Heading, Text } from '@react-spectrum/s2'
import { Suspense, useMemo } from 'react'
import { useStore } from '@tanstack/react-store'
import { crmStore, ensureAccounts, ensureEpics, ensureTasks, ensureUsers } from '../Crm/store'
import { PageSkeleton } from '../App/ui/Skeletons'
import { IconPlus } from '../App/ui/icons'
import { TaskDrawer } from './_components/TaskDrawer'
import type { TasksProps } from '@generated/types'
import { TasksFiltersPanel } from './_components/TasksFiltersPanel'
import { TasksGrid } from './_components/TasksGrid'
import { TasksGridSkeleton } from './_components/TasksGridSkeleton'
import { CreateTaskPane } from './_components/CreateTaskPane'

export const Route = createFileRoute('/tasks')({
    validateSearch: (search: Record<string, unknown>) => ({
        drawer: typeof search.drawer === 'string' ? search.drawer : undefined,
        epicId: typeof search.epicId === 'string' ? search.epicId : undefined,
        q: typeof search.q === 'string' ? search.q : '',
        status: typeof search.status === 'string' ? search.status : '',
        priority: typeof search.priority === 'string' ? search.priority : '',
        page: (() => {
            const raw = typeof search.page === 'number' ? search.page : typeof search.page === 'string' ? Number(search.page) : 1
            const n = Number.isFinite(raw) ? Math.floor(raw) : 1
            return n > 0 ? n : 1
        })(),
        pageSize: (() => {
            const raw =
                typeof search.pageSize === 'number' ? search.pageSize : typeof search.pageSize === 'string' ? Number(search.pageSize) : 20
            const n = Number.isFinite(raw) ? Math.floor(raw) : 20
            return Math.min(100, Math.max(5, n > 0 ? n : 20))
        })(),
        skeleton: search.skeleton === '1' || search.skeleton === 'true',
        drawerSkeleton: search.drawerSkeleton === '1' || search.drawerSkeleton === 'true',
        threadSkeleton: search.threadSkeleton === '1' || search.threadSkeleton === 'true',
    }),
    loader: async () => {
        await Promise.all([ensureUsers(), ensureAccounts(), ensureEpics(), ensureTasks()])
        return null
    },
    pendingComponent: () => <PageSkeleton />,
    component: TasksRoute,
})

function TasksRoute() {
    const { epics } = useStore(crmStore)
    const navigate = useNavigate()
    const { drawer, epicId, q, status, priority, page, pageSize, skeleton, drawerSkeleton, threadSkeleton } = Route.useSearch()
    const pathname = useRouterState({ select: (s) => s.location.pathname })

    if (skeleton) return <PageSkeleton />

    const isTasksIndex = pathname === '/tasks'
    const props = window.__SHALIMAR_PROPS__ as TasksProps
    const baseHref = props.grid.props.tasksGrid.href
    const href = useMemo(() => {
        const sp = new URLSearchParams()
        if (q.trim()) sp.set('q', q.trim())
        if (status) sp.set('status', status)
        if (priority) sp.set('priority', priority)
        if (epicId) sp.set('epicId', epicId)
        sp.set('page', String(Number.isFinite(page) && page > 0 ? page : 1))
        sp.set('pageSize', String(Number.isFinite(pageSize) && pageSize > 0 ? pageSize : 20))
        const qs = sp.toString()
        return qs ? `${baseHref}?${qs}` : baseHref
    }, [baseHref, epicId, page, pageSize, priority, q, status])

    return (
        <div>
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                <div>
                    <Heading level={1}>Tasks</Heading>
                    <Text>
                        Table view (server-truth). Filters + paging stay in the URL. Create happens in a right-side pane,
                        and the grid refreshes via typed invalidation without losing state.
                    </Text>
                </div>
                <div className="flex gap-2">
                    <Button onPress={() => navigate({ to: '/tasks', search: (s) => ({ ...s, drawer: 'new' }) })}>
                        <span className="inline-flex items-center gap-2">
                            <IconPlus className="h-4 w-4" /> New task
                        </span>
                    </Button>
                    <Button elementType={Link as any} to="/tasks/board" isQuiet>
                        Board view
                    </Button>
                </div>
            </div>

            {isTasksIndex ? (
                <div className="mt-6 grid gap-4 lg:grid-cols-12">
                    <aside className="lg:col-span-4">
                        <TasksFiltersPanel
                            epics={epics}
                            value={{ epicId, q, status, priority, page, pageSize }}
                            onChange={(next) => navigate({ to: '/tasks', search: (s) => ({ ...s, ...next }) })}
                            onClear={() =>
                                navigate({
                                    to: '/tasks',
                                    search: (s) => ({ ...s, q: '', status: '', priority: '', epicId: undefined, page: 1 }),
                                })
                            }
                        />
                    </aside>

                    <section className="lg:col-span-8">
                        <Suspense fallback={<TasksGridSkeleton />}>
                            <TasksGrid
                                href={href}
                                onOpenTask={(id) => navigate({ to: '/tasks', search: (s) => ({ ...s, drawer: id }) })}
                                onPageChange={(p) => navigate({ to: '/tasks', search: (s) => ({ ...s, page: p }) })}
                            />
                        </Suspense>
                    </section>
                </div>
            ) : (
                <div className="mt-6">
                    <Outlet />
                </div>
            )}

            {isTasksIndex ? (
                <>
                    <CreateTaskPane
                        open={drawer === 'new'}
                        epicId={epicId}
                        onClose={() =>
                            navigate({
                                to: '/tasks',
                                search: { drawer: undefined, epicId, q, status, priority, page, pageSize },
                            })
                        }
                    />
                    <TaskDrawer
                        open={typeof drawer === 'string' && drawer.length > 0 && drawer !== 'new'}
                        drawer={drawer === 'new' ? undefined : drawer}
                        prefillEpicId={epicId}
                        forceSkeleton={drawerSkeleton}
                        forceThreadSkeleton={threadSkeleton}
                        onClose={() =>
                            navigate({
                                to: '/tasks',
                                search: { drawer: undefined, epicId, q, status, priority, page, pageSize },
                            })
                        }
                    />
                </>
            ) : null}
        </div>
    )
}
