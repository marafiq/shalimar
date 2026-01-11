import { Link, useRouterState } from '@tanstack/react-router'
import { useStore } from '@tanstack/react-store'
import { Button, Heading, Text, TextField } from '@react-spectrum/s2'
import { IconAccounts, IconBell, IconBoard, IconChevronRight, IconDashboard, IconMenu, IconSettings, IconTasks, IconX } from './icons'
import { setMobileNavOpen, toggleNotifications, toggleSidebarCollapsed, uiStore } from './store'

export function AppShell(props: { children: React.ReactNode }) {
    const ui = useStore(uiStore)
    const pathname = useRouterState({ select: (s) => s.location.pathname })
    const unread = ui.notifications.filter((n) => !n.read).length
    const gridCols = ui.sidebarCollapsed ? 'lg:grid-cols-[92px_1fr]' : 'lg:grid-cols-[304px_1fr]'

    return (
        <div className="min-h-full bg-zinc-50 text-zinc-950 dark:bg-zinc-950 dark:text-zinc-50">
            {/* Top bar */}
            <header className="sticky top-0 z-30 border-b border-zinc-200/70 bg-white/70 backdrop-blur supports-[backdrop-filter]:bg-white/60 dark:border-zinc-800/80 dark:bg-zinc-950/60">
                <div className="mx-auto flex max-w-7xl items-center gap-3 px-4 py-2.5 sm:px-6">
                    <div className="lg:hidden">
                        <Button isQuiet onPress={() => setMobileNavOpen(true)} aria-label="Open navigation">
                            <IconMenu />
                        </Button>
                    </div>
                    <div className="flex min-w-0 items-center gap-2">
                        <div className="hidden h-8 w-8 items-center justify-center rounded-xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-900 sm:flex">
                            <span className="text-xs font-semibold tracking-wide">S</span>
                        </div>
                        <div className="min-w-0">
                            <Heading level={3}>Shalimar CRM</Heading>
                            <div className="hidden text-xs text-zinc-500 dark:text-zinc-400 sm:block">
                                Agent task management (mock)
                            </div>
                        </div>
                    </div>

                    <div className="ml-auto flex min-w-0 items-center gap-2">
                        <div className="hidden w-[min(520px,44vw)] lg:block">
                            <div className="relative">
                                <TextField label="Search" aria-label="Global search" placeholder="Search tasks, accounts…" />
                                <div className="pointer-events-none absolute right-3 top-2 hidden select-none rounded-md border border-zinc-200 bg-white px-1.5 py-0.5 text-[10px] text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-400 xl:block">
                                    ⌘K
                                </div>
                            </div>
                        </div>
                        <div className="relative">
                            <Button
                                isQuiet
                                onPress={toggleNotifications}
                                aria-label="Open notifications"
                                data-testid="top-notifications"
                            >
                                <IconBell />
                            </Button>
                            {unread ? (
                                <span className="pointer-events-none absolute right-1 top-1 h-2.5 w-2.5 rounded-full bg-blue-500 ring-2 ring-white dark:ring-zinc-950" />
                            ) : null}
                        </div>
                        <div className="hidden items-center gap-2 rounded-full border border-zinc-200 bg-white px-3 py-1 text-xs shadow-sm dark:border-zinc-800 dark:bg-zinc-900 sm:flex">
                            <span className="h-2 w-2 rounded-full bg-emerald-500" />
                            <span className="text-zinc-600 dark:text-zinc-300">Online</span>
                        </div>
                    </div>
                </div>
            </header>

            <div className={`mx-auto grid max-w-7xl grid-cols-1 gap-5 px-4 py-6 sm:px-6 ${gridCols}`}>
                {/* Desktop sidebar */}
                <aside className="hidden lg:block">
                    <div className="sticky top-[72px] space-y-4">
                        <div className="flex items-center justify-between">
                            {ui.sidebarCollapsed ? null : (
                                <div className="px-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                                    Navigation
                                </div>
                            )}
                            <Button isQuiet onPress={toggleSidebarCollapsed} aria-label="Toggle sidebar">
                                <span className={ui.sidebarCollapsed ? 'rotate-180 inline-flex' : 'inline-flex'}>
                                    <IconChevronRight />
                                </span>
                            </Button>
                        </div>
                        <div className="rounded-2xl border border-zinc-200 bg-white p-2 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                            <SidebarSection title="Work" collapsed={ui.sidebarCollapsed}>
                                <NavItem
                                    collapsed={ui.sidebarCollapsed}
                                    to="/"
                                    active={pathname === '/'}
                                    label="Dashboard"
                                    icon={<IconDashboard className="h-4 w-4" />}
                                />
                                <NavItem
                                    collapsed={ui.sidebarCollapsed}
                                    to="/tasks"
                                    active={pathname === '/tasks'}
                                    label="Tasks"
                                    icon={<IconTasks className="h-4 w-4" />}
                                />
                                <NavItem
                                    collapsed={ui.sidebarCollapsed}
                                    to="/tasks/board"
                                    active={pathname === '/tasks/board'}
                                    label="Board"
                                    icon={<IconBoard className="h-4 w-4" />}
                                />
                            </SidebarSection>
                            <SidebarSection title="Customers" collapsed={ui.sidebarCollapsed}>
                                <NavItem
                                    collapsed={ui.sidebarCollapsed}
                                    to="/accounts"
                                    active={pathname.startsWith('/accounts')}
                                    label="Accounts"
                                    icon={<IconAccounts className="h-4 w-4" />}
                                />
                            </SidebarSection>
                            <SidebarSection title="System" collapsed={ui.sidebarCollapsed}>
                                <NavItem
                                    collapsed={ui.sidebarCollapsed}
                                    to="/settings"
                                    active={pathname === '/settings'}
                                    label="Settings"
                                    icon={<IconSettings className="h-4 w-4" />}
                                />
                            </SidebarSection>
                        </div>

                        <div
                            className={[
                                'rounded-2xl border border-zinc-200 bg-white p-4 text-sm shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40',
                                ui.sidebarCollapsed ? 'hidden' : '',
                            ].join(' ')}
                        >
                            <div className="font-semibold">Agent queue</div>
                            <div className="mt-1 text-zinc-600 dark:text-zinc-300">
                                Routing model: <span className="font-medium">Mock</span>
                            </div>
                            <div className="mt-3 grid grid-cols-3 gap-2 text-xs">
                                <Kpi label="Open" value="12" />
                                <Kpi label="SLA" value="98%" />
                                <Kpi label="CSAT" value="4.8" />
                            </div>
                        </div>
                    </div>
                </aside>

                {/* Mobile nav overlay */}
                {ui.mobileNavOpen ? (
                    <div className="fixed inset-0 z-40 lg:hidden" aria-label="Mobile navigation">
                        <div className="absolute inset-0 bg-black/30 backdrop-blur-sm" onClick={() => setMobileNavOpen(false)} />
                        <div className="absolute left-0 top-0 h-full w-[min(336px,88vw)] border-r border-zinc-200 bg-white p-4 shadow-xl dark:border-zinc-800 dark:bg-zinc-950">
                            <div className="flex items-center justify-between gap-3">
                                <div className="min-w-0">
                                    <Heading level={3}>Navigation</Heading>
                                    <div className="mt-0.5 text-xs text-zinc-500 dark:text-zinc-400">CRM agent workspace</div>
                                </div>
                                <Button isQuiet onPress={() => setMobileNavOpen(false)} aria-label="Close navigation">
                                    <IconX />
                                </Button>
                            </div>
                            <div className="mt-4 space-y-4">
                                <div className="rounded-2xl border border-zinc-200 bg-white p-2 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                                    <SidebarSection title="Work">
                                        <NavItem
                                            to="/"
                                            active={pathname === '/'}
                                            label="Dashboard"
                                            icon={<IconDashboard className="h-4 w-4" />}
                                            onNavigate={() => setMobileNavOpen(false)}
                                        />
                                        <NavItem
                                            to="/tasks"
                                            active={pathname === '/tasks'}
                                            label="Tasks"
                                            icon={<IconTasks className="h-4 w-4" />}
                                            onNavigate={() => setMobileNavOpen(false)}
                                        />
                                        <NavItem
                                            to="/tasks/board"
                                            active={pathname === '/tasks/board'}
                                            label="Board"
                                            icon={<IconBoard className="h-4 w-4" />}
                                            onNavigate={() => setMobileNavOpen(false)}
                                        />
                                    </SidebarSection>
                                    <SidebarSection title="Customers">
                                        <NavItem
                                            to="/accounts"
                                            active={pathname.startsWith('/accounts')}
                                            label="Accounts"
                                            icon={<IconAccounts className="h-4 w-4" />}
                                            onNavigate={() => setMobileNavOpen(false)}
                                        />
                                    </SidebarSection>
                                    <SidebarSection title="System">
                                        <NavItem
                                            to="/settings"
                                            active={pathname === '/settings'}
                                            label="Settings"
                                            icon={<IconSettings className="h-4 w-4" />}
                                            onNavigate={() => setMobileNavOpen(false)}
                                        />
                                    </SidebarSection>
                                </div>
                            </div>
                        </div>
                    </div>
                ) : null}

                <main className="min-w-0 rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 sm:p-6">
                    {props.children}
                </main>
            </div>
        </div>
    )
}

function SidebarSection(props: { title: string; children: React.ReactNode; collapsed?: boolean }) {
    return (
        <div>
            {props.collapsed ? null : (
                <div className="px-2 py-2 text-[11px] font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                    {props.title}
                </div>
            )}
            <div className="space-y-1">{props.children}</div>
            {props.collapsed ? null : <div className="my-2 h-px bg-zinc-200/70 dark:bg-zinc-800/70" />}
        </div>
    )
}

function NavItem(props: {
    to: string
    label: string
    active: boolean
    collapsed?: boolean
    icon?: React.ReactNode
    onNavigate?: () => void
}) {
    return (
        <Link
            to={props.to}
            onClick={props.onNavigate}
            className={[
                'group relative flex items-center justify-between rounded-xl px-3 py-2 text-sm',
                'hover:bg-zinc-100 dark:hover:bg-zinc-900/60',
                'focus:outline-none focus:ring-2 focus:ring-blue-500/40',
                props.active ? 'bg-zinc-100 font-medium text-zinc-950 dark:bg-zinc-900/60 dark:text-zinc-50' : 'text-zinc-700 dark:text-zinc-300',
            ].join(' ')}
            aria-label={props.collapsed ? props.label : undefined}
        >
            <span
                className={[
                    'pointer-events-none absolute left-1 top-1/2 h-6 w-1 -translate-y-1/2 rounded-full bg-blue-500 transition-opacity',
                    props.active ? 'opacity-100' : 'opacity-0 group-hover:opacity-40',
                ].join(' ')}
            />
            <div className="flex min-w-0 items-center gap-3">
                <span className="inline-flex h-7 w-7 items-center justify-center rounded-lg border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950">
                    {props.icon ?? <span className="text-xs font-semibold">{props.label.slice(0, 1)}</span>}
                </span>
                {props.collapsed ? null : <span className="truncate">{props.label}</span>}
            </div>
            {props.collapsed ? null : <span className="text-xs text-zinc-400 dark:text-zinc-600">›</span>}
        </Link>
    )
}

function Kpi(props: { label: string; value: string }) {
    return (
        <div className="rounded-xl border border-zinc-200 bg-white px-2 py-2 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-950">
            <div className="text-[10px] uppercase tracking-wide text-zinc-500 dark:text-zinc-400">{props.label}</div>
            <div className="mt-1 text-sm font-semibold tabular-nums">{props.value}</div>
        </div>
    )
}

