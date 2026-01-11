import { Outlet, createRootRouteWithContext } from '@tanstack/react-router'
import { AppShell } from './ui/AppShell'
import { NotificationsPanel } from './ui/NotificationsPanel'
import { ToastHost } from './ui/Toasts'
import { ModalHost } from './ui/ModalHost'
import type { AppContext } from './appContext'

export const Route = createRootRouteWithContext<AppContext>()({
    component: RootComponent,
})

function RootComponent() {
    return (
        <>
            <AppShell>
                <Outlet />
            </AppShell>
            <NotificationsPanel />
            <ToastHost />
            <ModalHost />
        </>
    )
}

