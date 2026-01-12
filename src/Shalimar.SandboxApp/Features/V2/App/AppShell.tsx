import { Heading } from '@react-spectrum/s2'

function NavLink(props: { href: string; label: string }) {
    return (
        <a
            href={props.href}
            className="block rounded-xl px-3 py-2 text-sm text-zinc-700 hover:bg-zinc-100 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        >
            {props.label}
        </a>
    )
}

export function AppShell(props: { children: React.ReactNode }) {
    return (
        <div className="min-h-full bg-zinc-50 text-zinc-950">
            <header className="sticky top-0 z-10 border-b border-zinc-200 bg-white">
                <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-3">
                    <Heading level={3}>Shalimar Senior Living</Heading>
                    <div className="text-xs text-zinc-500">Server-first · Generated routes/store</div>
                </div>
            </header>

            <div className="mx-auto grid max-w-6xl grid-cols-1 gap-6 px-6 py-6 lg:grid-cols-[240px_1fr]">
                <aside className="space-y-2">
                    <div className="rounded-2xl border border-zinc-200 bg-white p-2">
                        <div className="px-3 py-2 text-[11px] font-semibold uppercase tracking-wide text-zinc-500">Navigation</div>
                        <NavLink href="/" label="Welcome" />
                        <NavLink href="/dashboard" label="Dashboard" />
                        <NavLink href="/residents" label="Residents" />
                    </div>
                </aside>

                <main className="min-w-0 rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm">{props.children}</main>
            </div>
        </div>
    )
}

