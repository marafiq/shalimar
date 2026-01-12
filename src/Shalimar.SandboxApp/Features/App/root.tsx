import { Outlet, createRootRouteWithContext } from '@tanstack/react-router'
import type { AppContext } from './appContext'
import { AppShell } from '../V2/App/AppShell'

export const Route = createRootRouteWithContext<AppContext>()({
    component: () => (
        <AppShell>
            <Outlet />
        </AppShell>
    ),
})

