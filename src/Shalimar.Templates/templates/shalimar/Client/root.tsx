import { Outlet, createRootRoute } from '@tanstack/react-router'
import { Heading, Text } from '@react-spectrum/s2'

export const Route = createRootRoute({
    component: RootComponent,
})

function RootComponent() {
    return (
        <div className="min-h-full">
            <header className="border-b border-black/10 dark:border-white/10">
                <div className="mx-auto max-w-6xl px-4 py-3 sm:px-6">
                    <Heading level={2}>Shalimar</Heading>
                    <Text>Server-first React with Spectrum S2 + Tailwind.</Text>
                </div>
            </header>
            <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
                <Outlet />
            </main>
        </div>
    )
}
