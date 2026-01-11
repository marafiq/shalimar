import { Link, Outlet, createRootRoute } from '@tanstack/react-router'
import { Heading, Text } from '@react-spectrum/s2'

export const Route = createRootRoute({
    component: RootComponent,
})

function RootComponent() {
    return (
        <div className="min-h-full">
            <header className="border-b border-black/10 dark:border-white/10">
                <div className="mx-auto max-w-7xl px-4 py-3 sm:px-6">
                    <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
                        <div>
                            <Heading level={2}>Shalimar CRM</Heading>
                            <Text>Server-first React with Spectrum S2 + Tailwind + TanStack.</Text>
                        </div>
                        <nav className="flex flex-wrap gap-3 text-sm">
                            <NavLink to="/">Dashboard</NavLink>
                            <NavLink to="/tasks">Tasks</NavLink>
                            <NavLink to="/tasks/board">Board</NavLink>
                            <NavLink to="/accounts">Accounts</NavLink>
                            <NavLink to="/settings">Settings</NavLink>
                        </nav>
                    </div>
                </div>
            </header>
            <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
                <Outlet />
            </main>
        </div>
    )
}

function NavLink(props: { to: string; children: string }) {
    return (
        <Link
            to={props.to}
            className="rounded-md px-2 py-1 hover:bg-black/5 focus:outline-none focus:ring-2 focus:ring-blue-500/50 dark:hover:bg-white/10"
            activeProps={{ className: 'bg-black/5 dark:bg-white/10 font-medium' }}
        >
            {props.children}
        </Link>
    )
}
