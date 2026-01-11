import { Link, useRouterState } from '@tanstack/react-router'
import { useStore } from '@tanstack/react-store'
import { Button, Heading, Text, TextField } from '@react-spectrum/s2'
import { IconBell, IconChevronRight, IconMenu } from './icons'
import { setMobileNavOpen, toggleNotifications, toggleSidebarCollapsed, uiStore } from './store'

export function AppShell(props: { children: React.ReactNode }) {
    const ui = useStore(uiStore)
    const pathname = useRouterState({ select: (s) => s.location.pathname })
    const unread = ui.notifications.filter((n) => !n.read).length
    const gridCols = ui.sidebarCollapsed ? 'lg:grid-cols-[84px_1fr]' : 'lg:grid-cols-[280px_1fr]'

    return (
        <div className="min-h-full bg-white text-black dark:bg-zinc-950 dark:text-white">
            {/* Top bar */}
            <header className="sticky top-0 z-30 border-b border-black/10 bg-white/80 backdrop-blur dark:border-white/10 dark:bg-zinc-950/80">
                <div className="mx-auto flex max-w-7xl items-center gap-3 px-4 py-3 sm:px-6">
                    <div className="lg:hidden">
                        <Button isQuiet onPress={() => setMobileNavOpen(true)} aria-label="Open navigation">
                            <IconMenu />
                        </Button>
                    </div>
                    <div className="flex min-w-0 items-baseline gap-2">
                        <Heading level={3}>Shalimar CRM</Heading>
                        <span className="hidden text-xs opacity-60 sm:inline">Agent Task Management</span>
                    </div>

                    <div className="ml-auto flex min-w-0 items-center gap-2">
                        <div className="hidden w-[min(440px,40vw)] lg:block">
                            <TextField label="Search" aria-label="Global search" placeholder="Search tasks, accounts…" />
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
                        <div className="hidden items-center gap-2 rounded-full border border-black/10 bg-black/5 px-3 py-1 text-xs dark:border-white/10 dark:bg-white/10 sm:flex">
                            <span className="h-2 w-2 rounded-full bg-emerald-500" />
                            <span className="opacity-80">Online</span>
                        </div>
                    </div>
                </div>
            </header>

            <div className={`mx-auto grid max-w-7xl grid-cols-1 gap-6 px-4 py-6 sm:px-6 ${gridCols}`}>
                {/* Desktop sidebar */}
                <aside className="hidden lg:block">
                    <div className="sticky top-[84px] space-y-4">
                        <div className="flex items-center justify-between">
                            {ui.sidebarCollapsed ? null : <div className="px-2 text-xs font-semibold uppercase tracking-wide opacity-60">Navigation</div>}
                            <Button isQuiet onPress={toggleSidebarCollapsed} aria-label="Toggle sidebar">
                                <span className={ui.sidebarCollapsed ? 'rotate-180 inline-flex' : 'inline-flex'}>
                                    <IconChevronRight />
                                </span>
                            </Button>
                        </div>
                        <SidebarSection title="Work">
                            <NavItem collapsed={ui.sidebarCollapsed} to="/" active={pathname === '/'} label="Dashboard" />
                            <NavItem collapsed={ui.sidebarCollapsed} to="/tasks" active={pathname === '/tasks'} label="Tasks" />
                            <NavItem collapsed={ui.sidebarCollapsed} to="/tasks/board" active={pathname === '/tasks/board'} label="Board" />
                        </SidebarSection>
                        <SidebarSection title="Customers">
                            <NavItem collapsed={ui.sidebarCollapsed} to="/accounts" active={pathname.startsWith('/accounts')} label="Accounts" />
                        </SidebarSection>
                        <SidebarSection title="System">
                            <NavItem collapsed={ui.sidebarCollapsed} to="/settings" active={pathname === '/settings'} label="Settings" />
                        </SidebarSection>

                        <div className={`rounded-2xl border border-black/10 bg-black/5 p-4 text-sm dark:border-white/10 dark:bg-white/10 ${ui.sidebarCollapsed ? 'hidden' : ''}`}>
                            <div className="font-semibold">Agent queue</div>
                            <div className="mt-1 opacity-70">
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
                        <div className="absolute left-0 top-0 h-full w-[min(320px,86vw)] border-r border-black/10 bg-white p-4 shadow-xl dark:border-white/10 dark:bg-zinc-950">
                            <div className="flex items-center justify-between gap-3">
                                <Heading level={3}>Navigation</Heading>
                                <Button isQuiet onPress={() => setMobileNavOpen(false)} aria-label="Close navigation">
                                    Close
                                </Button>
                            </div>
                            <div className="mt-4 space-y-4">
                                <SidebarSection title="Work">
                                    <NavItem to="/" active={pathname === '/'} label="Dashboard" onNavigate={() => setMobileNavOpen(false)} />
                                    <NavItem to="/tasks" active={pathname === '/tasks'} label="Tasks" onNavigate={() => setMobileNavOpen(false)} />
                                    <NavItem to="/tasks/board" active={pathname === '/tasks/board'} label="Board" onNavigate={() => setMobileNavOpen(false)} />
                                </SidebarSection>
                                <SidebarSection title="Customers">
                                    <NavItem to="/accounts" active={pathname.startsWith('/accounts')} label="Accounts" onNavigate={() => setMobileNavOpen(false)} />
                                </SidebarSection>
                                <SidebarSection title="System">
                                    <NavItem to="/settings" active={pathname === '/settings'} label="Settings" onNavigate={() => setMobileNavOpen(false)} />
                                </SidebarSection>
                            </div>
                        </div>
                    </div>
                ) : null}

                <main className="min-w-0">{props.children}</main>
            </div>
        </div>
    )
}

function SidebarSection(props: { title: string; children: React.ReactNode }) {
    return (
        <div>
            <div className="px-2 text-xs font-semibold uppercase tracking-wide opacity-60">{props.title}</div>
            <div className="mt-2 space-y-1">{props.children}</div>
        </div>
    )
}

function NavItem(props: { to: string; label: string; active: boolean; collapsed?: boolean; onNavigate?: () => void }) {
    const mono = props.label.slice(0, 1)
    return (
        <Link
            to={props.to}
            onClick={props.onNavigate}
            className={[
                'flex items-center justify-between rounded-xl px-3 py-2 text-sm',
                'hover:bg-black/5 dark:hover:bg-white/10',
                'focus:outline-none focus:ring-2 focus:ring-blue-500/50',
                props.active ? 'bg-black/5 font-semibold dark:bg-white/10' : 'opacity-90',
            ].join(' ')}
        >
            {props.collapsed ? (
                <span className="inline-flex h-7 w-7 items-center justify-center rounded-lg border border-black/10 bg-white/60 text-xs font-semibold dark:border-white/10 dark:bg-zinc-950/40">
                    {mono}
                </span>
            ) : (
                <>
                    <span className="truncate">{props.label}</span>
                    <span className="text-xs opacity-40">›</span>
                </>
            )}
        </Link>
    )
}

function Kpi(props: { label: string; value: string }) {
    return (
        <div className="rounded-xl border border-black/10 bg-white/60 px-2 py-2 text-center dark:border-white/10 dark:bg-zinc-950/40">
            <div className="text-[10px] uppercase tracking-wide opacity-60">{props.label}</div>
            <div className="mt-1 text-sm font-semibold tabular-nums">{props.value}</div>
        </div>
    )
}

